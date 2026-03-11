// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

// ── Recursive request with depth tracking ─────────────────────────

public record RecursivePing(int Depth) : IRequest<int>;

public sealed class RecursiveHandler : IRequestHandler<RecursivePing, int>
{
    private readonly IMediator _mediator;

    public RecursiveHandler(IMediator mediator) => _mediator = mediator;

    public async ValueTask<int> Handle(RecursivePing request, CancellationToken cancellationToken)
    {
        if (request.Depth > 0)
            await _mediator.Send(new RecursivePing(request.Depth - 1), cancellationToken);

        return 1;
    }
}

// ── Tests ─────────────────────────────────────────────────────────

public class DeadlockTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public DeadlockTests()
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
    public async Task Send_RecursiveDepth20_CompletesWithoutDeadlock()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await _mediator.Send(
            new RecursivePing(20), cts.Token);

        result.ShouldBe(1);
    }

    [Fact]
    public async Task Send_RecursiveDepth0_CompletesImmediately()
    {
        var result = await _mediator.Send(new RecursivePing(0));

        result.ShouldBe(1);
    }
}
