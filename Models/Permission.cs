using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Bus_ticket.Models;

public class Permission
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("name")] [Required] public string Name { get; set; }

    [BsonElement("description")] public string Description { get; set; }

    [BsonElement("link")] [Required] public string Link { get; set; }

    [BsonElement("method")] [Required] public string Method { get; set; }
}