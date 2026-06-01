using HappyPedia.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HappyPedia.Api.Controllers;

[ApiController]
[Route("api/statistics")]
public class StatisticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatisticsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetStatistics(CancellationToken cancellationToken)
    {
        var articles = await _db.Articles
            .AsNoTracking()
            .Select(a => new ArticleStatisticsRow
            {
                Id = a.Id,
                Title = a.Title,
                Summary = a.Summary,
                Url = a.Url,
                Source = a.Source,
                ImageUrl = a.ImageUrl,
                PublishedAt = a.PublishedAt,
                CreatedAt = a.CreatedAt,
                Score = a.Score,
                KeywordScore = a.KeywordScore,
                AiScore = a.AiScore,
                Category = a.Category,
                Categories = a.Categories,
                Tags = a.Tags,
                Upvotes = a.Upvotes,
                Downvotes = a.Downvotes,
                CommentCount = _db.Comments.Count(c => c.ArticleId == a.Id)
            })
            .ToListAsync(cancellationToken);

        var totalArticles = articles.Count;
        var totalScore = articles.Sum(a => a.Score);
        var averageScore = totalArticles > 0 ? articles.Average(a => a.Score) : 0;

        var goodArticles = articles.Count(a => a.Score > 0);
        var neutralArticles = articles.Count(a => a.Score == 0);
        var badArticles = articles.Count(a => a.Score < 0);

        var totalUpvotes = articles.Sum(a => a.Upvotes);
        var totalDownvotes = articles.Sum(a => a.Downvotes);
        var totalComments = articles.Sum(a => a.CommentCount);

        var sourceStatistics = BuildSourceStatistics(articles);
        var categoryStatistics = BuildCategoryStatistics(articles);
        var dailyStatistics = BuildDailyStatistics(articles);

        var topPositiveSources = sourceStatistics
            .OrderByDescending(s => s.AverageScore)
            .ThenByDescending(s => s.TotalScore)
            .ThenByDescending(s => s.ArticleCount)
            .Take(10)
            .ToList();

        var topNegativeSources = sourceStatistics
            .OrderBy(s => s.AverageScore)
            .ThenBy(s => s.TotalScore)
            .ThenByDescending(s => s.ArticleCount)
            .Take(10)
            .ToList();

        var topPositiveCategories = categoryStatistics
            .OrderByDescending(c => c.AverageScore)
            .ThenByDescending(c => c.TotalScore)
            .ThenByDescending(c => c.ArticleCount)
            .Take(10)
            .ToList();

        var topNegativeCategories = categoryStatistics
            .OrderBy(c => c.AverageScore)
            .ThenBy(c => c.TotalScore)
            .ThenByDescending(c => c.ArticleCount)
            .Take(10)
            .ToList();

        var bestScoredArticle = articles
            .OrderByDescending(a => a.Score)
            .ThenByDescending(a => a.Upvotes)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        var worstScoredArticle = articles
            .OrderBy(a => a.Score)
            .ThenByDescending(a => a.Downvotes)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        var mostLikedArticle = articles
            .OrderByDescending(a => a.Upvotes)
            .ThenByDescending(a => a.Score)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        var mostDislikedArticle = articles
            .OrderByDescending(a => a.Downvotes)
            .ThenBy(a => a.Score)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        var mostCommentedArticle = articles
            .OrderByDescending(a => a.CommentCount)
            .ThenByDescending(a => a.Upvotes + a.Downvotes)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        var response = new StatisticsResponse(
            totalArticles,
            totalScore,
            averageScore,
            goodArticles,
            neutralArticles,
            badArticles,
            totalUpvotes,
            totalDownvotes,
            totalComments,
            topPositiveSources.FirstOrDefault(),
            topNegativeSources.FirstOrDefault(),
            topPositiveSources,
            topNegativeSources,
            topPositiveCategories,
            topNegativeCategories,
            ToArticleResponse(bestScoredArticle),
            ToArticleResponse(worstScoredArticle),
            ToArticleResponse(mostLikedArticle),
            ToArticleResponse(mostDislikedArticle),
            ToArticleResponse(mostCommentedArticle),
            dailyStatistics
        );

        return Ok(response);
    }

    private static List<SourceStatisticResponse> BuildSourceStatistics(List<ArticleStatisticsRow> articles)
    {
        return articles
            .Where(a => !string.IsNullOrWhiteSpace(a.Source))
            .GroupBy(a => a.Source.Trim())
            .Select(group => new SourceStatisticResponse(
                group.Key,
                group.Count(),
                group.Sum(a => a.Score),
                group.Average(a => a.Score),
                group.Count(a => a.Score > 0),
                group.Count(a => a.Score == 0),
                group.Count(a => a.Score < 0),
                group.Sum(a => a.Upvotes),
                group.Sum(a => a.Downvotes),
                group.Sum(a => a.CommentCount)
            ))
            .ToList();
    }

    private static List<CategoryStatisticResponse> BuildCategoryStatistics(List<ArticleStatisticsRow> articles)
    {
        var categoryRows = articles
            .SelectMany(article => ExtractCategories(article)
                .Select(category => new CategoryRow(
                    category,
                    article.Score,
                    article.Upvotes,
                    article.Downvotes,
                    article.CommentCount
                )))
            .ToList();

        return categoryRows
            .GroupBy(row => row.Category)
            .Select(group => new CategoryStatisticResponse(
                group.Key,
                group.Count(),
                group.Sum(x => x.Score),
                group.Average(x => x.Score),
                group.Count(x => x.Score > 0),
                group.Count(x => x.Score == 0),
                group.Count(x => x.Score < 0),
                group.Sum(x => x.Upvotes),
                group.Sum(x => x.Downvotes),
                group.Sum(x => x.CommentCount)
            ))
            .ToList();
    }

    private static List<DailyStatisticResponse> BuildDailyStatistics(List<ArticleStatisticsRow> articles)
    {
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-6);

        var grouped = articles
            .Where(a => a.CreatedAt.Date >= startDate && a.CreatedAt.Date <= today)
            .GroupBy(a => a.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<DailyStatisticResponse>();
        double previousAverageScore = 0;

        for (var date = startDate; date <= today; date = date.AddDays(1))
        {
            grouped.TryGetValue(date, out var dayArticles);
            dayArticles ??= new List<ArticleStatisticsRow>();

            var articleCount = dayArticles.Count;
            var totalScore = dayArticles.Sum(a => a.Score);
            var averageScore = articleCount > 0 ? dayArticles.Average(a => a.Score) : 0;
            var scoreChange = result.Count == 0 ? 0 : averageScore - previousAverageScore;

            result.Add(new DailyStatisticResponse(
                date,
                articleCount,
                totalScore,
                averageScore,
                scoreChange,
                dayArticles.Count(a => a.Score > 0),
                dayArticles.Count(a => a.Score == 0),
                dayArticles.Count(a => a.Score < 0),
                dayArticles.Sum(a => a.Upvotes),
                dayArticles.Sum(a => a.Downvotes),
                dayArticles.Sum(a => a.CommentCount)
            ));

            previousAverageScore = averageScore;
        }

        return result;
    }

    private static ArticleStatisticArticleResponse? ToArticleResponse(ArticleStatisticsRow? article)
    {
        if (article == null)
        {
            return null;
        }

        return new ArticleStatisticArticleResponse(
            article.Id,
            article.Title,
            article.Summary,
            article.Url,
            article.Source,
            article.ImageUrl,
            article.PublishedAt,
            article.CreatedAt,
            article.Score,
            article.KeywordScore,
            article.AiScore,
            DisplayCategory(article),
            ExtractCategories(article),
            article.Tags,
            article.Upvotes,
            article.Downvotes,
            article.CommentCount
        );
    }

    private static string DisplayCategory(ArticleStatisticsRow article)
    {
        if (!string.IsNullOrWhiteSpace(article.Category))
        {
            return article.Category.Trim();
        }

        var categories = ExtractCategories(article);

        if (categories.Count > 0)
        {
            return categories[0];
        }

        return "Allgemein";
    }

    private static List<string> ExtractCategories(ArticleStatisticsRow article)
    {
        var result = new List<string>();

        AddCategory(result, article.Category);

        if (!string.IsNullOrWhiteSpace(article.Categories))
        {
            var raw = article.Categories.Trim();

            if (raw.StartsWith("["))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<string>>(raw);

                    if (parsed != null)
                    {
                        foreach (var category in parsed)
                        {
                            AddCategory(result, category);
                        }
                    }
                }
                catch
                {
                    AddCategoriesFromCommaText(result, raw);
                }
            }
            else
            {
                AddCategoriesFromCommaText(result, raw);
            }
        }

        if (result.Count == 0)
        {
            result.Add("Allgemein");
        }

        return result;
    }

    private static void AddCategoriesFromCommaText(List<string> result, string raw)
    {
        var parts = raw
            .Replace("[", "")
            .Replace("]", "")
            .Replace("\"", "")
            .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            AddCategory(result, part);
        }
    }

    private static void AddCategory(List<string> result, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return;
        }

        var cleaned = category.Trim();

        if (cleaned == "[]" || cleaned == "\"\"" || cleaned == "-")
        {
            return;
        }

        var exists = result.Any(existing =>
            string.Equals(existing, cleaned, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            result.Add(cleaned);
        }
    }

    private sealed class ArticleStatisticsRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Url { get; set; } = "";
        public string Source { get; set; } = "";
        public string? ImageUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public double Score { get; set; }
        public double KeywordScore { get; set; }
        public double AiScore { get; set; }
        public string? Category { get; set; }
        public string? Categories { get; set; }
        public string? Tags { get; set; }
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public int CommentCount { get; set; }
    }

    private sealed record CategoryRow(
        string Category,
        double Score,
        int Upvotes,
        int Downvotes,
        int CommentCount
    );
}

