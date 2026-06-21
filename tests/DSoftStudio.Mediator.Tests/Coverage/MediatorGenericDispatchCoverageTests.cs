// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ══════════════════════════════════════════════════════════════════════════════════════════════
//  Mediator.Send<,> / Publish<> / CreateStream<,> — the NON-INTERCEPTED dispatch path.
//
//  Every concrete call site in this assembly (e.g. mediator.Send<Ping, int>(…)) is REPLACED by the
//  source-generated interceptor, whose inlined fast path never enters these methods — so they sat at
//  0% even though the whole suite exercises Send/Publish/CreateStream constantly.
//
//  These methods are still the real dispatch used whenever a call site is NOT intercepted:
//    • an open-generic caller — TRequest/TResponse are type parameters, so the generator cannot emit a
//      concrete interceptor and the call binds to the interface method (this is what the helpers below do);
//    • reflection / cached-delegate callers that resolve IMediator dynamically;
//    • a consumer built with DSoftMediatorSuppressInterceptors=true.
//
//  The OpenGeneric helper reproduces that path with zero artificial machinery: it forwards through a
//  generic method, so the call uses open type parameters the interceptor cannot bind, reaching the real
//  Mediator.Send/Publish/CreateStream. Each branch (pipeline vs handler-cache, custom publisher vs
//  sequential, precompiled stream vs invoker fallback) gets its own unique message type.
// ══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Forwards through open type parameters so the source-generated interceptors cannot bind to the call
/// site — the calls reach the real <c>Mediator.Send/Publish/CreateStream</c> instead of the inlined
/// fast path. This is the dispatch a reflection / open-generic / interceptor-suppressed caller hits.
/// </summary>
file static class OpenGeneric
{
    public static ValueTask<TResponse> Send<TRequest, TResponse>(IMediator m, TRequest request, CancellationToken ct = default)
        where TRequest : IRequest<TResponse>
        => m.Send<TRequest, TResponse>(request, ct);

    public static Task Publish<TNotification>(IMediator m, TNotification notification, CancellationToken ct = default)
        where TNotification : INotification
        => m.Publish(notification, ct);

    public static IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(IMediator m, TRequest request, CancellationToken ct = default)
        where TRequest : IStreamRequest<TResponse>
        => m.CreateStream<TRequest, TResponse>(request, ct);
}

// ── Message types (unique per branch to avoid static-dispatch collisions with other tests) ──────────

public record MgdNoPipe : IRequest<int>;       // Send → HasPipelineChain == false (handler-cache path)
public record MgdWithPipe : IRequest<int>;     // Send → HasPipelineChain == true  (chain path)
public record MgdNotif : INotification;        // Publish → no custom publisher (sequential dispatch)
public record MgdNotifPub : INotification;     // Publish → custom INotificationPublisher registered
public record MgdStreamPre : IStreamRequest<int>;    // CreateStream → precompiled (static pipeline)
public record MgdStreamOrphan : IStreamRequest<int>; // CreateStream → no handler ever discovered → invoker fallback

public sealed class MgdNoPipeHandler : IRequestHandler<MgdNoPipe, int>
{
    public ValueTask<int> Handle(MgdNoPipe request, CancellationToken ct) => new(11);
}

public sealed class MgdWithPipeHandler : IRequestHandler<MgdWithPipe, int>
{
    public ValueTask<int> Handle(MgdWithPipe request, CancellationToken ct) => new(22);
}

public sealed class MgdNotifHandler : INotificationHandler<MgdNotif>
{
    public static int Count;
    public Task Handle(MgdNotif notification, CancellationToken ct) { Count++; return Task.CompletedTask; }
}

public sealed class MgdNotifPubHandler : INotificationHandler<MgdNotifPub>
{
    public static int Count;
    public Task Handle(MgdNotifPub notification, CancellationToken ct) { Count++; return Task.CompletedTask; }
}

public sealed class MgdStreamPreHandler : IStreamRequestHandler<MgdStreamPre, int>
{
    public async IAsyncEnumerable<int> Handle(MgdStreamPre request, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return 1;
        yield return 2;
        await Task.CompletedTask;
    }
}

// NOTE: MgdStreamOrphan deliberately has NO handler — so it is never discovered or precompiled and its
// StreamDispatch<,>.Pipeline stays null, forcing Mediator.CreateStream down the invoker fallback.

