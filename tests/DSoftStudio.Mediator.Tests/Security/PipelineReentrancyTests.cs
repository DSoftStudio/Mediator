// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

// ── Request type unique to reentrancy tests ───────────────────────

public sealed record ReentrantPing(int Depth) : IRequest<int>;

// ── Handler that recurses via the mediator ────────────────────────

public sealed class ReentrantPingHandler : IRequestHandler<ReentrantPing, int>
{
    private readonly IMediator _mediator;

    public ReentrantPingHandler(IMediator mediator) => _mediator = mediator;

    public async ValueTask<int> Handle(ReentrantPing request, CancellationToken cancellationToken)
    {
        if (request.Depth > 0)
            await _mediator.Send(new ReentrantPing(request.Depth - 1), cancellationToken);

        return 1;
    }
}

// ── Behavior that triggers a nested Send mid-pipeline ─────────────

public sealed class ReentrantBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMediator _mediator;

    public ReentrantBehavior(IMediator mediator) => _mediator = mediator;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Trigger a nested mediator call when the outer request is at depth 3.
        if (request is ReentrantPing ping && ping.Depth == 3)
            await _mediator.Send(new ReentrantPing(0), cancellationToken);

        return await next.Handle(request, cancellationToken);
    }
}

// ── Tests ─────────────────────────────────────────────────────────

public class PipelineReentrancyTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public PipelineReentrancyTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ReentrantBehavior<,>));
        services.PrecompilePipelines();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Send_ReentrantPipeline_CompletesWithoutCorruption()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await _mediator.Send(
            new ReentrantPing(5), cts.Token);

        result.ShouldBe(1);
    }

    [Fact]
    public async Task Send_ConcurrentReentrancy_RemainsStable()
    {
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            async (_, ct) =>
            {
                using var scope = _provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new ReentrantPing(3), ct);
                result.ShouldBe(1);
            });
    }
}
