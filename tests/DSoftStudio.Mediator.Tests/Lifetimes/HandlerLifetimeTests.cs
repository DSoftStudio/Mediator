// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Lifetimes;

// ── Unique request types per test to avoid cross-test interference ──

public sealed record ScopedPing : IRequest<int>;
public sealed record SingletonPing : IRequest<int>;

// ── Handlers that track identity ────────────────────────────────────

public sealed class ScopedPingHandler : IRequestHandler<ScopedPing, int>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public ValueTask<int> Handle(ScopedPing request, CancellationToken ct) => new(42);
}

public sealed class SingletonPingHandler : IRequestHandler<SingletonPing, int>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public ValueTask<int> Handle(SingletonPing request, CancellationToken ct) => new(42);
}

// ── Tests ───────────────────────────────────────────────────────────

public class HandlerLifetimeTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public HandlerLifetimeTests()
    {
        var services = new ServiceCollection();
        services
            .AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        // Override the generated Transient with Scoped/Singleton AFTER RegisterMediatorHandlers()
        services.AddScoped<IRequestHandler<ScopedPing, int>, ScopedPingHandler>();
        services.AddSingleton<IRequestHandler<SingletonPing, int>, SingletonPingHandler>();

        _provider = services.BuildServiceProvider();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task ScopedHandler_SameInstanceWithinScope_DifferentAcrossScopes()
    {
        Guid id1, id2, id3;

        // Scope 1: two Send calls should get the SAME handler instance
        using (var scope = _provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new ScopedPing());
            await mediator.Send(new ScopedPing());

            id1 = scope.ServiceProvider.GetRequiredService<IRequestHandler<ScopedPing, int>>()
                is ScopedPingHandler h1 ? h1.InstanceId : Guid.Empty;
            id2 = scope.ServiceProvider.GetRequiredService<IRequestHandler<ScopedPing, int>>()
                is ScopedPingHandler h2 ? h2.InstanceId : Guid.Empty;
        }

        // Scope 2: should get a DIFFERENT handler instance
        using (var scope = _provider.CreateScope())
        {
            id3 = scope.ServiceProvider.GetRequiredService<IRequestHandler<ScopedPing, int>>()
                is ScopedPingHandler h3 ? h3.InstanceId : Guid.Empty;
        }

        id1.ShouldBe(id2, "same scope → same Scoped handler");
        id1.ShouldNotBe(id3, "different scope → different Scoped handler");
    }

    [Fact]
    public async Task SingletonHandler_SameInstanceEverywhere()
    {
        Guid id1, id2;

        using (var scope1 = _provider.CreateScope())
        {
            var mediator = scope1.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new SingletonPing());

            id1 = scope1.ServiceProvider.GetRequiredService<IRequestHandler<SingletonPing, int>>()
                is SingletonPingHandler h1 ? h1.InstanceId : Guid.Empty;
        }

        using (var scope2 = _provider.CreateScope())
        {
            id2 = scope2.ServiceProvider.GetRequiredService<IRequestHandler<SingletonPing, int>>()
                is SingletonPingHandler h2 ? h2.InstanceId : Guid.Empty;
        }

        id1.ShouldBe(id2, "Singleton → same instance across all scopes");
    }
}
