// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Fixtures ──────────────────────────────────────────────────────
// Deliberately dependency-free. An assembly-wide test resolves every discovered handler, so a fixture
// with an unregistered constructor argument fails somebody else's test rather than its own. What these
// pin is the DESCRIPTOR's lifetime, which is the only thing the fold reads.

public sealed record FoldPing : IRequest<int>;

public sealed class FoldPingHandler : IRequestHandler<FoldPing, int>
{
    public ValueTask<int> Handle(FoldPing r, CancellationToken ct) => new(42);
}

public sealed record FoldStream : IStreamRequest<int>;

public sealed class FoldStreamHandler : IStreamRequestHandler<FoldStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        FoldStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return 1;
        await Task.CompletedTask;
    }
}

/// <summary>Singleton when registered, so on its own it never constrains the chain.</summary>
public sealed class FoldSingletonBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(r, ct);
}

public sealed class FoldSingletonStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}

public sealed class FoldObserver : IMediatorDispatchObserver
{
    public bool IsActive => false;

    public IMediatorDispatchScope? BeginDispatch<TRequest, TResponse>(
        TRequest request, IRequestHandler<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
        => null;
}

/// <summary>
/// The pipeline chain's constructor consumes more than the components: it also takes the handler and
/// the registered dispatch observers. Each of those is a chain DEPENDENCY, so each has to constrain the
/// chain's lifetime downwards — otherwise an all-singleton set of components yields a SINGLETON chain
/// that captures something scoped for the life of the process.
/// <para>
/// The request-side handler was folded, with a comment saying why. The dispatch observer and the STREAM
/// handler were not, and both gaps produced the same failure: a singleton chain over a scoped
/// dependency, which <c>ValidateScopes</c> — the ASP.NET Core Development default — rejects, and which
/// with validation off is a silent capture.
/// </para>
/// </summary>
public class ChainLifetimeFoldTests
{
    [Fact]
    public void A_scoped_dispatch_observer_stops_the_request_chain_being_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();

        services.AddScoped<IMediatorDispatchObserver, FoldObserver>();

        // Every COMPONENT is a singleton, so without folding the observer the chain takes the
        // allSingleton branch and becomes a singleton over a scoped dependency.
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(FoldSingletonBehavior<,>));
        services.PrecompilePipelines();

        var chain = services.SingleOrDefault(d => d.ServiceType == typeof(PipelineChainHandler<FoldPing, int>));

        chain.ShouldNotBeNull();
        chain.Lifetime.ShouldNotBe(
            ServiceLifetime.Singleton,
            "a singleton chain would capture the scoped observer for the process lifetime");
    }

    [Fact]
    public void The_request_chain_resolves_under_scope_validation_with_a_scoped_observer()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped<IMediatorDispatchObserver, FoldObserver>();
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(FoldSingletonBehavior<,>));
        services.PrecompilePipelines();

        // The end the descriptor assertion stands in for. Resolving the one chain rather than
        // ValidateOnBuild, because this assembly's other fixtures do not all validate.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<PipelineChainHandler<FoldPing, int>>());
    }

    [Fact]
    public void A_scoped_stream_handler_stops_the_stream_chain_being_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();

        // The handler explicitly Scoped, which is what the fold has to read. The generator's own
        // registration is replaced by this one.
        services.AddScoped<IStreamRequestHandler<FoldStream, int>, FoldStreamHandler>();
        services.AddSingleton(typeof(IStreamPipelineBehavior<,>), typeof(FoldSingletonStreamBehavior<,>));
        services.PrecompileStreams();

        var chain = services.SingleOrDefault(d =>
            d.ServiceType == typeof(StreamPipelineChainHandler<FoldStream, int>));

        chain.ShouldNotBeNull();
        chain.Lifetime.ShouldNotBe(
            ServiceLifetime.Singleton,
            "a singleton stream chain would capture the scoped stream handler for the process lifetime");
    }

    [Fact]
    public void The_stream_chain_resolves_under_scope_validation_with_a_scoped_handler()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped<IStreamRequestHandler<FoldStream, int>, FoldStreamHandler>();
        services.AddSingleton(typeof(IStreamPipelineBehavior<,>), typeof(FoldSingletonStreamBehavior<,>));
        services.PrecompileStreams();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<StreamPipelineChainHandler<FoldStream, int>>());
    }
}

