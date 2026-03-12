// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Unique types ──

public record CovFinalPing : IRequest<int>;
public record CovFinalAsyncPostPing : IRequest<int>;

// ── Handlers ──

public sealed class CovFinalPingHandler : IRequestHandler<CovFinalPing, int>
{
    public ValueTask<int> Handle(CovFinalPing request, CancellationToken ct) => new(55);
}

public sealed class CovFinalAsyncPostPingHandler : IRequestHandler<CovFinalAsyncPostPing, int>
{
    public ValueTask<int> Handle(CovFinalAsyncPostPing request, CancellationToken ct) => new(66);
}

// Sync post-processor that completes synchronously
public sealed class SyncPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
{
    public ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
        => ValueTask.CompletedTask;
}

/// <summary>
/// Final coverage push — targets the last uncovered lines.
/// </summary>
public class FinalCoverageTests
{
    [Fact]
    public void Mediator_IServiceProviderAccessor_ReturnsProvider()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        // Cast to IServiceProviderAccessor (internal interface)
        var accessor = mediator as IServiceProviderAccessor;
        accessor.ShouldNotBeNull();
        accessor.ServiceProvider.ShouldNotBeNull();
    }

    [Fact]
    public async Task PipelineChainHandler_SyncPostProcessor_SyncPath()
    {
        // Tests AwaitPostProcessorAndContinue — sync post-processor on sync core
        var services = new ServiceCollection();
        services.AddTransient<IRequestPostProcessor<CovFinalPing, int>, SyncPostProcessor<CovFinalPing, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovFinalPing, int>(new CovFinalPing());
        result.ShouldBe(55);
    }

    [Fact]
    public async Task PipelineChainHandler_AsyncPostProcessor_OnSyncCore()
    {
        // Sync handler + async post-processor = AwaitPostProcessorAndContinue path
        var services = new ServiceCollection();
        services.AddTransient<IRequestPostProcessor<CovFinalAsyncPostPing, int>,
            AsyncPostProcessor<CovFinalAsyncPostPing, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovFinalAsyncPostPing, int>(new CovFinalAsyncPostPing());
        result.ShouldBe(66);
    }

    [Fact]
    public void RequestDispatch_Pipeline_Property_NotNull_AfterPrecompile()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();

        // The Pipeline delegate is set by PrecompilePipelines() generated code
        RequestDispatch<CovFinalPing, int>.Pipeline.ShouldNotBeNull();
    }

    [Fact]
    public void RequestDispatch_HasPipelineChain_And_IsCacheable()
    {
        var services = new ServiceCollection();
        // Singleton behavior → cacheable chain
        services.AddSingleton<IPipelineBehavior<CovFinalPing, int>>(
            new CovCacheLoggingBehavior2());
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();

        RequestDispatch<CovFinalPing, int>.HasPipelineChain.ShouldBeTrue();
        RequestDispatch<CovFinalPing, int>.IsPipelineChainCacheable.ShouldBeTrue();
    }
}

public sealed class CovCacheLoggingBehavior2 : IPipelineBehavior<CovFinalPing, int>
{
    public ValueTask<int> Handle(CovFinalPing request,
        IRequestHandler<CovFinalPing, int> next, CancellationToken ct)
        => next.Handle(request, ct);
}

// ── Types for Mediator.Publish<T> coverage ──

public record CovDirectPublishNotif : INotification;

public sealed class CovDirectPublishHandler : INotificationHandler<CovDirectPublishNotif>
{
    private static int _callCount;
    public static int CallCount => _callCount;
    public static void ResetCallCount() => _callCount = 0;

    public Task Handle(CovDirectPublishNotif notification, CancellationToken ct)
    {
        Interlocked.Increment(ref _callCount);
        return Task.CompletedTask;
    }
}

// ── Types for multiple async post-processors ──

public record CovMultiPostPing : IRequest<int>;

public sealed class CovMultiPostPingHandler : IRequestHandler<CovMultiPostPing, int>
{
    public ValueTask<int> Handle(CovMultiPostPing request, CancellationToken ct) => new(88);
}

/// <summary>
/// Covers Mediator.Publish&lt;T&gt; body, AwaitPostProcessorAndContinue with multiple
/// async post-processors, and StreamPipelineChainHandler explicit interface impl.
/// </summary>
public class MediatorPublishGenericCoverageTests
{
    [Fact]
    public async Task Publish_Generic_WithoutPublisher_UsesDefaultPath()
    {
        CovDirectPublishHandler.ResetCallCount();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();

        // Call Publish<T> explicitly via the IPublisher interface
        IPublisher publisher = sp.GetRequiredService<IMediator>();
        await publisher.Publish(new CovDirectPublishNotif());

        CovDirectPublishHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_Generic_WithCustomPublisher_UsesPublisherPath()
    {
        CovDirectPublishHandler.ResetCallCount();
        var services = new ServiceCollection();
        services.AddSingleton<INotificationPublisher, SequentialNotificationPublisher>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();

        IPublisher publisher = sp.GetRequiredService<IMediator>();
        await publisher.Publish(new CovDirectPublishNotif());

        CovDirectPublishHandler.CallCount.ShouldBe(1);
    }
}

public class MultiAsyncPostProcessorTests
{
    [Fact]
    public async Task TwoAsyncPostProcessors_AwaitPostProcessorAndContinue()
    {
        // This exercises the AwaitPostProcessorAndContinue path with
        // multiple post-processors where the first one is async.
        var services = new ServiceCollection();
        services.AddTransient<IRequestPostProcessor<CovMultiPostPing, int>,
            AsyncPostProcessor<CovMultiPostPing, int>>();
        services.AddTransient<IRequestPostProcessor<CovMultiPostPing, int>,
            AsyncPostProcessor<CovMultiPostPing, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovMultiPostPing, int>(new CovMultiPostPing());
        result.ShouldBe(88);
    }
}
