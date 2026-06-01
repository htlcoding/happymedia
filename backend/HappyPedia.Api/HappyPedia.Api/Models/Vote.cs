namespace HappyPedia.Api.Models;

public class Vote
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser? AppUser { get; set; }

    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    public bool IsLike { get; set; }
}