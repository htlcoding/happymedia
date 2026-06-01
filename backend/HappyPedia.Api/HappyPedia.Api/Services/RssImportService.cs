using HappyPedia.Api.Data;
using HappyPedia.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace HappyPedia.Api.Services;

public class RssImportService
{
    private readonly AppDbContext _db;
    private readonly KeywordService _keywordService;
    private readonly OpenAiScoringService _openAiScoringService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RssImportService> _logger;
    private readonly HttpClient _http;

    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ScriptStyleRegex = new(
        "<(script|style)[^>]*>.*?</\\1>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    private static readonly Regex WhitespaceRegex = new(
        "\\s+",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex NonWordRegex = new(
        "[^a-z0-9äöüß\\s-]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ImageSrcRegex = new(
        "<img[^>]+src=[\"'](?<url>[^\"']+)[\"']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex OgImageRegex1 = new(
        "<meta[^>]+property=[\"']og:image[\"'][^>]+content=[\"'](?<url>[^\"']+)[\"']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex OgImageRegex2 = new(
        "<meta[^>]+content=[\"'](?<url>[^\"']+)[\"'][^>]+property=[\"']og:image[\"']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    public RssImportService(
        AppDbContext db,
        KeywordService keywordService,
        OpenAiScoringService openAiScoringService,
        IConfiguration configuration,
        ILogger<RssImportService> logger,
        HttpClient http)
    {
        _db = db;
        _keywordService = keywordService;
        _openAiScoringService = openAiScoringService;
        _configuration = configuration;
        _logger = logger;
        _http = http;
    }

    public async Task<int> ImportAllFeeds(CancellationToken cancellationToken = default)
    {
        var maxItems = _configuration.GetValue<int?>("ImportSettings:MaxItemsPerFeed") ?? 5;

        var goodKeywords = NormalizeKeywords(_keywordService.GetGoodKeywords());
        var badKeywords = NormalizeKeywords(_keywordService.GetBadKeywords());

        _logger.LogInformation("Good Keywords geladen: {Count}", goodKeywords.Count);
        _logger.LogInformation("Bad Keywords geladen: {Count}", badKeywords.Count);

        var feeds = await _db.RssFeeds
            .Where(f => f.IsActive)
            .ToListAsync(cancellationToken);

        var imported = 0;
        var urlsImportedInThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var feed in feeds)
        {
            try
            {
                var candidates = await LoadFeedItemsAsync(feed.Url, cancellationToken);

                candidates = candidates
                    .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                    .Take(maxItems)
                    .ToList();

                if (candidates.Count == 0)
                {
                    _logger.LogWarning(
                        "Feed enthält keine lesbaren Artikel: {FeedName} | URL: {FeedUrl}",
                        feed.Name,
                        feed.Url);

                    continue;
                }

                var urls = candidates
                    .Select(x => x.Url)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var existingUrls = await _db.Articles
                    .AsNoTracking()
                    .Where(a => urls.Contains(a.Url))
                    .Select(a => a.Url)
                    .ToListAsync(cancellationToken);

                var existingUrlSet = existingUrls.ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var candidate in candidates)
                {
                    var url = candidate.Url;

                    if (existingUrlSet.Contains(url))
                    {
                        continue;
                    }

                    if (!urlsImportedInThisRun.Add(url))
                    {
                        continue;
                    }

                    var cleanSummary = CleanHtml(candidate.Summary);

                    var article = new Article
                    {
                        Title = WebUtility.HtmlDecode(candidate.Title).Trim(),
                        Summary = cleanSummary,
                        Content = cleanSummary,
                        Url = url,
                        Source = feed.Name,
                        ImageUrl = candidate.ImageUrl
                            ?? await ExtractImageUrl(
                                candidate.SyndicationItem,
                                candidate.RawDescription,
                                url,
                                cancellationToken),
                        PublishedAt = candidate.PublishedAt,
                        CreatedAt = DateTime.UtcNow
                    };

                    article.KeywordScore = CalculateKeywordScore(article, goodKeywords, badKeywords);
                    var aiAnalysis = await _openAiScoringService.AnalyzeArticleAsync(article, cancellationToken);

                    article.AiScore = aiAnalysis.AiScore;
                    article.Category = aiAnalysis.Category;
                    article.Categories = JsonSerializer.Serialize(aiAnalysis.Categories);
                    article.Tags = string.Join(", ", aiAnalysis.Tags);
                    article.Score = article.KeywordScore + article.AiScore;

                    _logger.LogInformation(
                        "{Score} | KW {KeywordScore} | AI {AiScore} | {Title}",
                        article.Score,
                        article.KeywordScore,
                        article.AiScore,
                        article.Title);

                    _db.Articles.Add(article);
                    imported++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Feed konnte nicht importiert werden, bleibt aber aktiv: {FeedName} | URL: {FeedUrl}",
                    feed.Name,
                    feed.Url);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return imported;
    }

    private async Task<List<ParsedFeedItem>> LoadFeedItemsAsync(
        string feedUrl,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);

        request.Headers.UserAgent.ParseAdd("HappyPedia/1.0");
        request.Headers.Accept.ParseAdd("application/rss+xml");
        request.Headers.Accept.ParseAdd("application/atom+xml");
        request.Headers.Accept.ParseAdd("application/xml");
        request.Headers.Accept.ParseAdd("text/xml");
        request.Headers.Accept.ParseAdd("*/*");

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);

