// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Types owned by this file ────────────────────────────────────────────────

public record RelPing : IRequest<int>;

public record RelStream : IStreamRequest<int>;

public record RelNote : INotification;

/// <summary>Scoped, so it is reachable only through its scope: if it outlives
/// <c>scope.Dispose()</c> plus a full collection, something else is rooting it.</summary>
public sealed class RelPayload
{
    public readonly byte[] Ballast = new byte[256];
}

public sealed class RelPingHandler(IServiceProvider sp) : IRequestHandler<RelPing, int>
{
    private readonly RelPayload? _payload = sp.GetService<RelPayload>();

    public ValueTask<int> Handle(RelPing request, CancellationToken ct) => new(_payload is null ? 0 : 1);
}

public sealed class RelStreamHandler(IServiceProvider sp) : IStreamRequestHandler<RelStream, int>
{
    private readonly RelPayload? _payload = sp.GetService<RelPayload>();

    public async IAsyncEnumerable<int> Handle(
        RelStream request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield return _payload is null ? 0 : 1;
    }
}

public sealed class RelNoteHandler(IServiceProvider sp) : INotificationHandler<RelNote>
{
    private readonly RelPayload? _payload = sp.GetService<RelPayload>();

    public Task Handle(RelNote n, CancellationToken ct)
    {
        _ = _payload;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Disposing a scope must let that scope go. The dispatch caches key on the
/// <see cref="IServiceProvider"/> and hold it in thread-local state, so a slot that is never emptied
/// keeps the disposed scope — and every scoped instance it resolved — reachable.
/// <para>
/// Nothing used to empty one. A <c>[ThreadStatic]</c> can only be written by its own thread, and the
/// thread that disposes a scope is rarely the one that dispatched on it, so the slot was released
/// only by accident: when that same thread happened to dispatch that same request type again against
/// a different provider. For a request type served rarely, or a pool thread that moves on to other
/// work, that is never — measured, the payload below survived <c>Dispose</c> and a full
/// <c>GC.Collect</c> and became collectible only once the slot was overwritten.
/// </para>
/// <para>
/// The caches now hold a <see cref="DispatchCacheSlot{TValue}"/>, which the scope's disposal empties
/// through <see cref="DispatchCacheReleaser"/> from whichever thread does the disposing.
/// </para>
/// </summary>
public class ScopeReleasedOnDisposeTests
{
    /// <summary>
    /// Runs one dispatch in its own scope and disposes it, returning weak references to the scope and
    /// to a scoped instance it resolved.
    /// <para>
    /// The scope work is in its own <c>NoInlining</c> method on purpose: in a Debug build the JIT
    /// keeps locals alive to the end of the enclosing method, so measuring in the test body reports a
    /// leak that is really just the test frame holding the scope.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference Payload, WeakReference Provider)> DispatchInAScope(
        ServiceProvider root,
        Func<IServiceProvider, CancellationToken, Task> dispatch,
        CancellationToken ct)
    {
        var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        await dispatch(sp, ct);

        var refs = (new WeakReference(sp.GetRequiredService<RelPayload>()), new WeakReference(sp));
        scope.Dispose();
        return refs;
    }

    private static void FullGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static async Task AssertScopeIsReleased(
        ServiceCollection services,
        Func<IServiceProvider, CancellationToken, Task> dispatch,
        CancellationToken ct)
    {
        services.AddScoped<RelPayload>();

        using var root = services.BuildServiceProvider();

        var (payload, provider) = await DispatchInAScope(root, dispatch, ct);

        FullGc();

        provider.IsAlive.ShouldBeFalse("the disposed scope must not be reachable from a dispatch cache");
        payload.IsAlive.ShouldBeFalse("nor anything the scope resolved");
    }

    [Fact]
    public async Task Send_ReleasesTheScope_OnDispose()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped<IRequestHandler<RelPing, int>, RelPingHandler>();

        await AssertScopeIsReleased(
            services,
            static (sp, ct) => sp.GetRequiredService<IMediator>().Send(new RelPing(), ct).AsTask(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateStream_ReleasesTheScope_OnDispose()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped<IStreamRequestHandler<RelStream, int>, RelStreamHandler>();
        services.PrecompileStreams();

        await AssertScopeIsReleased(
            services,
            static async (sp, ct) =>
            {
                await foreach (var _ in sp.GetRequiredService<IMediator>().CreateStream(new RelStream(), ct))
                {
                }
            },
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Publish_ReleasesTheScope_OnDispose()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped<INotificationHandler<RelNote>, RelNoteHandler>();
        services.PrecompileNotifications();

        await AssertScopeIsReleased(
            services,
            static (sp, ct) => sp.GetRequiredService<IMediator>().Publish(new RelNote(), ct),
            TestContext.Current.CancellationToken);
    }
}
