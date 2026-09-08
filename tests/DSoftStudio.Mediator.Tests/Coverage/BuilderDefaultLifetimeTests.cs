// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Fixtures ──────────────────────────────────────────────────────
// Dependency-free, like the fold fixtures next door: an assembly-wide test resolves every discovered
// handler, so a fixture with an unregistered constructor argument fails somebody else's test.

public sealed record BdlPing : IRequest<int>;

public sealed class BdlPingHandler : IRequestHandler<BdlPing, int>
{
    public ValueTask<int> Handle(BdlPing r, CancellationToken ct) => new(7);
}

public sealed record BdlStream : IStreamRequest<int>;

public sealed class BdlStreamHandler : IStreamRequestHandler<BdlStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        BdlStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return 1;
        await Task.CompletedTask;
    }
}

public sealed class BdlBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(r, ct);
}

public sealed class BdlStreamBehavior : IStreamPipelineBehavior<BdlStream, int>
{
    public IAsyncEnumerable<int> Handle(
        BdlStream request, IStreamRequestHandler<BdlStream, int> next, CancellationToken ct)
        => next.Handle(request, ct);
}

/// <summary>The one component kind that lands on a SINGLE pair, so a chain is built for it alone.</summary>
public sealed class BdlPreProcessor : IRequestPreProcessor<BdlPing>
{
    public ValueTask Process(BdlPing request, CancellationToken ct) => default;
}

public sealed class BdlPostProcessor : IRequestPostProcessor<BdlPing, int>
{
    public ValueTask Process(BdlPing request, int response, CancellationToken ct) => default;
}

public sealed class BdlExceptionHandler : IRequestExceptionHandler<BdlPing, int>
{
    public ValueTask Handle(
        BdlPing request, Exception exception, RequestExceptionHandlerState<int> state, CancellationToken ct)
        => default;
}

// ── Fixtures for the construction-count tests ─────────────────────
// A lifetime is only observable as a COUNT of constructions across dispatches, so these fixtures carry
// state: an instance field the component mutates per dispatch, and a log recording how many instances
// the container built. The log is an instance per container, never a static — the dispatch caches are
// process-global and a sibling fixture must not be able to prime or observe this one.

public sealed class BdlConstructionLog
{
    private int _constructions;
    private int _maxDispatchesPerInstance;

    public int Constructions => Volatile.Read(ref _constructions);

    /// <summary>The most dispatches any ONE instance saw, which is what separates state carried
    /// across dispatches from state a fresh instance resets.</summary>
    public int MaxDispatchesPerInstance => Volatile.Read(ref _maxDispatchesPerInstance);

    public void Constructed() => Interlocked.Increment(ref _constructions);

    public void Saw(int dispatchesByOneInstance)
    {
        int seen;
        while ((seen = Volatile.Read(ref _maxDispatchesPerInstance)) < dispatchesByOneInstance)
        {
            if (Interlocked.CompareExchange(ref _maxDispatchesPerInstance, dispatchesByOneInstance, seen) == seen)
                return;
        }
    }
}

public sealed record BdlStatePing : IRequest<int>;

public sealed class BdlStatePingHandler : IRequestHandler<BdlStatePing, int>
{
    public ValueTask<int> Handle(BdlStatePing r, CancellationToken ct) => new(0);
}

public sealed record BdlFreshPing : IRequest<int>;

public sealed class BdlFreshPingHandler : IRequestHandler<BdlFreshPing, int>
{
    public ValueTask<int> Handle(BdlFreshPing r, CancellationToken ct) => new(0);
}

/// <summary>
/// Holds MUTABLE INSTANCE STATE across the dispatches it sees and reports its own construction count,
/// so the lifetime the builder chose is observable as behaviour rather than as a descriptor.
/// <para>
/// Takes <see cref="IServiceProvider"/> rather than the log directly because
/// <c>ValidateMediatorHandlers()</c> resolves every handler discovered in this assembly, so a fixture
/// with an unregistered constructor argument fails somebody else's test.
/// </para>
/// </summary>
public sealed class BdlStatefulPreProcessor : IRequestPreProcessor<BdlStatePing>
{
    private readonly BdlConstructionLog? _log;
    private int _dispatches;

