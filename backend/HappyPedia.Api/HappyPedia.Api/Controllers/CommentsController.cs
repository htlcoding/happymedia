using HappyPedia.Api.Data;
using HappyPedia.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace HappyPedia.Api.Controllers;

[ApiController]
[Route("api/articles/{articleId:int}/comments")]
public class CommentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(
        AppDbContext db,
        ILogger<CommentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetComments(
        int articleId,
        CancellationToken cancellationToken)
    {
        var articleExists = await _db.Articles
            .AsNoTracking()
            .AnyAsync(a => a.Id == articleId, cancellationToken);

        if (!articleExists)
        {
            return NotFound(new { message = "Artikel nicht gefunden." });
        }

        var comments = await _db.Comments
            .AsNoTracking()
            .Where(c => c.ArticleId == articleId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentResponse(
                c.Id,
                c.ArticleId,
                c.UserId,
                c.User != null ? c.User.Username : "User",
                c.Text,
                c.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Ok(comments);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> AddComment(
        int articleId,
        [FromBody] AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdText, out var userId))
        {
            return Unauthorized(new { message = "Bitte anmelden." });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { message = "Kommentar darf nicht leer sein." });
        }

        var text = request.Text.Trim();

        if (text.Length > 1000)
        {
            return BadRequest(new { message = "Kommentar darf maximal 1000 Zeichen lang sein." });
        }

        var articleExists = await _db.Articles
            .AsNoTracking()
            .AnyAsync(a => a.Id == articleId, cancellationToken);

        if (!articleExists)
        {
            return NotFound(new { message = "Artikel nicht gefunden." });
        }

        var username = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(cancellationToken);

        if (username == null)
        {
            return Unauthorized(new { message = "User nicht gefunden." });
        }

        var comment = new Comment
        {
            ArticleId = articleId,
            UserId = userId,
            Text = text,
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Neuer Kommentar erstellt. ArticleId: {ArticleId}, UserId: {UserId}, CommentId: {CommentId}",
            articleId,
            userId,
            comment.Id);

        var response = new CommentResponse(
            comment.Id,
            comment.ArticleId,
            comment.UserId,
            username,
            comment.Text,
            comment.CreatedAt
        );

        return Created(
            $"api/articles/{articleId}/comments",
            response
        );
    }
}

public record AddCommentRequest(
    [Required]
    [MaxLength(1000)]
    string Text
);

public record CommentResponse(
    int Id,
    int ArticleId,
    int UserId,
    string Username,
    string Text,
    DateTime CreatedAt
);