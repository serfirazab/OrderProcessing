using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderProcessing.Core.Models;

/// <summary>
/// Represents a customer order with its full lifecycle status.
/// </summary>
public class Order
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    [EmailAddress(ErrorMessage = "Invalid customer email address.")]
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// Order line items.
    /// </summary>
    public List<OrderItem> Items { get; set; } = [];

    /// <summary>
    /// Total price of all items in the order. Calculated during processing.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// Current status in the processing pipeline.
    /// </summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>
    /// Error message if the order processing failed. Null otherwise.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when the order was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the order was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