        var normalFeedItems = TryParseSyndicationFeed(xml);

        if (normalFeedItems.Count > 0)
        {
            return normalFeedItems;
        }

        var rdfItems = TryParseRss10RdfFeed(xml);

        if (rdfItems.Count > 0)
        {
            return rdfItems;
        }

        return [];
    }

    private static List<ParsedFeedItem> TryParseSyndicationFeed(string xml)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                Async = false,
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            };

            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);

            var feed = SyndicationFeed.Load(reader);

            if (feed == null)
            {
                return [];
            }

            return feed.Items
                .Select(item =>
                {
                    var url = GetArticleUrl(item);

                    if (string.IsNullOrWhiteSpace(url))
                    {
                        return null;
                    }

                    var rawSummary = item.Summary?.Text ?? "";
                    var cleanTitle = WebUtility.HtmlDecode(item.Title?.Text ?? "Ohne Titel").Trim();

                    return new ParsedFeedItem(
                        Title: cleanTitle,
                        Summary: rawSummary,
                        RawDescription: rawSummary,
                        Url: url,
                        PublishedAt: GetPublishedAt(item),
                        ImageUrl: null,
                        SyndicationItem: item
                    );
                })
                .Where(item => item != null)
                .Select(item => item!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<ParsedFeedItem> TryParseRss10RdfFeed(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);

            XNamespace rss10 = "http://purl.org/rss/1.0/";
            XNamespace dc = "http://purl.org/dc/elements/1.1/";
            XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
            XNamespace media = "http://search.yahoo.com/mrss/";

            var items = document
                .Descendants(rss10 + "item")
                .Select(item =>
                {
                    var title = item.Element(rss10 + "title")?.Value?.Trim() ?? "";
                    var description = item.Element(rss10 + "description")?.Value?.Trim() ?? "";

                    var link = item.Element(rss10 + "link")?.Value?.Trim();

                    if (string.IsNullOrWhiteSpace(link))
                    {
                        link = item.Attribute(rdf + "about")?.Value?.Trim();
                    }

                    var dateText = item.Element(dc + "date")?.Value?.Trim();

                    var publishedAt = DateTime.UtcNow;

                    if (DateTimeOffset.TryParse(dateText, out var parsedOffset))
                    {
                        publishedAt = parsedOffset.UtcDateTime;
                    }
                    else if (DateTime.TryParse(dateText, out var parsedDate))
                    {
                        publishedAt = parsedDate.ToUniversalTime();
                    }

                    var imageUrl =
                        item.Element(media + "thumbnail")?.Attribute("url")?.Value?.Trim()
                        ?? item.Element(media + "content")?.Attribute("url")?.Value?.Trim();

                    return new ParsedFeedItem(
                        Title: WebUtility.HtmlDecode(title),
                        Summary: description,
                        RawDescription: description,
                        Url: link ?? "",
                        PublishedAt: publishedAt,
                        ImageUrl: imageUrl,
                        SyndicationItem: null
                    );
                })
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Title) &&
                    !string.IsNullOrWhiteSpace(item.Url))
                .ToList();

            return items;
        }
        catch
        {
            return [];
        }
    }

    private static double CalculateKeywordScore(
        Article article,
        List<string> goodKeywords,
        List<string> badKeywords)
    {
        var text = NormalizeText($"{article.Title} {article.Summary} {article.Content}");

        var goodHits = CountKeywordHits(text, goodKeywords);
        var badHits = CountKeywordHits(text, badKeywords);

        var score = goodHits * 15 - badHits * 20;

        return Math.Clamp(score, -100, 100);
    }

    private static int CountKeywordHits(string normalizedText, List<string> normalizedKeywords)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return 0;
        }

        var paddedText = $" {normalizedText} ";

        return normalizedKeywords.Count(keyword =>
            !string.IsNullOrWhiteSpace(keyword) &&
            paddedText.Contains($" {keyword} ", StringComparison.Ordinal));
    }

    private static List<string> NormalizeKeywords(List<string> keywords)
    {
        return keywords
            .Select(NormalizeText)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct()
            .ToList();
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var normalized = WebUtility.HtmlDecode(text)
            .ToLowerInvariant()
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");

        normalized = NonWordRegex.Replace(normalized, " ");
        normalized = normalized.Replace("-", " ");
        normalized = WhitespaceRegex.Replace(normalized, " ").Trim();

        return normalized;
    }

    private static string CleanHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var withoutScripts = ScriptStyleRegex.Replace(html, " ");
        var withoutTags = HtmlTagRegex.Replace(withoutScripts, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);

        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    private static string? GetArticleUrl(SyndicationItem item)
    {
        var mainLink = item.Links.FirstOrDefault(link =>
            link.Uri != null &&
            (
                string.IsNullOrWhiteSpace(link.RelationshipType) ||
                link.RelationshipType == "alternate"
            ));

        return mainLink?.Uri?.ToString()
            ?? item.Links.FirstOrDefault()?.Uri?.ToString();
    }

    private static DateTime GetPublishedAt(SyndicationItem item)
    {
        if (item.PublishDate != DateTimeOffset.MinValue)
        {
            return item.PublishDate.UtcDateTime;
        }

        if (item.LastUpdatedTime != DateTimeOffset.MinValue)
        {
            return item.LastUpdatedTime.UtcDateTime;
        }

        return DateTime.UtcNow;
    }

    private async Task<string?> ExtractImageUrl(
        SyndicationItem? item,
        string rawDescription,
        string articleUrl,
        CancellationToken cancellationToken)
    {
        if (item != null)
        {
            foreach (var link in item.Links)
            {
                if (link.RelationshipType == "enclosure" &&
                    link.MediaType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return MakeAbsoluteUrl(link.Uri.ToString(), articleUrl);
                }
            }

            foreach (var ext in item.ElementExtensions)
            {
                try
                {
                    var element = ext.GetObject<XElement>();
                    var imageUrl = element.Attribute("url")?.Value;

                    if (!string.IsNullOrWhiteSpace(imageUrl))
                    {
                        return MakeAbsoluteUrl(imageUrl, articleUrl);
                    }
                }
                catch
                {
                    // Manche RSS-Extensions lassen sich nicht sauber als XElement lesen.
                }
            }
        }

        var fromHtml = ExtractImageFromHtml(rawDescription);

        if (!string.IsNullOrWhiteSpace(fromHtml))
        {
            return MakeAbsoluteUrl(fromHtml, articleUrl);
        }

        return await ExtractOgImageFromPage(articleUrl, cancellationToken);
    }

    private static string? ExtractImageFromHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = ImageSrcRegex.Match(html);

        return match.Success
            ? WebUtility.HtmlDecode(match.Groups["url"].Value.Trim())
            : null;
    }

    private async Task<string?> ExtractOgImageFromPage(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            var html = await _http.GetStringAsync(url, cancellationToken);

            var match = OgImageRegex1.Match(html);

            if (!match.Success)
            {
                match = OgImageRegex2.Match(html);
            }

            if (!match.Success)
            {
                return null;
            }

            var imageUrl = WebUtility.HtmlDecode(match.Groups["url"].Value.Trim());

            return MakeAbsoluteUrl(imageUrl, url);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static string? MakeAbsoluteUrl(string? imageUrl, string articleUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        imageUrl = WebUtility.HtmlDecode(imageUrl.Trim());

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        if (Uri.TryCreate(articleUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, imageUrl, out var combinedUri))
        {
            return combinedUri.ToString();
        }

        return imageUrl;
    }

    private sealed record ParsedFeedItem(
        string Title,
        string Summary,
        string RawDescription,
        string Url,
        DateTime PublishedAt,
        string? ImageUrl,
        SyndicationItem? SyndicationItem
    );
}