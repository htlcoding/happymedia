namespace HappyPedia.Api.Models;

public class ArticleVote
{
    public int Id { get; set; }

    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public bool IsLike { get; set; }
}