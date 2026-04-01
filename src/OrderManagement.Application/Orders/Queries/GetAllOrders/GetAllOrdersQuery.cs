using MediatR;
using OrderManagement.Application.Common.Interfaces;
using OrderManagement.Application.Orders.DTOs;

namespace OrderManagement.Application.Orders.Queries.GetAllOrders;

public record GetAllOrdersQuery : IRequest<Result<IReadOnlyList<OrderDto>>>;
