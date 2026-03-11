// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Streaming.Application.Streams;

/// <summary>
/// Streams stock price ticks for a given symbol.
/// Simulates a real-time data feed with random price fluctuations.
/// </summary>
public record StockPriceStream(string Symbol) : IStreamRequest<StockTick>;

public record StockTick(string Symbol, decimal Price, DateTime Timestamp);

public sealed class StockPriceStreamHandler : IStreamRequestHandler<StockPriceStream, StockTick>
{
    public async IAsyncEnumerable<StockTick> Handle(
        StockPriceStream request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var random = new Random();
        var price = 100.00m;

        for (var i = 0; i < 20; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simulate real-time price updates
            price += (decimal)(random.NextDouble() * 2 - 1);
            price = Math.Round(price, 2);

            yield return new StockTick(request.Symbol, price, DateTime.UtcNow);

            await Task.Delay(300, cancellationToken);
        }
    }
}
