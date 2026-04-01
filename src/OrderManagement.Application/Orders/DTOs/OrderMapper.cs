using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Orders.DTOs;

public static class OrderMapper
{
    public static OrderDto ToDto(Order order) => new(
        order.Id,
        order.CustomerName,
        order.Status.ToString(),
        order.TotalAmount.Amount,
        order.TotalAmount.Currency,
        order.CreatedAt,
        order.Items.Select(i => new OrderItemDto(
            i.Id,
            i.ProductName,
            i.Quantity,
            i.UnitPrice.Amount,
            i.TotalPrice.Amount)).ToList());
}
