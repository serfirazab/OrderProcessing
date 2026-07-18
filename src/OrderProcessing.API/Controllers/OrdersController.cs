using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.API.Dtos;
using OrderProcessing.Core.Data;
using OrderProcessing.Core.Models;

namespace OrderProcessing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrderDbContext db, ILogger<OrdersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order. Processes it synchronously and returns the created order.
    /// (Phase 4 will convert this to fully async via Kafka.)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Order), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { Error = "Order must contain at least one item." });

        // Map request to domain model
        var order = new Order
        {
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Items = request.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList(),
            TotalPrice = request.Items.Sum(i => i.UnitPrice * i.Quantity),
            Status = OrderStatus.Pending
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} created with total {TotalPrice:C}", order.Id, order.TotalPrice);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    /// <summary>
    /// Gets a specific order by ID including its line items.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Order>> GetOrder(Guid id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
            return NotFound(new { Error = $"Order {id} not found." });

        return Ok(order);
    }

    /// <summary>
    /// Gets all orders, optionally filtered by status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Order>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders(
        [FromQuery] OrderStatus? status = null)
    {
        var query = _db.Orders.Include(o => o.Items).AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        return Ok(orders);
    }

    /// <summary>
    /// Gets only the status of a specific order (lightweight, used for polling).
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(OrderStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderStatusResponse>> GetOrderStatus(Guid id)
    {
        var order = await _db.Orders
            .Where(o => o.Id == id)
            .Select(o => new OrderStatusResponse
            {
                Id = o.Id,
                Status = o.Status.ToString(),
                TotalPrice = o.TotalPrice,
                ErrorMessage = o.ErrorMessage
            })
            .FirstOrDefaultAsync();

        if (order is null)
            return NotFound(new { Error = $"Order {id} not found." });

        return Ok(order);
    }
}
