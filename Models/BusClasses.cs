using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models
{
    public class BusClass
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("name")] 
        public string Name { get; set; } // Ví dụ: "Express", "Luxury", "Volvo A/C"

        [BsonElement("basePrice")] 
        public decimal BasePrice { get; set; } // Giá sàn: 150000, 200000...

        [BsonElement("description")] 
        public string Description { get; set; }
    }
}