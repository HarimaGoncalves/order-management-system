using MediatR;
using OrderManagement.Application.Common.Interfaces;
using OrderManagement.Application.Orders.DTOs;

namespace OrderManagement.Application.Orders.Commands.CreateOrder;

public record CreateOrderCommand : IRequest<Result<OrderDto>>
{
    public string CustomerName { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string ZipCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public List<CreateOrderItemDto> Items { get; init; } = [];
}

public record CreateOrderItemDto(string ProductName, int Quantity, decimal UnitPrice);