public record StatisticsResponse(
    int TotalArticles,
    double TotalScore,
    double AverageScore,
    int GoodArticles,
    int NeutralArticles,
    int BadArticles,
    int TotalUpvotes,
    int TotalDownvotes,
    int TotalComments,
    SourceStatisticResponse? MostPositiveSource,
    SourceStatisticResponse? MostNegativeSource,
    List<SourceStatisticResponse> TopPositiveSources,
    List<SourceStatisticResponse> TopNegativeSources,
    List<CategoryStatisticResponse> TopPositiveCategories,
    List<CategoryStatisticResponse> TopNegativeCategories,
    ArticleStatisticArticleResponse? BestScoredArticle,
    ArticleStatisticArticleResponse? WorstScoredArticle,
    ArticleStatisticArticleResponse? MostLikedArticle,
    ArticleStatisticArticleResponse? MostDislikedArticle,
    ArticleStatisticArticleResponse? MostCommentedArticle,
    List<DailyStatisticResponse> LastSevenDays
);

public record SourceStatisticResponse(
    string Source,
    int ArticleCount,
    double TotalScore,
    double AverageScore,
    int PositiveArticles,
    int NeutralArticles,
    int NegativeArticles,
    int Upvotes,
    int Downvotes,
    int CommentCount
);

public record CategoryStatisticResponse(
    string Category,
    int ArticleCount,
    double TotalScore,
    double AverageScore,
    int PositiveArticles,
    int NeutralArticles,
    int NegativeArticles,
    int Upvotes,
    int Downvotes,
    int CommentCount
);

public record ArticleStatisticArticleResponse(
    int Id,
    string Title,
    string Summary,
    string Url,
    string Source,
    string? ImageUrl,
    DateTime PublishedAt,
    DateTime CreatedAt,
    double Score,
    double KeywordScore,
    double AiScore,
    string Category,
    List<string> Categories,
    string? Tags,
    int Upvotes,
    int Downvotes,
    int CommentCount
);

public record DailyStatisticResponse(
    DateTime Date,
    int ArticleCount,
    double TotalScore,
    double AverageScore,
    double ScoreChange,
    int GoodArticles,
    int NeutralArticles,
    int BadArticles,
    int Upvotes,
    int Downvotes,
    int CommentCount
);