using HappyPedia.Api.Data;
using HappyPedia.Api.Models;
using HappyPedia.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HappyPedia.Api.Controllers;

[ApiController]
[Route("api/articles")]
public class ArticlesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RssImportService _rssImportService;
    private readonly ILogger<ArticlesController> _logger;

    public ArticlesController(
        AppDbContext db,
        RssImportService rssImportService,
        ILogger<ArticlesController> logger)
    {
        _db = db;
        _rssImportService = rssImportService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetArticles(CancellationToken cancellationToken)
    {
        var articles = await _db.Articles
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ArticleListResponse(
                a.Id,
                a.Title,
                a.Summary,
                a.Url,
                a.Source,
                a.ImageUrl,
                a.PublishedAt,
                a.CreatedAt,
                a.Score,
                a.KeywordScore,
                a.AiScore,
                a.Category ?? "Allgemein",
                a.Categories ?? "[]",
                a.Tags,
                a.Upvotes,
                a.Downvotes,
                _db.Comments.Count(c => c.ArticleId == a.Id)
            ))
            .ToListAsync(cancellationToken);

        return Ok(articles);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var article = await _db.Articles
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new ArticleDetailResponse(
                a.Id,
                a.Title,
                a.Summary,
                a.Content,
                a.Url,
                a.Source,
                a.ImageUrl,
                a.PublishedAt,
                a.CreatedAt,
                a.Score,
                a.KeywordScore,
                a.AiScore,
                a.Category ?? "Allgemein",
                a.Categories ?? "[]",
                a.Tags,
                a.Upvotes,
                a.Downvotes,
                _db.Comments.Count(c => c.ArticleId == a.Id)
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (article == null)
        {
            return NotFound(new { message = "Artikel nicht gefunden." });
        }

        return Ok(article);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("fetch")]
    public async Task<IActionResult> FetchArticles(CancellationToken cancellationToken)
    {
        var imported = await _rssImportService.ImportAllFeeds(cancellationToken);

        _logger.LogInformation("{Count} Artikel wurden importiert.", imported);

        return Ok(new
        {
            message = "RSS-Import abgeschlossen.",
            imported
        });
    }

    [Authorize]
    [HttpPost("{id:int}/like")]
    public async Task<IActionResult> LikeArticle(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new { message = "Bitte anmelden." });
        }

        var article = await _db.Articles
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article == null)
        {
            return NotFound(new { message = "Artikel nicht gefunden." });
        }

        var existingVote = await _db.Votes
            .FirstOrDefaultAsync(
                v => v.ArticleId == id && v.UserId == userId.Value,
                cancellationToken);

        if (existingVote == null)
        {
            var vote = new ArticleVote
            {
                ArticleId = id,
                UserId = userId.Value,
                IsLike = true
            };

            _db.Votes.Add(vote);

            article.Upvotes++;
            article.Score += 1;
        }
        else if (!existingVote.IsLike)
        {
            existingVote.IsLike = true;

            article.Upvotes++;
            article.Downvotes = Math.Max(0, article.Downvotes - 1);

            article.Score += 2;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ArticleVoteResponse(
            article.Id,
            article.Upvotes,
            article.Downvotes,
            article.Score
        ));
    }

    [Authorize]
    [HttpPost("{id:int}/dislike")]
    public async Task<IActionResult> DislikeArticle(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new { message = "Bitte anmelden." });
        }

        var article = await _db.Articles
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article == null)
        {
            return NotFound(new { message = "Artikel nicht gefunden." });
        }

        var existingVote = await _db.Votes
            .FirstOrDefaultAsync(
                v => v.ArticleId == id && v.UserId == userId.Value,
                cancellationToken);

        if (existingVote == null)
        {
            var vote = new ArticleVote
            {
                ArticleId = id,
                UserId = userId.Value,
                IsLike = false
            };

            _db.Votes.Add(vote);

            article.Downvotes++;
            article.Score -= 1;
        }
        else if (existingVote.IsLike)
        {
            existingVote.IsLike = false;

            article.Downvotes++;
            article.Upvotes = Math.Max(0, article.Upvotes - 1);

            article.Score -= 2;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ArticleVoteResponse(
            article.Id,
            article.Upvotes,
            article.Downvotes,
            article.Score
        ));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteArticle(
        int id,
        CancellationToken cancellationToken)
    {
        var article = await _db.Articles
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article == null)
        {
            return NotFound(new { message = "Artikel nicht gefunden." });
        }

        _db.Articles.Remove(article);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Artikel gelöscht: {Id} | {Title}", article.Id, article.Title);

        return Ok(new
        {
            message = "Artikel gelöscht.",
            id
        });
    }

    private int? GetCurrentUserId()
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdText, out var userId))
        {
            return null;
        }

        return userId;
    }
}

public record ArticleListResponse(
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
    string Categories,
    string? Tags,
    int Upvotes,
    int Downvotes,
    int CommentCount
);

public record ArticleDetailResponse(
    int Id,
    string Title,
    string Summary,
    string Content,
    string Url,
    string Source,
    string? ImageUrl,
    DateTime PublishedAt,
    DateTime CreatedAt,
    double Score,
    double KeywordScore,
    double AiScore,
    string Category,
    string Categories,
    string? Tags,
    int Upvotes,
    int Downvotes,
    int CommentCount
);

public record ArticleVoteResponse(
    int ArticleId,
    int Upvotes,
    int Downvotes,
    double Score
);