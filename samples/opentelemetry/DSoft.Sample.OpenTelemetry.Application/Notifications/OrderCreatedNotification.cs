// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.OpenTelemetry.Application.Notifications;

public record OrderCreatedNotification(Guid OrderId, string Product) : INotification;

public sealed class SendConfirmationEmail : INotificationHandler<OrderCreatedNotification>
{
    public async Task Handle(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        await Task.Delay(15, cancellationToken);
        Console.WriteLine($"  → Email sent for order {notification.OrderId}");
    }
}

public sealed class UpdateInventory : INotificationHandler<OrderCreatedNotification>
{
    public async Task Handle(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        await Task.Delay(5, cancellationToken);
        Console.WriteLine($"  → Inventory updated for {notification.Product}");
    }
}
