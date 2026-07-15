using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class News
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? OriginalUrl { get; set; }
    public string? SourceSite { get; set; }
    public bool IsApproved { get; set; } = false; //Mặc định khi crawl về sẽ là FALSE (Lưu Nháp)
}