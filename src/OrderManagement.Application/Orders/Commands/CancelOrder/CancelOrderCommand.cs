using MediatR;
using OrderManagement.Application.Common.Interfaces;

namespace OrderManagement.Application.Orders.Commands.CancelOrder;

public record CancelOrderCommand(Guid OrderId) : IRequest<Result<bool>>;
