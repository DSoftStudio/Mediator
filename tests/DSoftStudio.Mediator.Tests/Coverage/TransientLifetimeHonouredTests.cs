// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Types owned by this file ────────────────────────────────────────────────

public record LifePing : IRequest<int>;

public record LifeStream : IStreamRequest<int>;

public record LifeNote : INotification;

/// <summary>Counts constructions. An instance per container, never a static: the dispatch caches
/// are process-global and a sibling fixture must not be able to prime or observe this.</summary>
public sealed class LifeCounter
{
    private int _n;
    public int Count => Volatile.Read(ref _n);
    public void Bump() => Interlocked.Increment(ref _n);
}

/// <summary>Stand-in for a per-operation dependency (unit of work, DbContext, typed HttpClient).
/// Registered Transient, so a handler holding one must not outlive a single dispatch.</summary>
public sealed class LifeUnitOfWork
{
    public LifeUnitOfWork(LifeCounter c) => c.Bump();
}

// Every handler below takes IServiceProvider rather than its dependency directly. Two reasons:
// ValidateMediatorHandlers() walks every handler discovered in the assembly and resolves it, so a
// fixture with an unregistered dependency breaks unrelated tests; and an unregistered dependency
// type is exactly what keeps HandlerLifetimeOptimizer from raising the handler above Transient
// ("unknown dependency - stay safe"), which is the path under test.
public sealed class LifePingHandler(IServiceProvider sp) : IRequestHandler<LifePing, int>
{
    private readonly LifeUnitOfWork? _uow = sp.GetService<LifeUnitOfWork>();

    public ValueTask<int> Handle(LifePing request, CancellationToken ct)
        => new(_uow is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_uow));
}

public sealed class LifeStreamHandler(IServiceProvider sp) : IStreamRequestHandler<LifeStream, int>
{
    private readonly LifeCounter? _counter = Bumped(sp.GetService<LifeCounter>());

    private static LifeCounter? Bumped(LifeCounter? c)
    {
        c?.Bump();
        return c;
    }

    public async IAsyncEnumerable<int> Handle(
        LifeStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield return _counter?.Count ?? 0;
    }
}

public sealed class LifeNoteHandler(IServiceProvider sp) : INotificationHandler<LifeNote>
{
    private readonly LifeCounter? _counter = Bumped(sp.GetService<LifeCounter>());

    private static LifeCounter? Bumped(LifeCounter? c)
    {
        c?.Bump();
        return c;
    }

    public Task Handle(LifeNote n, CancellationToken ct)
    {
        _ = _counter;
        return Task.CompletedTask;
    }
}


/// <summary>Counts its own construction, so a pinned chain is observable: reusing the chain reuses
/// the behaviors it was built from.</summary>
public sealed class LifeBehavior(IServiceProvider sp) : IPipelineBehavior<LifePing, int>
{
    private readonly LifeCounter? _counter = Bumped(sp.GetService<LifeCounter>());

    private static LifeCounter? Bumped(LifeCounter? c)
    {
        c?.Bump();
        return c;
    }

    public ValueTask<int> Handle(
        LifePing request,
        IRequestHandler<LifePing, int> next,
        CancellationToken ct)
    {
        _ = _counter;
        return next.Handle(request, ct);
    }
}

// ── Re-entrant publish ───────────────────────────────────────────────

public record ReNote : INotification;

public sealed class ReLog
{
    private int _handled;
    public int Handled => Volatile.Read(ref _handled);
    public void Handled1() => Interlocked.Increment(ref _handled);

    /// <summary>One re-entry only: a bare re-publish from the constructor recurses until the stack dies.</summary>
    public int ReentryBudget = 1;
}

/// <summary>Publishes the same notification from its own constructor, so it re-enters
/// NotificationHandlerCache.Resolve before the outer call has stored anything.</summary>
public sealed class ReHandlerA : INotificationHandler<ReNote>
{
    private readonly ReLog? _log;

    // IServiceProvider, not ReLog: ValidateMediatorHandlers walks and resolves every handler in the
    // assembly, so a fixture with an unregistered dependency breaks unrelated tests.
    public ReHandlerA(IServiceProvider sp)
    {
        _log = sp.GetService<ReLog>();

        if (_log is not null && Interlocked.Decrement(ref _log.ReentryBudget) >= 0)
            sp.GetRequiredService<IPublisher>().Publish(new ReNote(), CancellationToken.None).GetAwaiter().GetResult();
    }

