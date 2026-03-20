// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace Host.Application.Services;

/// <summary>
/// A service that depends on <see cref="ISender"/> to send commands and queries.
/// This is the "system under test" that <c>Host.Tests</c> will exercise with mocks.
/// </summary>
public sealed class OrderService(ISender sender)
{
    public async Task<int> PlaceOrderAsync(string productName, int quantity, CancellationToken ct = default)
    {
        var orderId = await sender.Send<Commands.CreateOrderCommand, int>(
            new Commands.CreateOrderCommand(productName, quantity), ct);

        return orderId;
    }

    public async Task<string> GetOrderSummaryAsync(int orderId, CancellationToken ct = default)
    {
        var summary = await sender.Send<Queries.GetOrderQuery, string>(
            new Queries.GetOrderQuery(orderId), ct);

        return summary;
    }
}