    public BdlStatefulPreProcessor(IServiceProvider sp)
    {
        _log = sp.GetService<BdlConstructionLog>();
        _log?.Constructed();
    }

    public ValueTask Process(BdlStatePing request, CancellationToken ct)
    {
        _log?.Saw(++_dispatches);
        return default;
    }
}

/// <summary>
/// The same component on its OWN request pair, which keeps the two measurements independently readable.
/// Note what does NOT protect it: a sibling test's <c>AddOpenBehavior</c> latches
/// <c>MarkPipelineChainCacheable</c> for every pair in the assembly, this one included. What makes the
/// measurement below sound is that <c>PipelineChainCache.ResolveSlow</c> asks
/// <c>DispatchCacheability.AllowsCaching</c> per CONTAINER — the static flag no longer decides caching,
/// as PipelineChainCache's own documentation states.
/// </summary>
public sealed class BdlFreshPreProcessor : IRequestPreProcessor<BdlFreshPing>
{
    private readonly BdlConstructionLog? _log;
    private int _dispatches;

    public BdlFreshPreProcessor(IServiceProvider sp)
    {
        _log = sp.GetService<BdlConstructionLog>();
        _log?.Constructed();
    }

    public ValueTask Process(BdlFreshPing request, CancellationToken ct)
    {
        _log?.Saw(++_dispatches);
        return default;
    }
}

// ── Fixtures for the validator tests ──────────────────────────────

public sealed record BdlLatePing : IRequest<int>;

public sealed class BdlLatePingHandler : IRequestHandler<BdlLatePing, int>
{
    public ValueTask<int> Handle(BdlLatePing r, CancellationToken ct) => new(0);
}

public sealed class BdlLatePreProcessor : IRequestPreProcessor<BdlLatePing>
{
    public ValueTask Process(BdlLatePing request, CancellationToken ct) => default;
}

public sealed class BdlLateBehavior : IPipelineBehavior<BdlLatePing, int>
{
    public ValueTask<int> Handle(BdlLatePing r, IRequestHandler<BdlLatePing, int> next, CancellationToken ct)
        => next.Handle(r, ct);
}

// ── Fixtures for the masked-registration test ─────────────────────────────
// Two behaviors on ONE pair, both registered after the scan. IPipelineBehavior is an ENUMERABLE
// service type, so both run -- which is exactly why reading only the last descriptor was the wrong
// question to ask about them.

public sealed record BdlMaskedPing : IRequest<int>;

public sealed class BdlMaskedPingHandler : IRequestHandler<BdlMaskedPing, int>
{
    public ValueTask<int> Handle(BdlMaskedPing r, CancellationToken ct) => new(0);
}

public sealed class BdlMaskedPreProcessor : IRequestPreProcessor<BdlMaskedPing>
{
    public ValueTask Process(BdlMaskedPing request, CancellationToken ct) => default;
}

public sealed class BdlMaskedTransientBehavior : IPipelineBehavior<BdlMaskedPing, int>
{
    public ValueTask<int> Handle(BdlMaskedPing r, IRequestHandler<BdlMaskedPing, int> next, CancellationToken ct)
        => next.Handle(r, ct);
}

public sealed class BdlMaskedScopedBehavior : IPipelineBehavior<BdlMaskedPing, int>
{
    public ValueTask<int> Handle(BdlMaskedPing r, IRequestHandler<BdlMaskedPing, int> next, CancellationToken ct)
        => next.Handle(r, ct);
}

