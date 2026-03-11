// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

// ── Request / handler unique to lock-contention tests ─────────────

public sealed record ConcurrencyPing : IRequest<int>;

public sealed class ConcurrencyPingHandler : IRequestHandler<ConcurrencyPing, int>
{
    public ValueTask<int> Handle(ConcurrencyPing request, CancellationToken cancellationToken)
        => new(1);
}

// ── Tests ─────────────────────────────────────────────────────────

public class LockContentionTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public LockContentionTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Send_HighConcurrency_NoExceptionsAndCorrectResults()
    {
        const int concurrency = 1000;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, concurrency),
            async (_, ct) =>
            {
                using var scope = _provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new ConcurrencyPing(), ct);
                result.ShouldBe(1);
            });
    }

    [Fact]
    public async Task Send_RepeatedBurstLoad_NoLockContentionSpikes()
    {
        for (int i = 0; i < 50; i++)
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, 500),
                async (_, ct) =>
                {
                    using var scope = _provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var result = await mediator.Send(new ConcurrencyPing(), ct);
                    result.ShouldBe(1);
                });
        }
    }
}
