using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
namespace HappyPedia.Api.Services;

public class KeywordService
{
    private const string ConfigFolder = "Config";
    private const string GoodKeywordsFile = "keywords_good.json";
    private const string BadKeywordsFile = "keyword_bad.json";

    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KeywordService> _logger;

    public KeywordService(
        IWebHostEnvironment env,
        IMemoryCache cache,
        ILogger<KeywordService> logger)
    {
        _env = env;
        _cache = cache;
        _logger = logger;
    }

    public List<string> GetGoodKeywords()
    {
        return LoadKeywords(GoodKeywordsFile);
    }

    public List<string> GetBadKeywords()
    {
        return LoadKeywords(BadKeywordsFile);
    }

    private List<string> LoadKeywords(string fileName)
    {
        var cacheKey = $"keywords:{fileName}";

        var keywords = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            return ReadKeywordsFromFile(fileName);
        });

        return keywords?.ToList() ?? new List<string>();
    }

    private List<string> ReadKeywordsFromFile(string fileName)
    {
        var path = Path.Combine(_env.ContentRootPath, ConfigFolder, fileName);

        if (!File.Exists(path))
        {
            _logger.LogWarning("Keyword-Datei wurde nicht gefunden: {Path}", path);
            return new List<string>();
        }

        try
        {
            var json = File.ReadAllText(path);

            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var keywords = JsonSerializer.Deserialize<List<string>>(json, options);

            if (keywords == null)
            {
                _logger.LogWarning("Keyword-Datei ist leer oder ungültig: {FileName}", fileName);
                return new List<string>();
            }

            return keywords
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .Select(NormalizeKeyword)
                .Distinct()
                .OrderBy(keyword => keyword)
                .ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Keyword-Datei enthält ungültiges JSON: {FileName}", fileName);
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Keyword-Datei: {FileName}", fileName);
            return new List<string>();
        }
    }

    private static string NormalizeKeyword(string keyword)
    {
        var cleaned = keyword.Trim().ToLowerInvariant();

        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        return cleaned;
    }
}