/// <summary>
/// The lifetime a <see cref="MediatorBuilder"/> component gets when the caller states none.
/// <para>
/// It decides the whole application's dispatch cost, not just that component's: the generated
/// <c>RegisterPipeline</c> folds every component lifetime, and ONE Transient component registers the
/// pair's <c>PipelineChainHandler</c> as Transient and skips <c>MarkPipelineChainCacheable</c>, so
/// every dispatch re-resolves and re-links the chain instead of reusing the one already built for the
/// scope. Transient was therefore the wrong default: it put a caller who expressed no opinion on the
/// slow path and gave them nothing for it.
/// </para>
/// <para>
/// Scoped is the longest lifetime that is safe WITHOUT inspecting the component's constructor: a
/// Scoped service may consume Singleton, Scoped and Transient dependencies alike, so no captive
/// dependency is possible, and the chain is only ever resolved through the Scoped <c>IMediator</c>,
/// so it is always resolved inside a scope. Singleton would need the dependency analysis;
/// <c>HandlerLifetimeOptimizer</c> is where that reasoning already lives, for handlers.
/// </para>
/// </summary>
public class BuilderDefaultLifetimeTests
{
    /// <summary>The exact words the generated validator uses for a Transient component its chain
    /// shares. Matching on them is what makes the two tests below about THAT rule and no other.</summary>
    private const string SharedTransientMessage = "constructed once and shared";

    /// <summary>
    /// Every builder method that takes a lifetime, checked at the DESCRIPTOR — no container is built
    /// and no dispatch static is latched, so this pins all five defaults with no blast radius.
    /// </summary>
    [Fact]
    public void Every_component_method_defaults_to_Scoped()
    {
        var services = new ServiceCollection();
        var builder = new MediatorBuilder(services);

        builder.AddOpenBehavior(typeof(BdlBehavior<,>));
        builder.AddStreamBehavior<BdlStreamBehavior>();
        builder.AddRequestPreProcessor<BdlPreProcessor>();
        builder.AddRequestPostProcessor<BdlPostProcessor>();
        builder.AddRequestExceptionHandler<BdlExceptionHandler>();

        services.Count.ShouldBe(5, "each call registers exactly one component descriptor");
        foreach (var descriptor in services)
        {
            descriptor.Lifetime.ShouldBe(
                ServiceLifetime.Scoped,
                $"{descriptor.ServiceType} was registered without a stated lifetime");
        }
    }

