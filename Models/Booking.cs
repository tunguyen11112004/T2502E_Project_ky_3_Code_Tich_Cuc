using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class PassengerDetail
{
    [BsonElement("seatNumber")] public string SeatNumber { get; set; }

    [BsonElement("name")] public string Name { get; set; }

    [BsonElement("phoneNumber")] public string PhoneNumber { get; set; }

    [BsonElement("email")] public string Email { get; set; }

    [BsonElement("dob")] public DateTime Dob { get; set; }

    [BsonElement("finalSeatPrice")] public decimal FinalSeatPrice { get; set; }
}

public class PaymentInfo
{
    [BsonElement("paymentMethod")] public string PaymentMethod { get; set; } // Cash, Banking

    [BsonElement("amountPaid")] public decimal AmountPaid { get; set; }

    [BsonElement("transactionCode")] public string TransactionCode { get; set; }
}

public class CancellationInfo
{
    [BsonElement("canceledAt")] public DateTime CanceledAt { get; set; }

    [BsonElement("reason")] public string Reason { get; set; }

    [BsonElement("penaltyPercentage")] public decimal PenaltyPercentage { get; set; }

    [BsonElement("refundAmount")] public decimal RefundAmount { get; set; }
    
    [BsonElement("refundMethod")] 
    [BsonIgnoreIfNull]
    public string RefundMethod { get; set; } // "Cash" hoặc "Online"

    [BsonElement("refundBankName")] 
    [BsonIgnoreIfNull]
    public string RefundBankName { get; set; }

    [BsonElement("refundAccountNo")] 
    [BsonIgnoreIfNull]
    public string RefundAccountNo { get; set; }

    [BsonElement("refundAccountName")] 
    [BsonIgnoreIfNull]
    public string RefundAccountName { get; set; }
}

public class Booking
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("bookingCode")] public string BookingCode { get; set; }

    [BsonElement("customerId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string CustomerId { get; set; }

    [BsonElement("customerPhone")] public string CustomerPhone { get; set; }

    [BsonElement("customerEmail")] public string CustomerEmail { get; set; }

    [BsonElement("tripId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string TripId { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; }

    [BsonElement("branchId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string BranchId { get; set; }

    [BsonElement("bookingTime")] public DateTime BookingTime { get; set; } = DateTime.UtcNow;

    [BsonElement("totalPrice")] public decimal TotalPrice { get; set; }

    [BsonElement("taxAmount")] public decimal TaxAmount { get; set; }

    [BsonElement("discountAmount")] public decimal DiscountAmount { get; set; }

    [BsonElement("finalAmount")] public decimal FinalAmount { get; set; }

    [BsonElement("bookingStatus")] public string BookingStatus { get; set; } = "Reserved";

    [BsonElement("paymentStatus")] public string PaymentStatus { get; set; } = "Pending";

    [BsonElement("passengers")] public List<PassengerDetail> Passengers { get; set; } = new List<PassengerDetail>();

    [BsonElement("paymentInfo")] public PaymentInfo Payment { get; set; }

    [BsonElement("cancellation")] public CancellationInfo Cancellation { get; set; }

    // Đã thêm cờ đánh dấu vé đang chờ hoàn tiền (Dùng cho Task 14 - Cancel Trip)
    [BsonElement("isRefundPending")] public bool IsRefundPending { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; }
}