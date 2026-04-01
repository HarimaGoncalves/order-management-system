using OrderManagement.Domain.Entities;

namespace OrderManagement.Domain.Events;

public class OrderCreatedEvent : DomainEvent
{
    public Guid OrderId { get; }
    public string CustomerName { get; }

    public OrderCreatedEvent(Guid orderId, string customerName)
    {
        OrderId = orderId;
        CustomerName = customerName;
    }
}
