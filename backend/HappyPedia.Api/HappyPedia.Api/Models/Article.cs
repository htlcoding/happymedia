namespace HappyPedia.Api.Models;

public class Article
{
    public int Id { get; set; }

    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Summary { get; set; } = "";

    public string Url { get; set; } = "";
    public string Source { get; set; } = "";

    public string? ImageUrl { get; set; }

    public double Score { get; set; }
    public double KeywordScore { get; set; }
    public double AiScore { get; set; }

    public string Category { get; set; } = "Allgemein";

    public string Categories { get; set; } = "[]";

    public string? Tags { get; set; }

    public int Upvotes { get; set; }
    public int Downvotes { get; set; }

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}