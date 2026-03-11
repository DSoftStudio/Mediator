// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests;

// ── Messages unique to this test ──────────────────────────────────

public record InnerPing : IRequest<int>;

public record OuterPing : IRequest<int>;

// ── Handlers ──────────────────────────────────────────────────────

public sealed class InnerPingHandler : IRequestHandler<InnerPing, int>
{
    public ValueTask<int> Handle(InnerPing request, CancellationToken cancellationToken)
        => new(10);
}

public sealed class OuterPingHandler : IRequestHandler<OuterPing, int>
{
    private readonly IMediator _mediator;

    public OuterPingHandler(IMediator mediator) => _mediator = mediator;

    public async ValueTask<int> Handle(OuterPing request, CancellationToken cancellationToken)
    {
        var inner = await _mediator.Send(new InnerPing(), cancellationToken);
        return inner + 32;
    }
}

// ── Test ──────────────────────────────────────────────────────────

public class NestedSendTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public NestedSendTests()
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
    public async Task Mediator_Should_Support_Nested_Send()
    {
        var result = await _mediator.Send(new OuterPing());

        result.ShouldBe(42);
    }
}
