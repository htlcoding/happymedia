namespace HappyPedia.Api.Models;

public class BulkRssFeedRequest
{
    public List<RssFeed> Feeds { get; set; } = new();
}