using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models
{
    public class RefundRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string BookingCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal RefundAmount { get; set; } // Giá đã trừ
        public string Reason { get; set; }
        public string RefundMethod { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } // "Pending" hoặc "Completed"
        public DateTime? ProcessedAt { get; set; }
        public string ProcessedBy { get; set; }
    }
}