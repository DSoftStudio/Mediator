// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
namespace DSoftStudio.Mediator.Tests.Security;

// ── Request / handler unique to GC leak tests ─────────────────────

public sealed record MemoryPing : IRequest<int>;

public sealed class MemoryPingHandler : IRequestHandler<MemoryPing, int>
{
    /// <summary>
    /// Armed only for the retention test, and null otherwise, so an ordinary dispatch pays a single
    /// static read. Every constructed handler adds itself, which is what lets the test assert both
    /// how many were built and whether any survived.
    /// </summary>
    internal static List<WeakReference>? Instances;

    public MemoryPingHandler()
    {
        var sink = Instances;
        if (sink is not null)
        {
            lock (sink)
                sink.Add(new WeakReference(this));
        }
    }

    public ValueTask<int> Handle(MemoryPing request, CancellationToken cancellationToken)
        => new(1);
}

// ── Tests ─────────────────────────────────────────────────────────

public class PipelineGcLeakTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public PipelineGcLeakTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers();

        // Override the auto-Singleton with Transient so the GC test can verify
        // that non-singleton handlers are collected after scope disposal.
        services.AddTransient<IRequestHandler<MemoryPing, int>, MemoryPingHandler>();

        services.PrecompilePipelines();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        MemoryPingHandler.Instances = null;
        _provider.Dispose();
    }

    /// <summary>
    /// A Transient handler must be constructed once per dispatch and retained by nothing afterwards.
    /// <para>
    /// This used to dispatch a million requests and compare <c>GC.GetTotalMemory(true)</c> across two
    /// identical rounds. That instrument does not work here, for two separate reasons. It reads the
    /// whole PROCESS heap, which the other 500-odd tests are mutating in parallel — measured swings of
    /// tens of megabytes against a one-megabyte tolerance, which is where the intermittent failures
    /// came from. And comparing two identical rounds cancels any retention that is CONSTANT per
    /// dispatch, which is precisely the shape of both bugs this file exists to guard: pinning one
    /// handler, or holding one disposed scope. Reverting either fix left the old assertion passing.
    /// </para>
    /// <para>
    /// Liveness measures retention directly instead, and is immune to what other tests allocate: no
    /// other thread can make THESE weak references reachable. Both assertions are load-bearing — see
    /// the comments on each.
    /// </para>
    /// <para>
    /// What this test uniquely covers is ACCUMULATION PER DISPATCH — something holding one more
    /// reference for every request that goes through. Nothing else in the suite sees that shape:
    /// <c>TransientLifetimeHonouredTests.Send_ExplicitlyTransientHandler_IsConstructedPerDispatch</c>
    /// dispatches three times, and <c>ScopeReleasedOnDisposeTests</c> tracks a single instance. Those
    /// two are what actually guard the pinning and disposed-scope regressions; do not fold this one
    /// into them, and do not shrink the dispatch count to a handful.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Send_ManyRequests_ConstructsAHandlerPerDispatch_AndRetainsNone()
    {
        const int dispatches = 1_000;

        var tracked = await DispatchAndTrackHandlers(dispatches);

        FullGc();

        // The COUNT catches pinning (the bug fixed in 7d0fac7): a cache that ignores the registered
        // Transient lifetime constructs ONE handler and reuses it, so this reads 1 instead of N.
        // Note there is deliberately no warm-up before tracking is armed — a warm-up would move that
        // single construction outside the window, leaving the set empty and making the liveness
        // assertion below true for the wrong reason.
        tracked.Count.ShouldBe(dispatches);

        // The LIVENESS catches retention: anything on the dispatch path still holding a handler it
        // built keeps it reachable across a full collection.
        tracked.Count(reference => reference.IsAlive).ShouldBe(0);
    }

    /// <summary>
    /// Dispatches in its own <c>NoInlining</c> frame: in a Debug build the JIT keeps locals alive to
    /// the end of the enclosing method, so tracking in the test body would report the test's own frame
    /// as retention.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<List<WeakReference>> DispatchAndTrackHandlers(int dispatches)
    {
        var sink = new List<WeakReference>(dispatches);
        MemoryPingHandler.Instances = sink;

        try
        {
            for (int i = 0; i < dispatches; i++)
                await _mediator.Send(new MemoryPing(), TestContext.Current.CancellationToken);
        }
        finally
        {
            MemoryPingHandler.Instances = null;
        }

        return sink;
    }

    [Fact]
    public async Task Send_ScopedExecution_HandlerIsCollectedAfterDisposal()
    {
        await _mediator.Send(new MemoryPing(), TestContext.Current.CancellationToken);

        var weakRef = ResolveSendAndTrackHandler();

        FullGc();

        Assert.False(
            weakRef.IsAlive,
            "transient handler should be eligible for GC after scope disposal"
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference ResolveSendAndTrackHandler()
    {
        using var scope = _provider.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<MemoryPing, int>>();

        mediator.Send(new MemoryPing()).GetAwaiter().GetResult();

        return new WeakReference(handler);
    }

    private static void FullGc()
    {
        // Forced, blocking and compacting, three times. The parameterless GC.Collect() can be
        // satisfied by a BACKGROUND collection, which under load from another test assembly running
        // in parallel leaves objects uncollected — and the liveness assertion below then measures how
        // busy the machine is rather than what the dispatch path retains. Repeating is not retrying
        // an assertion: something genuinely rooted survives any number of collections, so a pass here
        // still means unreachable.
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }
}