// ── Fixtures: a handler the optimizer deliberately LEAVES Transient ────────────────────────────
// HandlerLifetimeOptimizer promotes a handler to the longest lifetime its constructor allows, and
// stops at Transient when a dependency is transient or unregistered ("a transient dependency keeps
// the handler transient", HandlerLifetimeOptimizer.cs). IServiceProvider is not in the descriptor
// list, so it reads as unregistered and these handlers stay Transient -- which is the state the fold
// below has to respect.

public sealed record FoldTransientPing : IRequest<int>;

public sealed class FoldTransientPingHandler : IRequestHandler<FoldTransientPing, int>
{
    private readonly BdlConstructionLog? _log;
    private int _dispatches;

    public FoldTransientPingHandler(IServiceProvider sp)
    {
        _log = sp.GetService<BdlConstructionLog>();
        _log?.Constructed();
    }

    public ValueTask<int> Handle(FoldTransientPing r, CancellationToken ct)
    {
        _log?.Saw(++_dispatches);
        return new(1);
    }
}

public sealed class FoldScopedPreProcessor : IRequestPreProcessor<FoldTransientPing>
{
    public ValueTask Process(FoldTransientPing request, CancellationToken ct) => default;
}

public sealed record FoldTransientStream : IStreamRequest<int>;

public sealed class FoldTransientStreamHandler : IStreamRequestHandler<FoldTransientStream, int>
{
    private readonly BdlConstructionLog? _log;
    private int _enumerations;

    public FoldTransientStreamHandler(IServiceProvider sp)
    {
        _log = sp.GetService<BdlConstructionLog>();
        _log?.Constructed();
    }

    public async IAsyncEnumerable<int> Handle(
        FoldTransientStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        _log?.Saw(++_enumerations);
        yield return 1;
        await Task.CompletedTask;
    }
}

public sealed class FoldScopedStreamBehavior : IStreamPipelineBehavior<FoldTransientStream, int>
{
    public IAsyncEnumerable<int> Handle(
        FoldTransientStream request, IStreamRequestHandler<FoldTransientStream, int> next, CancellationToken ct)
        => next.Handle(request, ct);
}