    public Task Handle(ReNote n, CancellationToken ct)
    {
        _log?.Handled1();
        return Task.CompletedTask;
    }
}

public sealed class ReHandlerB(IServiceProvider sp) : INotificationHandler<ReNote>
{
    private readonly ReLog? _log = sp.GetService<ReLog>();

    public Task Handle(ReNote n, CancellationToken ct)
    {
        _log?.Handled1();
        return Task.CompletedTask;
    }
}

/// <summary>
/// A Transient registration must survive dispatch: the provider-keyed <c>[ThreadStatic]</c> caches
/// may reuse an instance only when the container says that instance is reusable.
/// <para>
/// Every one of these caches used to store whatever it resolved, unconditionally, keyed only on the
/// <see cref="IServiceProvider"/> reference. That is right for Singleton and for Scoped (a scope IS
/// a provider), and wrong for Transient: three dispatches on one thread in one scope shared a single
/// instance. The library's own documentation registers a handler WITH dependencies as Transient
/// precisely because "dependencies may be scoped or transient themselves" — the guarantee the cache
/// was quietly removing.
/// </para>
/// </summary>
public class TransientLifetimeHonouredTests
{
    [Fact]
    public async Task Send_HandlerWithTransientDependency_GetsAFreshDependencyPerDispatch()
    {
        // The default path: no manual handler registration. A transient dependency keeps the
        // generated handler registration at Transient (HandlerLifetimeOptimizer declines to raise
        // a handler whose dependency is transient).
        var counter = new LifeCounter();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(counter);
        services.AddTransient<LifeUnitOfWork>();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        services.Last(d => d.ServiceType == typeof(IRequestHandler<LifePing, int>))
            .Lifetime.ShouldBe(ServiceLifetime.Transient);

        // Same thread, same scope — exactly the conditions under which the cache hits.
        var a = await mediator.Send(new LifePing(), TestContext.Current.CancellationToken);
        var b = await mediator.Send(new LifePing(), TestContext.Current.CancellationToken);
        var c = await mediator.Send(new LifePing(), TestContext.Current.CancellationToken);

        counter.Count.ShouldBe(3);
        new[] { a, b, c }.Distinct().Count().ShouldBe(3, "each dispatch must see its own unit of work");
    }

