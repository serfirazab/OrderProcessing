using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Core.Models;

/// <summary>
/// Represents a single product line item within an order.
/// </summary>
public class OrderItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    [Required, MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than zero.")]
    public decimal UnitPrice { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }

    /// <summary>
    /// Computed subtotal for this line item (UnitPrice × Quantity).
    /// </summary>
    public decimal SubTotal => UnitPrice * Quantity;
}