    /// <summary>
    /// The end the descriptor assertion stands in for: the pair's chain is cacheable. Uses the
    /// pre-processor because it registers against ONE pair, so the chain built here is this test's.
    /// </summary>
    [Fact]
    public void A_default_component_leaves_the_pipeline_chain_cacheable()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddRequestPreProcessor<BdlPreProcessor>());

        var chain = services.Last(d => d.ServiceType == typeof(PipelineChainHandler<BdlPing, int>));
        chain.Lifetime.ShouldNotBe(
            ServiceLifetime.Transient,
            "a Transient chain is re-resolved and re-linked on every dispatch of this request");

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        DispatchCacheability.AllowsCaching(provider, typeof(PipelineChainHandler<BdlPing, int>))
            .ShouldBeTrue("the dispatch caches ask the container, not the descriptor");
    }

    /// <summary>
    /// The default moved; an EXPLICIT Transient did not. This is the line a promotion scheme would
    /// cross — the call site bakes the optional argument in, so a promotion cannot tell this caller
    /// from one who said nothing, and would silently share an instance the caller asked to be fresh.
    /// </summary>
    [Fact]
    public void An_explicit_Transient_is_still_honoured_end_to_end()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddRequestPreProcessor<BdlPreProcessor>(ServiceLifetime.Transient));

        services.Last(d => d.ServiceType == typeof(IRequestPreProcessor<BdlPing>))
            .Lifetime.ShouldBe(ServiceLifetime.Transient);

        services.Last(d => d.ServiceType == typeof(PipelineChainHandler<BdlPing, int>))
            .Lifetime.ShouldBe(
                ServiceLifetime.Transient,
                "one Transient component still drags the chain down, which is what makes it per dispatch");
    }

    /// <summary>
    /// A Scoped component resolves under <c>ValidateScopes</c> — the ASP.NET Core Development default
    /// — which is the safety claim the flip rests on. The chain is reached only through the Scoped
    /// <c>IMediator</c>, so there is no path that asks the root provider for it.
    /// </summary>
    [Fact]
    public async Task A_default_component_dispatches_under_scope_validation()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddRequestPreProcessor<BdlPreProcessor>());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        (await mediator.Send(new BdlPing(), TestContext.Current.CancellationToken)).ShouldBe(7);
    }

    /// <summary>
    /// The default's whole point, measured rather than inferred: across three dispatches in ONE scope
    /// the container builds the component ONCE, and the chain linked from it is reused. Under a
    /// Transient default it builds three and re-links the chain three times.
    /// <para>
    /// The second assertion is the honest cost of the change, not a bonus: a component with mutable
    /// instance state now carries that state ACROSS dispatches within a scope. That is precisely why
    /// <see cref="ServiceLifetime.Transient"/> remains available and is still honoured.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_default_component_is_constructed_once_per_scope_not_once_per_dispatch()
    {
        var log = new BdlConstructionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator(b => b.AddRequestPreProcessor<BdlStatefulPreProcessor>());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        for (int i = 0; i < 3; i++)
            await mediator.Send(new BdlStatePing(), TestContext.Current.CancellationToken);

        log.Constructions.ShouldBe(1, "a Scoped component is built once for the scope, not once per dispatch");
        log.MaxDispatchesPerInstance.ShouldBe(3, "and that one instance saw every dispatch in the scope");
    }

    /// <summary>
    /// Scoped, not Singleton: a second scope gets its own instance, so nothing a component accumulates
    /// leaks into the next request. This is the bound on how far the default was moved.
    /// </summary>
    [Fact]
    public async Task A_second_scope_gets_its_own_default_component()
    {
        var log = new BdlConstructionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator(b => b.AddRequestPreProcessor<BdlStatefulPreProcessor>());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        for (int s = 0; s < 2; s++)
        {
            using var scope = provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new BdlStatePing(), TestContext.Current.CancellationToken);
            await mediator.Send(new BdlStatePing(), TestContext.Current.CancellationToken);
        }

        log.Constructions.ShouldBe(2, "one instance per scope, over four dispatches");
        log.MaxDispatchesPerInstance.ShouldBe(2, "and no instance outlived its scope to see all four");
    }

    /// <summary>
    /// The escape hatch, measured on the same fixture: a caller who states
    /// <see cref="ServiceLifetime.Transient"/> still gets a fresh instance per dispatch, with its state
    /// reset. Invariant to the default by design — it is the line a lifetime-derivation scheme would
    /// cross, since the call site bakes the optional argument in and such a scheme could not tell this
    /// caller from one who said nothing.
    /// </summary>
    [Fact]
    public async Task An_explicit_Transient_component_is_still_constructed_per_dispatch()
    {
        var log = new BdlConstructionLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator(b => b.AddRequestPreProcessor<BdlFreshPreProcessor>(ServiceLifetime.Transient));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        for (int i = 0; i < 3; i++)
            await mediator.Send(new BdlFreshPing(), TestContext.Current.CancellationToken);

        log.Constructions.ShouldBe(3, "an explicitly Transient component is built for every dispatch");
        log.MaxDispatchesPerInstance.ShouldBe(1, "and no instance carries state into the next dispatch");
    }

    /// <summary>
    /// The library must not throw on its own behaviour. The generated validator reports a Transient
    /// behavior that a non-Transient chain shares; the lifetime the builder now defaults to is Scoped,
    /// so that rule's third conjunct is false and it cannot fire on a default-lifetime component.
    /// Neither the rule nor DSOFT010 was changed to get there.
    /// <para>
    /// Honest about what it discriminates: put the default back to Transient and the line that fails is
    /// the SETUP assertion on chain cacheability, never the closing <c>ShouldNotContain</c>. Given the
    /// preceding assertions the silence is entailed rather than observed, so this test's unique value is
    /// as a guard on the rule's wording, not as a measurement of the default.
    /// </para>
    /// </summary>
    [Fact]
    public void The_validator_says_nothing_about_the_lifetime_the_builder_defaults_to()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddOpenBehavior(typeof(BdlBehavior<,>)));

        using var provider = services.BuildServiceProvider();

        // Non-vacuous: the rule's first two conjuncts hold, so the silence below is its THIRD conjunct
        // answering, not a pair the validator never reached.
        provider.GetService<IPipelineBehavior<BdlPing, int>>().ShouldNotBeNull();
        DispatchCacheability.AllowsCaching(provider, typeof(PipelineChainHandler<BdlPing, int>))
            .ShouldBeTrue("a cacheable chain is the condition the rule keys on");
        DispatchCacheability.AnyTransient(provider, typeof(IPipelineBehavior<BdlPing, int>))
            .ShouldBeFalse("and no registration for it being Transient is what switches the rule off");

        // Other handlers in this assembly need dependencies this minimal container does not register,
        // so validation still reports things — but never the shared-Transient defect.
        var aggregate = Record.Exception(() => provider.ValidateMediatorHandlers()) as AggregateException;
        IEnumerable<Exception> reported = aggregate?.InnerExceptions ?? (IEnumerable<Exception>)[];

        reported.ShouldNotContain(
            e => e.Message.Contains(SharedTransientMessage),
            "the default must not make the library's own registration trip the library's own rule");
    }

    /// <summary>
    /// The other half of the guarantee: a Transient component is never silently shared. Registered
    /// AFTER the scan it misses the fold, the chain stays cacheable, the component really is built once
    /// and shared — and the validator names it, in its own unmodified words.
    /// <para>
    /// The Transient default MASKED this defect: it had already dragged the chain to Transient, so the
    /// late component was per-dispatch by accident of the slow path and the rule stayed quiet.
    /// De-masking it is a behavioural change for an existing application, and belongs in the changelog.
    /// </para>
    /// </summary>
    [Fact]
    public void A_Transient_component_registered_after_the_scan_is_reported()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddRequestPreProcessor<BdlLatePreProcessor>());

        // Too late: the chain's lifetime is already fixed, and it is now cacheable, so this behavior
        // is constructed once with the chain instead of once per dispatch.