    [Fact]
    public async Task Send_ExplicitlyTransientHandler_IsConstructedPerDispatch()
    {
        var counter = new LifeCounter();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(counter);
        services.AddTransient<LifeUnitOfWork>();
        services.AddTransient<IRequestHandler<LifePing, int>, LifePingHandler>();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Send(new LifePing(), TestContext.Current.CancellationToken);
        await mediator.Send(new LifePing(), TestContext.Current.CancellationToken);
        await mediator.Send(new LifePing(), TestContext.Current.CancellationToken);

        counter.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CreateStream_TransientHandler_IsConstructedPerDispatch()
    {
        var counter = new LifeCounter();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(counter);
        services.AddTransient<IStreamRequestHandler<LifeStream, int>, LifeStreamHandler>();
        services.PrecompileStreams();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        for (int i = 0; i < 3; i++)
        {
            await foreach (var _ in mediator.CreateStream(
                new LifeStream(), TestContext.Current.CancellationToken))
            {
            }
        }

        counter.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Publish_TransientHandler_IsConstructedPerDispatch()
    {
        var counter = new LifeCounter();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(counter);
        services.AddTransient<INotificationHandler<LifeNote>, LifeNoteHandler>();
        services.PrecompileNotifications();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish(new LifeNote(), TestContext.Current.CancellationToken);
        await mediator.Publish(new LifeNote(), TestContext.Current.CancellationToken);
        await mediator.Publish(new LifeNote(), TestContext.Current.CancellationToken);

        counter.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ScopedHandler_IsStillCached_WithinOneScope()
    {
        // The guard must not overreach: Scoped means one instance per scope, and a scope IS the
        // provider the cache keys on, so caching stays correct and must keep working.
        var counter = new LifeCounter();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(counter);
        services.AddScoped<LifeUnitOfWork>();
        services.AddScoped<IRequestHandler<LifePing, int>, LifePingHandler>();

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var a = await mediator.Send(new LifePing(), TestContext.Current.CancellationToken);
        var b = await mediator.Send(new LifePing(), TestContext.Current.CancellationToken);

        counter.Count.ShouldBe(1);
        a.ShouldBe(b);
    }

    [Fact]
    public async Task TransientChain_IsNotCached_EvenAfterAnotherContainerLatchedTheGlobalFlag()
    {
        // The cross-container hazard, end to end. RequestDispatch<,>.IsPipelineChainCacheable is one
        // static per closed pair, so it is PROCESS-global and only ever set to true. Container A
        // registers a Scoped chain and latches it on; container B then registers a genuinely
        // Transient chain for the SAME pair and inherits a flag that describes A, not B.

        // ── Container A: Scoped behavior => Scoped chain => the pair's flag is latched on.
        var servicesA = new ServiceCollection();
        servicesA.AddMediator().RegisterMediatorHandlers();
        servicesA.AddSingleton(new LifeCounter());
        servicesA.AddScoped<IPipelineBehavior<LifePing, int>, LifeBehavior>();
        servicesA.PrecompilePipelines();

        using (var spA = servicesA.BuildServiceProvider())
        {
            await spA.GetRequiredService<IMediator>()
                .Send(new LifePing(), TestContext.Current.CancellationToken);
        }

        RequestDispatch<LifePing, int>.IsPipelineChainCacheable.ShouldBeTrue(
            "container A must have latched the process-global flag - otherwise this test proves nothing");

        // ── Container B: Transient behavior => Transient chain, but the flag already says cacheable.
        var counterB = new LifeCounter();
        var servicesB = new ServiceCollection();
        servicesB.AddMediator().RegisterMediatorHandlers();
        servicesB.AddSingleton(counterB);
        servicesB.AddTransient<IPipelineBehavior<LifePing, int>, LifeBehavior>();
        servicesB.PrecompilePipelines();

        using var spB = servicesB.BuildServiceProvider();
        var mediatorB = spB.GetRequiredService<IMediator>();

        var c1 = spB.GetRequiredService<PipelineChainHandler<LifePing, int>>();
        var c2 = spB.GetRequiredService<PipelineChainHandler<LifePing, int>>();
        c2.ShouldNotBeSameAs(c1, "container B's chain really is Transient in DI");

        int beforeDispatches = counterB.Count;

        await mediatorB.Send(new LifePing(), TestContext.Current.CancellationToken);
        await mediatorB.Send(new LifePing(), TestContext.Current.CancellationToken);
        await mediatorB.Send(new LifePing(), TestContext.Current.CancellationToken);

        (counterB.Count - beforeDispatches).ShouldBe(3,
            "B's chain is Transient, so each dispatch must build its own - A's flag is not B's answer");
    }

    [Fact]
    public async Task Publish_ReenteredFromAHandlerConstructor_DispatchesEachHandlerOncePerPublish()
    {
        // NotificationHandlerCache.Resolve fills its slot only AFTER running the factories, so a
        // handler constructor that publishes the same notification re-enters Resolve while the outer
        // call is still resolving. An audit flagged this as "2N instances constructed, N orphaned".
        //
        // Measured, it is not: the extra instances are exactly the ones the SECOND publish needs.
        // What matters is our contract, not the instance count, which belongs to the container -- for
        // a Scoped registration MS.DI builds one extra handler here, because it is asked for a scoped
        // service that is still inside its own constructor, and that happens with or without us.
        var log = new ReLog();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(log);
        services.AddTransient<INotificationHandler<ReNote>, ReHandlerA>();
        services.AddTransient<INotificationHandler<ReNote>, ReHandlerB>();
        services.PrecompileNotifications();

        using var sp = services.BuildServiceProvider();

        await sp.GetRequiredService<IMediator>().Publish(new ReNote(), TestContext.Current.CancellationToken);

        // Two publishes happened -- the outer one and the one the constructor triggered -- and each
        // must have reached both handlers exactly once. Neither a dropped nor a doubled dispatch.
        log.Handled.ShouldBe(4);
    }
}
