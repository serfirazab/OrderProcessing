using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.API.Dtos;
using OrderProcessing.API.Services;
using OrderProcessing.Core.Data;
using OrderProcessing.Core.Models;

namespace OrderProcessing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly OrderPublisherService _publisher;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrderDbContext db,
        OrderPublisherService publisher,
        ILogger<OrdersController> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order asynchronously.
    /// Validates only the input shape, saves as Pending, publishes to Kafka,
    /// and returns 202 Accepted. The OrderProcessor worker handles the rest.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { Error = "Order must contain at least one item." });

        if (string.IsNullOrWhiteSpace(request.CustomerEmail) || !request.CustomerEmail.Contains('@'))
            return BadRequest(new { Error = "A valid customer email is required." });

        // Map request to domain model — no calculation, no validation
        // OrderProcessor worker will validate and calculate asynchronously
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
            TotalPrice = 0, // Will be calculated by OrderProcessor
            Status = OrderStatus.Pending
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Publish to Kafka — OrderProcessor handles validation and calculation async
        await _publisher.PublishOrderAsync(order);

        _logger.LogInformation("Order {OrderId} submitted for async processing", order.Id);

        // Return 202 Accepted with tracking URL
        return AcceptedAtAction(nameof(GetOrder), new { id = order.Id }, new
        {
            order.Id,
            order.Status,
            Message = "Order submitted for processing. Use the tracking URL to check status.",
            TrackingUrl = Url.Action(nameof(GetOrder), null, new { id = order.Id }, Request.Scheme)
        });
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
