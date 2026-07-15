using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Bus_ticket.Models
{
    public class News
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? OriginalUrl { get; set; }
        public string? SourceSite { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsCrawled { get; set; } = false;
        public bool IsApproved { get; set; } = false;
        public bool IsCloned { get; set; } = false;
        public string? TitleXpath { get; set; }
        public string? DescXpath { get; set; }
        public string? ContentXpath { get; set; }
        public string? ThumbXpath { get; set; }
    }
}