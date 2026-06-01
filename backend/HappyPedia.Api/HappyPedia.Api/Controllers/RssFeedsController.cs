using HappyPedia.Api.Data;
using HappyPedia.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HappyPedia.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/rssfeeds")]
public class RssFeedsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<RssFeedsController> _logger;

    public RssFeedsController(
        AppDbContext db,
        ILogger<RssFeedsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var feeds = await _db.RssFeeds
            .AsNoTracking()
            .OrderBy(f => f.Id)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Url,
                f.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(feeds);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        RssFeed feed,
        CancellationToken cancellationToken)
    {
        var validation = ValidateFeed(feed);

        if (!validation.IsValid)
        {
            return BadRequest(new { message = validation.Error });
        }

        var name = validation.Name!;
        var url = validation.Url!;
        var urlLower = url.ToLower();

        var exists = await _db.RssFeeds
            .AsNoTracking()
            .AnyAsync(f => f.Url.ToLower() == urlLower, cancellationToken);

        if (exists)
        {
            return BadRequest(new { message = "Feed existiert bereits." });
        }

        var newFeed = new RssFeed
        {
            Name = name,
            Url = url,
            IsActive = true
        };

        _db.RssFeeds.Add(newFeed);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RSS-Feed erstellt: {Name} | {Url}", newFeed.Name, newFeed.Url);

        return Ok(new
        {
            newFeed.Id,
            newFeed.Name,
            newFeed.Url,
            newFeed.IsActive
        });
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> Bulk(
        BulkRssFeedRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Feeds == null || request.Feeds.Count == 0)
        {
            return BadRequest(new { message = "Keine Feeds übergeben." });
        }

        var validFeeds = request.Feeds
            .Select(ValidateFeed)
            .Where(x => x.IsValid)
            .Select(x => new
            {
                Name = x.Name!,
                Url = x.Url!,
                UrlLower = x.Url!.ToLower()
            })
            .GroupBy(x => x.UrlLower)
            .Select(g => g.First())
            .ToList();

        if (validFeeds.Count == 0)
        {
            return BadRequest(new { message = "Keine gültigen Feeds gefunden." });
        }

        var urlsLower = validFeeds
            .Select(f => f.UrlLower)
            .ToList();

        var existingUrlsLower = await _db.RssFeeds
            .AsNoTracking()
            .Where(f => urlsLower.Contains(f.Url.ToLower()))
            .Select(f => f.Url.ToLower())
            .ToListAsync(cancellationToken);

        var existingSet = existingUrlsLower.ToHashSet();

        var newFeeds = validFeeds
            .Where(f => !existingSet.Contains(f.UrlLower))
            .Select(f => new RssFeed
            {
                Name = f.Name,
                Url = f.Url,
                IsActive = true
            })
            .ToList();

        if (newFeeds.Count > 0)
        {
            _db.RssFeeds.AddRange(newFeeds);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{Count} RSS-Feeds wurden importiert.", newFeeds.Count);
        }

        var feeds = await _db.RssFeeds
            .AsNoTracking()
            .OrderBy(f => f.Id)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Url,
                f.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            imported = newFeeds.Count,
            skipped = validFeeds.Count - newFeeds.Count,
            feeds
        });
    }

    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> Activate(
        int id,
        CancellationToken cancellationToken)
    {
        var feed = await _db.RssFeeds
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (feed == null)
        {
            return NotFound(new { message = "Feed nicht gefunden." });
        }

        feed.IsActive = true;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RSS-Feed aktiviert: {Id} | {Name}", feed.Id, feed.Name);

        return Ok(new
        {
            feed.Id,
            feed.Name,
            feed.Url,
            feed.IsActive
        });
    }

    [HttpPut("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(
        int id,
        CancellationToken cancellationToken)
    {
        var feed = await _db.RssFeeds
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (feed == null)
        {
            return NotFound(new { message = "Feed nicht gefunden." });
        }

        feed.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RSS-Feed deaktiviert: {Id} | {Name}", feed.Id, feed.Name);

        return Ok(new
        {
            feed.Id,
            feed.Name,
            feed.Url,
            feed.IsActive
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var feed = await _db.RssFeeds
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (feed == null)
        {
            return NotFound(new { message = "Feed nicht gefunden." });
        }

        _db.RssFeeds.Remove(feed);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RSS-Feed gelöscht: {Id} | {Name}", feed.Id, feed.Name);

        return Ok(new
        {
            message = "Feed gelöscht",
            id
        });
    }

    private static FeedValidationResult ValidateFeed(RssFeed? feed)
    {
        if (feed == null)
        {
            return FeedValidationResult.Invalid("Feed darf nicht leer sein.");
        }

        var name = feed.Name?.Trim();
        var url = feed.Url?.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
        {
            return FeedValidationResult.Invalid("Name und URL sind Pflicht.");
        }

        if (name.Length > 150)
        {
            return FeedValidationResult.Invalid("Name darf maximal 150 Zeichen lang sein.");
        }

        if (url.Length > 1000)
        {
            return FeedValidationResult.Invalid("URL darf maximal 1000 Zeichen lang sein.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return FeedValidationResult.Invalid("URL muss eine gültige HTTP- oder HTTPS-Adresse sein.");
        }

        return FeedValidationResult.Valid(name, url);
    }

    private record FeedValidationResult(
        bool IsValid,
        string? Name,
        string? Url,
        string? Error)
    {
        public static FeedValidationResult Valid(string name, string url)
        {
            return new FeedValidationResult(true, name, url, null);
        }

        public static FeedValidationResult Invalid(string error)
        {
            return new FeedValidationResult(false, null, null, error);
        }
    }
}