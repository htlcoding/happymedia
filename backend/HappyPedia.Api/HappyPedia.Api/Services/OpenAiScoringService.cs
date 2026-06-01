using HappyPedia.Api.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HappyPedia.Api.Services;

public class OpenAiScoringService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiScoringService> _logger;

    private const string OpenAiUrl = "https://api.openai.com/v1/responses";
    private const string DefaultModel = "gpt-4.1-mini";

    private static readonly string[] AllowedCategories =
    [
        "Allgemein",
        "Politik",
        "Wirtschaft",
        "Technologie",
        "Wissenschaft",
        "Gesundheit",
        "Klima & Umwelt",
        "Sport",
        "Kultur",
        "Gesellschaft",
        "Österreich",
        "International"
    ];

    public OpenAiScoringService(
        HttpClient http,
        IConfiguration configuration,
        ILogger<OpenAiScoringService> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<double> ScoreArticleAsync(
        Article article,
        CancellationToken cancellationToken = default)
    {
        var analysis = await AnalyzeArticleAsync(article, cancellationToken);
        return analysis.AiScore;
    }

    public async Task<ArticleAiAnalysis> AnalyzeArticleAsync(
        Article article,
        CancellationToken cancellationToken = default)
    {
        if (article == null)
        {
            return ArticleAiAnalysis.Default;
        }

        var apiKey =
            _configuration["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("OPENAI_API_KEY fehlt. AiScore wird auf 0 gesetzt.");
            return ArticleAiAnalysis.Default;
        }

        var title = CleanText(article.Title, 400);
        var summary = CleanText(article.Summary, 2500);
        var content = CleanText(article.Content, 2500);
        var source = CleanText(article.Source, 150);

        if (string.IsNullOrWhiteSpace(title) &&
            string.IsNullOrWhiteSpace(summary) &&
            string.IsNullOrWhiteSpace(content))
        {
            return ArticleAiAnalysis.Default;
        }

        var categories = string.Join(", ", AllowedCategories);

        var prompt = $$"""
Du bist ein Nachrichten-Analyse-System.

Analysiere den folgenden Nachrichtenartikel.

Aufgabe 1: Bewerte den Artikel auf einer Skala von -100 bis 100.

Bedeutung:
-100 = sehr schlechte Nachricht, z.B. Tod, Krieg, Gewalt, Katastrophe, schwere Krise
-50 = eher schlechte Nachricht
0 = neutral, Unterhaltung, Lifestyle oder unklar
50 = eher gute Nachricht
100 = sehr gute Nachricht, z.B. Rettung, Hilfe, Fortschritt, Lösung, Erfolg

Wichtig für den Score:
Gute Nachrichten bedeuten: gesellschaftlich positiv, hilfreich, lösungsorientiert oder hoffnungsvoll.
Nachrichten, die hetzen, Angst machen, Gewalt, Katastrophen oder Krisen beschreiben, sind negativ zu bewerten.
Gute Nachrichten sind progressive, lösungsorientierte oder konstruktive Nachrichten.

Aufgabe 2: Bestimme eine Hauptkategorie.

Aufgabe 3: Bestimme mehrere passende Kategorien.

Erlaubte Kategorien:
{{categories}}

Regeln für Kategorien:
- "category" ist genau eine Hauptkategorie.
- "categories" ist eine Liste mit 1 bis 4 passenden Kategorien.
- Verwende nur Kategorien aus der erlaubten Liste.
- Erfinde keine neuen Kategorien.
- Die Hauptkategorie muss auch in "categories" enthalten sein.

Aufgabe 4: Erstelle 1 bis 5 kurze Tags.
Tags sollen einzelne Themenwörter sein, keine ganzen Sätze.
Tags dürfen freier sein als Kategorien.

Gib wirklich NUR gültiges JSON zurück.
Keine Erklärung.
Kein Markdown.
Kein Text außerhalb von JSON.

JSON-Format:
{
  "aiScore": 0,
  "category": "Allgemein",
  "categories": ["Allgemein"],
  "tags": ["Tag1", "Tag2"]
}

Quelle:
{{source}}

Titel:
{{title}}

Zusammenfassung:
{{summary}}

Inhalt:
{{content}}
""";

        var body = new
        {
            model = _configuration["OpenAI:Model"] ?? DefaultModel,
            input = prompt,
            temperature = 0,
            max_output_tokens = 250,
            store = false
        };

        var json = JsonSerializer.Serialize(body);

        using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var result = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenAI Fehler. StatusCode: {StatusCode}. Antwort: {Response}",
                    response.StatusCode,
                    result);

                return ArticleAiAnalysis.Default;
            }

            var text = ExtractOutputText(result);

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("OpenAI Antwort enthält keinen lesbaren Text.");
                return ArticleAiAnalysis.Default;
            }

            return ParseAnalysis(text);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "OpenAI Anfrage wurde abgebrochen oder Timeout erreicht.");
            return ArticleAiAnalysis.Default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim OpenAI Scoring.");
            return ArticleAiAnalysis.Default;
        }
    }

    private static string CleanText(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(text, @"\s+", " ").Trim();

        if (cleaned.Length <= maxLength)
        {
            return cleaned;
        }

        return cleaned[..maxLength];
    }

    private static string? ExtractOutputText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("output_text", out var outputTextElement))
        {
            var outputText = outputTextElement.GetString();

            if (!string.IsNullOrWhiteSpace(outputText))
            {
                return outputText;
            }
        }

        if (!root.TryGetProperty("output", out var outputElement))
        {
            return null;
        }

        foreach (var outputItem in outputElement.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var contentElement))
            {
                continue;
            }

            foreach (var contentItem in contentElement.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var textElement))
                {
                    var text = textElement.GetString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        return null;
    }

    private static ArticleAiAnalysis ParseAnalysis(string text)
    {
        var cleaned = CleanJsonText(text);

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var aiScore = 0.0;
            var category = "Allgemein";
            var categories = new List<string>();
            var tags = new List<string>();

            if (root.TryGetProperty("aiScore", out var aiScoreElement))
            {
                aiScore = ParseScore(aiScoreElement.ToString());
            }
            else if (root.TryGetProperty("score", out var scoreElement))
            {
                aiScore = ParseScore(scoreElement.ToString());
            }

            if (root.TryGetProperty("category", out var categoryElement))
            {
                category = NormalizeCategory(categoryElement.GetString());
            }

            if (root.TryGetProperty("categories", out var categoriesElement) &&
                categoriesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var categoryItem in categoriesElement.EnumerateArray())
                {
                    var normalizedCategory = NormalizeCategory(categoryItem.GetString());

                    if (!string.IsNullOrWhiteSpace(normalizedCategory) &&
                        !categories.Contains(normalizedCategory, StringComparer.OrdinalIgnoreCase))
                    {
                        categories.Add(normalizedCategory);
                    }

                    if (categories.Count >= 4)
                    {
                        break;
                    }
                }
            }

            if (!categories.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                categories.Insert(0, category);
            }

            categories = categories
                .Where(c => AllowedCategories.Contains(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();

            if (categories.Count == 0)
            {
                categories.Add("Allgemein");
                category = "Allgemein";
            }

            if (!categories.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                category = categories[0];
            }

            if (root.TryGetProperty("tags", out var tagsElement) &&
                tagsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var tagElement in tagsElement.EnumerateArray())
                {
                    var tag = CleanTag(tagElement.GetString());

                    if (!string.IsNullOrWhiteSpace(tag) &&
                        !tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    {
                        tags.Add(tag);
                    }

                    if (tags.Count >= 5)
                    {
                        break;
                    }
                }
            }

            return new ArticleAiAnalysis(
                Math.Clamp(aiScore, -100, 100),
                category,
                categories,
                tags
            );
        }
        catch
        {
            var fallbackScore = ParseScore(text);

            return new ArticleAiAnalysis(
                fallbackScore,
                "Allgemein",
                ["Allgemein"],
                []
            );
        }
    }

    private static string CleanJsonText(string text)
    {
        var cleaned = text.Trim();

        if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[7..].Trim();
        }

        if (cleaned.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[3..].Trim();
        }

        if (cleaned.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^3].Trim();
        }

        var firstBrace = cleaned.IndexOf('{');
        var lastBrace = cleaned.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            cleaned = cleaned[firstBrace..(lastBrace + 1)];
        }

        return cleaned;
    }

    private static double ParseScore(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var numberText = Regex.Match(text, @"-?\d+([.,]\d+)?").Value;

        if (string.IsNullOrWhiteSpace(numberText))
        {
            return 0;
        }

        numberText = numberText.Replace(",", ".");

        if (!double.TryParse(
                numberText,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var score))
        {
            return 0;
        }

        return Math.Clamp(score, -100, 100);
    }

    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "Allgemein";
        }

        var cleaned = category.Trim();

        var exactMatch = AllowedCategories.FirstOrDefault(c =>
            string.Equals(c, cleaned, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(exactMatch))
        {
            return exactMatch;
        }

        var lower = cleaned.ToLowerInvariant();

        if (lower.Contains("politik") || lower.Contains("government") || lower.Contains("election"))
        {
            return "Politik";
        }

        if (lower.Contains("wirtschaft") || lower.Contains("business") || lower.Contains("finance") || lower.Contains("market") || lower.Contains("economy"))
        {
            return "Wirtschaft";
        }

        if (lower.Contains("tech") || lower.Contains("digital") || lower.Contains("ki") || lower.Contains("ai") || lower.Contains("software"))
        {
            return "Technologie";
        }

        if (lower.Contains("wissenschaft") || lower.Contains("forschung") || lower.Contains("science") || lower.Contains("research"))
        {
            return "Wissenschaft";
        }

        if (lower.Contains("gesundheit") || lower.Contains("medizin") || lower.Contains("health") || lower.Contains("medical"))
        {
            return "Gesundheit";
        }

        if (lower.Contains("klima") || lower.Contains("umwelt") || lower.Contains("climate") || lower.Contains("environment"))
        {
            return "Klima & Umwelt";
        }

        if (lower.Contains("sport") || lower.Contains("football") || lower.Contains("soccer"))
        {
            return "Sport";
        }

        if (lower.Contains("kultur") || lower.Contains("culture") || lower.Contains("film") || lower.Contains("music"))
        {
            return "Kultur";
        }

        if (lower.Contains("gesellschaft") || lower.Contains("society") || lower.Contains("social"))
        {
            return "Gesellschaft";
        }

        if (lower.Contains("österreich") || lower.Contains("austria") || lower.Contains("wien") || lower.Contains("vienna"))
        {
            return "Österreich";
        }

        if (lower.Contains("international") || lower.Contains("world") || lower.Contains("global") || lower.Contains("foreign"))
        {
            return "International";
        }

        return "Allgemein";
    }

    private static string CleanTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return "";
        }

        var cleaned = Regex.Replace(tag.Trim(), @"\s+", " ");

        cleaned = cleaned
            .Replace("[", "")
            .Replace("]", "")
            .Replace("\"", "")
            .Replace("'", "")
            .Trim();

        if (cleaned.Length > 40)
        {
            cleaned = cleaned[..40].Trim();
        }

        return cleaned;
    }
}

public record ArticleAiAnalysis(
    double AiScore,
    string Category,
    List<string> Categories,
    List<string> Tags
)
{
    public static ArticleAiAnalysis Default => new(
        0,
        "Allgemein",
        ["Allgemein"],
        []
    );
}