/// <summary>
/// The third member of the same family as the two folds above, and the one with teeth: a handler the
/// optimizer left <see cref="ServiceLifetime.Transient"/> ON PURPOSE.
/// <para>
/// <c>HandlerLifetimeOptimizer</c> stops at Transient precisely when the handler has a transient or
/// unregistered dependency -- that is, when a fresh instance per resolve is the whole point. Folding
/// that handler only into <c>allSingleton</c> lands the chain on Scoped, which is cacheable, so the
/// chain constructs the handler ONCE per scope and every dispatch in that scope shares it along with
/// the transient dependency the optimizer just refused to share. A Transient chain dependency has to
/// constrain the chain all the way down to Transient, not merely off Singleton.
/// </para>
/// <para>
/// This was reachable before the builder default moved -- a Scoped companion behavior (FluentValidation
/// registers Scoped) reaches it with no builder component at all. What the default flip changes is that
/// a Transient builder component no longer drags the chain to Transient and stops masking it, so
/// without this fold the flip would make it the ordinary case rather than the corner one.
/// </para>
/// </summary>
public class TransientHandlerFoldTests
{
    [Fact]
    public void A_transient_handler_makes_the_request_chain_transient()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddRequestPreProcessor<FoldScopedPreProcessor>(ServiceLifetime.Scoped));

        services.Last(d => d.ServiceType == typeof(IRequestHandler<FoldTransientPing, int>))
            .Lifetime.ShouldBe(
                ServiceLifetime.Transient,
                "the premise: the optimizer leaves this handler Transient, so the fold has something to read");

        services.Last(d => d.ServiceType == typeof(PipelineChainHandler<FoldTransientPing, int>))
            .Lifetime.ShouldBe(
                ServiceLifetime.Transient,
                "a Scoped chain would construct the Transient handler once and share it for the scope");
    }

    /// <summary>The end the descriptor assertion stands in for, measured as constructions.</summary>
    [Fact]
    public async Task A_transient_handler_is_still_constructed_per_dispatch_behind_a_scoped_component()
    {
        var log = new BdlConstructionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator(b => b.AddRequestPreProcessor<FoldScopedPreProcessor>(ServiceLifetime.Scoped));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        for (int i = 0; i < 3; i++)
            await mediator.Send(new FoldTransientPing(), TestContext.Current.CancellationToken);

        log.Constructions.ShouldBe(3, "the handler asked to be Transient and three dispatches went through it");
        log.MaxDispatchesPerInstance.ShouldBe(1, "no instance may carry state into the next dispatch");
    }

    /// <summary>
    /// The documented override — a plain <c>Add</c> after <c>RegisterMediatorHandlers()</c> — decides the
    /// chain, because <c>IRequestHandler&lt;,&gt;</c> resolves SINGLE and the container hands the chain
    /// the LAST descriptor.
    /// <para>
    /// <c>HandlerLifetimeOptimizer</c> deliberately leaves the generator's own Transient descriptor in
    /// place once a user registration appends one, so the collection holds both. Reading every descriptor
    /// rather than the winner made this override yield a Transient, uncached chain for a handler the
    /// container never builds — the pipeline components keep that OR, because they are ENUMERABLE and
    /// every descriptor for them really does run.
    /// </para>
    /// </summary>
    [Fact]
    public void An_overridden_handler_decides_the_chain_not_the_superseded_descriptor()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped<IRequestPreProcessor<FoldTransientPing>, FoldScopedPreProcessor>();

        // Both descriptors now exist for this handler: the generator's Transient and this Scoped one.
        services.AddScoped<IRequestHandler<FoldTransientPing, int>, FoldTransientPingHandler>();
        services.PrecompilePipelines();

        services.Count(d => d.ServiceType == typeof(IRequestHandler<FoldTransientPing, int>))
            .ShouldBeGreaterThan(1, "the premise: the superseded descriptor is still in the collection");

        services.Last(d => d.ServiceType == typeof(PipelineChainHandler<FoldTransientPing, int>))
            .Lifetime.ShouldBe(
                ServiceLifetime.Scoped,
                "the container resolves the Scoped override, so the chain may be cached per scope");
    }

    [Fact]
    public void A_transient_stream_handler_makes_the_stream_chain_transient()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped<IStreamPipelineBehavior<FoldTransientStream, int>, FoldScopedStreamBehavior>();
        services.PrecompileStreams();

        services.Last(d => d.ServiceType == typeof(IStreamRequestHandler<FoldTransientStream, int>))
            .Lifetime.ShouldBe(ServiceLifetime.Transient, "the premise, on the stream side");

        services.Last(d => d.ServiceType == typeof(StreamPipelineChainHandler<FoldTransientStream, int>))
            .Lifetime.ShouldBe(
                ServiceLifetime.Transient,
                "a Scoped stream chain would construct the Transient stream handler once per scope");
    }

    [Fact]
    public async Task A_transient_stream_handler_is_still_constructed_per_enumeration()
    {
        var log = new BdlConstructionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped<IStreamPipelineBehavior<FoldTransientStream, int>, FoldScopedStreamBehavior>();
        services.PrecompileStreams();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        for (int i = 0; i < 3; i++)
        {
            await foreach (var _ in mediator.CreateStream(
                new FoldTransientStream(), TestContext.Current.CancellationToken))
            {
            }
        }

        log.Constructions.ShouldBe(3, "the stream handler asked to be Transient");
        log.MaxDispatchesPerInstance.ShouldBe(1, "no instance may carry state into the next enumeration");
    }
}
