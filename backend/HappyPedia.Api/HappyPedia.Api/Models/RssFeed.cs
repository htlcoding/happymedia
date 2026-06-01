namespace HappyPedia.Api.Models;

public class RssFeed
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Url { get; set; } = "";

    public bool IsActive { get; set; } = true;
}