/// <summary>
/// Drives the three generic dispatch methods on <c>Mediator</c> through their real (non-intercepted) body,
/// covering every branch.
/// </summary>
public class MediatorGenericDispatchCoverageTests
{
    private static IMediator BuildMediator(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    // ── Send<TRequest, TResponse> ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_NoPipeline_GoesThroughHandlerCache()
    {
        // No IPipelineBehavior registered → RequestDispatch.HasPipelineChain == false → HandlerCache path.
        var mediator = BuildMediator(_ => { });

        var result = await OpenGeneric.Send<MgdNoPipe, int>(mediator, new MgdNoPipe(), TestContext.Current.CancellationToken);

        result.ShouldBe(11);
    }

    [Fact]
    public async Task Send_WithPipeline_GoesThroughChain()
    {
        // A behavior is registered → RequestDispatch.HasPipelineChain == true → resolve + run the chain.
        var mediator = BuildMediator(s =>
            s.AddTransient<IPipelineBehavior<MgdWithPipe, int>, PassThroughBehavior<MgdWithPipe, int>>());

        var result = await OpenGeneric.Send<MgdWithPipe, int>(mediator, new MgdWithPipe(), TestContext.Current.CancellationToken);

        result.ShouldBe(22);
    }

    [Fact]
    public async Task Send_NullRequest_Throws()
    {
        var mediator = BuildMediator(_ => { });

        await Should.ThrowAsync<ArgumentNullException>(
            () => OpenGeneric.Send<MgdNoPipe, int>(mediator, null!).AsTask());
    }

    // ── Publish<TNotification> ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_NoCustomPublisher_UsesSequentialDispatch()
    {
        // No INotificationPublisher registered → _notificationPublisher is null → cached sequential dispatch.
        // MgdNotifHandler is auto-discovered by RegisterMediatorHandlers (registering it again would
        // double-dispatch on the GetServices path).
        MgdNotifHandler.Count = 0;
        var mediator = BuildMediator(_ => { });

        await OpenGeneric.Publish(mediator, new MgdNotif(), TestContext.Current.CancellationToken);

        MgdNotifHandler.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_WithCustomPublisher_UsesPublisher()
    {
        // A custom INotificationPublisher is registered → _notificationPublisher is not null → publisher path
        // (GetServices<INotificationHandler<T>> + publisher.Publish). The handler is auto-discovered.
        MgdNotifPubHandler.Count = 0;
        var mediator = BuildMediator(s =>
            s.AddSingleton<INotificationPublisher, SequentialNotificationPublisher>());

        await OpenGeneric.Publish(mediator, new MgdNotifPub(), TestContext.Current.CancellationToken);

        MgdNotifPubHandler.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_NullNotification_Throws()
    {
        var mediator = BuildMediator(_ => { });

        await Should.ThrowAsync<ArgumentNullException>(
            () => OpenGeneric.Publish<MgdNotif>(mediator, null!));
    }

    // ── CreateStream<TRequest, TResponse> ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStream_Precompiled_UsesStaticPipeline()
    {
        // PrecompileStreams() populates StreamDispatch<,>.Pipeline → the O(1) static-field path.
        var mediator = BuildMediator(_ => { });

        var values = new List<int>();
        await foreach (var v in OpenGeneric.CreateStream<MgdStreamPre, int>(mediator, new MgdStreamPre(), TestContext.Current.CancellationToken))
            values.Add(v);

        values.ShouldBe(new[] { 1, 2 });
    }

    [Fact]
    public async Task CreateStream_UnregisteredStream_FallsBackToInvoker_AndThrowsClearError()
    {
        // MgdStreamOrphan has no handler → never discovered/precompiled → StreamDispatch<,>.Pipeline stays
        // null → CreateStream takes the StreamPipelineInvoker fallback, which surfaces a clear
        // "not registered" error rather than a NullReferenceException.
        var mediator = BuildMediator(_ => { });

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in OpenGeneric.CreateStream<MgdStreamOrphan, int>(mediator, new MgdStreamOrphan(), TestContext.Current.CancellationToken))
            {
            }
        });
        ex.Message.ShouldContain("MgdStreamOrphan");
    }

    [Fact]
    public async Task CreateStream_NullRequest_Throws()
    {
        var mediator = BuildMediator(_ => { });

        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in OpenGeneric.CreateStream<MgdStreamPre, int>(mediator, null!))
            {
            }
        });
    }
}
