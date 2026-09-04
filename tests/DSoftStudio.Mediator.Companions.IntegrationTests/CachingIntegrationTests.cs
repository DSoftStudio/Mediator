// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Companions.IntegrationTests;

/// <summary>
/// Caching, exercised through a real container, the real generator and a real <c>HybridCache</c>.
/// <para>
/// The package's own suite was green while five defects were live in it, so these are written against
/// the axes that suite had no assertion on: what the handler observes rather than what it returns,
/// what happens under real concurrency, and what a failure leaves behind in the cache.
/// </para>
/// </summary>
public class CachingIntegrationTests
{
    private static ServiceProvider Build(CallLog log, ReportGate gate, Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddHybridCache();
        services.AddSingleton(log);
        services.AddSingleton(gate);
        extra?.Invoke(services);
        services.AddMediatorHybridCache();
        services.PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        return services.BuildServiceProvider();
    }

    private static GetReport Report(string key, int ttlMinutes = 5)
        => new(key, TimeSpan.FromMinutes(ttlMinutes));

    [Fact]
    public async Task Concurrent_callers_on_one_key_share_a_single_handler_execution()
    {
        var log = new CallLog();
        var gate = new ReportGate { Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var provider = Build(log, gate);
        var mediator = provider.GetRequiredService<IMediator>();
        var request = Report("stampede");

        // Twenty callers arrive while the handler is still blocked. Stampede prevention is the headline
        // feature of this package and nothing in its suite ever ran two dispatches at once.
        var calls = Enumerable.Range(0, 20)
            .Select(_ => mediator.Send(request, TestContext.Current.CancellationToken).AsTask())
            .ToArray();

        gate.Hold.SetResult();
        var results = await Task.WhenAll(calls);

        log.HandlerCalls.ShouldBe(1);
        results.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task A_handler_that_throws_leaves_nothing_cached()
    {
        var log = new CallLog();
        var gate = new ReportGate { Throw = true };
        using var provider = Build(log, gate);
        var mediator = provider.GetRequiredService<IMediator>();
        var request = Report("failing");

        await Should.ThrowAsync<InvalidOperationException>(
            () => mediator.Send(request, TestContext.Current.CancellationToken).AsTask());

        // A cached failure is worse than no cache: the next caller would get the same exception for the
        // whole TTL without the handler ever being asked again.
        gate.Throw = false;
        var recovered = await mediator.Send(request, TestContext.Current.CancellationToken);

        log.HandlerCalls.ShouldBe(2);
        recovered.ShouldStartWith("report:failing");
    }

    [Fact]
    public async Task An_entry_stops_serving_once_its_duration_has_elapsed()
    {
        var log = new CallLog();
        using var provider = Build(log, new ReportGate());
        var mediator = provider.GetRequiredService<IMediator>();

        // A real, very short TTL rather than an assertion about the options object: the existing suite
        // checked that Duration reached HybridCacheEntryOptions, never that an entry actually expires.
        var request = new GetReport("ttl", TimeSpan.FromMilliseconds(150));

        await mediator.Send(request, TestContext.Current.CancellationToken);
        await mediator.Send(request, TestContext.Current.CancellationToken);
        log.HandlerCalls.ShouldBe(1);

        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);

        await mediator.Send(request, TestContext.Current.CancellationToken);
        log.HandlerCalls.ShouldBe(2);
    }

    [Fact]
    public async Task Cancelling_the_caller_cancels_the_handler_it_is_waiting_on()
    {
        var log = new CallLog();
        var gate = new ReportGate { Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var provider = Build(log, gate);
        var mediator = provider.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource();
        var pending = mediator.Send(Report("cancelled"), cts.Token).AsTask();

        await cts.CancelAsync();

        // The caller's token has to keep reaching the handler through the cache. Passing
        // CancellationToken.None to GetOrCreateAsync would restore ambient context flow at the price of
        // this, so it is pinned rather than left to review.
        await Should.ThrowAsync<OperationCanceledException>(() => pending);

        gate.Hold.SetResult();
    }

    [Fact]
    public async Task The_cache_is_shared_across_scopes()
    {
        var log = new CallLog();
        using var provider = Build(log, new ReportGate());
        var request = Report("scoped");

        // The behavior is a Singleton now. If that had captured anything scoped, this is where it would
        // show: two scopes, two resolutions of IMediator, one cache entry.
        using (var first = provider.CreateScope())
            await first.ServiceProvider.GetRequiredService<IMediator>()
                .Send(request, TestContext.Current.CancellationToken);

        using (var second = provider.CreateScope())
            await second.ServiceProvider.GetRequiredService<IMediator>()
                .Send(request, TestContext.Current.CancellationToken);

        log.HandlerCalls.ShouldBe(1);
    }

    [Fact]
    public async Task The_handler_reads_the_callers_ambient_state_on_a_miss()
    {
        var log = new CallLog();
        using var provider = Build(log, new ReportGate());
        var mediator = provider.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource();
        AmbientTenant.Value = "tenant-77";

        // A CANCELLABLE token is the whole point: that is the only shape HybridCache dispatches through
        // ThreadPool.UnsafeQueueUserWorkItem, which captures no ExecutionContext. Nothing in the
        // package's own suite ever asserted on what the handler could SEE, only on what it returned.
        await mediator.Send(Report("ambient"), cts.Token);

        log.TenantsSeen.ShouldBe(["tenant-77"]);
    }

    [Fact]
    public async Task Under_a_stampede_every_caller_gets_the_value_the_FIRST_ones_context_produced()
    {
        var log = new CallLog();
        var gate = new ReportGate { Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var provider = Build(log, gate);
        var mediator = provider.GetRequiredService<IMediator>();
        var request = Report("shared-context");

        using var cts = new CancellationTokenSource();

        // Two tenants, one key. The leader's handler runs; the joiner waits on it.
        AmbientTenant.Value = "tenant-leader";
        var leader = mediator.Send(request, cts.Token).AsTask();

        await Task.Delay(50, TestContext.Current.CancellationToken);

        AmbientTenant.Value = "tenant-joiner";
        var joiner = mediator.Send(request, cts.Token).AsTask();

        gate.Hold.SetResult();
        var results = await Task.WhenAll(leader, joiner);

        // This is the caveat the README states, made executable: stampede prevention means ONE handler
        // execution serves everyone, so whatever that execution read out of ambient state is what the
        // joiner receives too. A key that does not encode the tenant leaks across tenants — and the
        // fix for the ExecutionContext defect cannot change this, because sharing is the feature.
        log.HandlerCalls.ShouldBe(1);
        log.TenantsSeen.ShouldBe(["tenant-leader"]);
        results[0].ShouldBe(results[1]);
    }

    [Fact]
    public async Task A_request_that_does_not_cache_is_unaffected_by_the_package_being_installed()
    {
        var log = new CallLog();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddHybridCache();
        services.AddSingleton(log);
        services.AddSingleton(new ReportGate());
        services.AddMediatorHybridCache<GetReport, string>();
        services.PrecompilePipelines().PrecompileNotifications().PrecompileStreams();

        using var provider = services.BuildServiceProvider();

        // The blast radius, which nothing in the package's own suite ever looked at: with the closed
        // registration, a request that caches nothing has no chain built for it at all.
        services.ShouldNotContain(d => d.ServiceType == typeof(PipelineChainHandler<SlowCommand, string>));

        var result = await provider.GetRequiredService<IMediator>()
            .Send(new SlowCommand("through"), TestContext.Current.CancellationToken);

        result.ShouldBe("through");
    }
}
