using System;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class ExchangeInfo
{
    [BsonElement("exchangedAt")] public DateTime ExchangedAt { get; set; }

    [BsonElement("exchangedSeatNumber")] public string ExchangedSeatNumber { get; set; }

    [BsonElement("newBookingCode")] public string NewBookingCode { get; set; }

    [BsonElement("oldSeatPrice")] public decimal OldSeatPrice { get; set; }

    [BsonElement("penaltyAmount")] public decimal PenaltyAmount { get; set; }

    [BsonElement("newSeatPrice")] public decimal NewSeatPrice { get; set; }

    [BsonElement("amountDue")] public decimal AmountDue { get; set; }
}