#pragma warning disable DSOFT010
        services.AddTransient<IPipelineBehavior<BdlLatePing, int>, BdlLateBehavior>();
#pragma warning restore DSOFT010

        using var provider = services.BuildServiceProvider();

        var error = Should.Throw<AggregateException>(() => provider.ValidateMediatorHandlers());

        error.InnerExceptions.ShouldContain(e =>
            e.Message.Contains(nameof(BdlLatePing)) && e.Message.Contains(SharedTransientMessage));
    }

    /// <summary>
    /// A late Transient behavior stays reported when a LATER non-Transient registration for the same
    /// service type sits behind it.
    /// <para>
    /// The rule used to ask whether the behavior service type was cacheable, and that answer is
    /// last-wins — right for a service the container resolves singly, wrong for
    /// <c>IPipelineBehavior&lt;,&gt;</c>, which is enumerable and runs EVERY descriptor registered for
    /// it. The Scoped registration below hid the Transient one, and the validator went quiet about a
    /// behavior that really was constructed once and shared. It now asks whether ANY registration is
    /// Transient.
    /// </para>
    /// </summary>
    [Fact]
    public void A_late_Transient_behavior_is_reported_even_when_a_later_registration_hides_it()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddRequestPreProcessor<BdlMaskedPreProcessor>());

        // Both after the scan, so neither was folded and the chain stayed cacheable. The Transient one
        // is therefore built once with the chain and shared; the Scoped one is registered LAST.
#pragma warning disable DSOFT010
        services.AddTransient<IPipelineBehavior<BdlMaskedPing, int>, BdlMaskedTransientBehavior>();
        services.AddScoped<IPipelineBehavior<BdlMaskedPing, int>, BdlMaskedScopedBehavior>();
#pragma warning restore DSOFT010

        using var provider = services.BuildServiceProvider();

        // Non-vacuous: the chain really is cacheable, so the rule's first two conjuncts hold and the
        // third is the one being measured.
        DispatchCacheability.AllowsCaching(provider, typeof(PipelineChainHandler<BdlMaskedPing, int>))
            .ShouldBeTrue("the late registrations missed the fold, so the chain kept its lifetime");
        DispatchCacheability.AllowsCaching(provider, typeof(IPipelineBehavior<BdlMaskedPing, int>))
            .ShouldBeTrue("and last-wins reads the Scoped one, which is what used to silence the rule");

        var error = Should.Throw<AggregateException>(() => provider.ValidateMediatorHandlers());

        error.InnerExceptions.ShouldContain(e =>
            e.Message.Contains(nameof(BdlMaskedPing)) && e.Message.Contains(SharedTransientMessage));
    }
}
