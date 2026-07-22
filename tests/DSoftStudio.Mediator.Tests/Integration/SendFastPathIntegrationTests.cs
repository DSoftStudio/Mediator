// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════
//  TEST-LOCAL TYPES — avoid cross-test static pollution
// ═══════════════════════════════════════════════════════════════════

public sealed record FastPathPing(int N) : IRequest<string>;

/// <summary>Unique handler → the generated typed Send extension gets a concrete-typed cache.</summary>
public sealed class FastPathPingHandler : IRequestHandler<FastPathPing, string>
{
    public ValueTask<string> Handle(FastPathPing request, CancellationToken ct)
        => new("fastpath:" + request.N);
}

public sealed class FastPathOverridePing(int N) : IRequest<string> { public int N { get; } = N; }

public sealed class FastPathOverridePingHandler : IRequestHandler<FastPathOverridePing, string>
{
    public ValueTask<string> Handle(FastPathOverridePing request, CancellationToken ct)
        => new("original:" + request.N);
}

/// <summary>
/// Registered manually AFTER RegisterMediatorHandlers in the override tests — MSDI last-wins makes
/// this the runtime winner while the generator only knew <see cref="FastPathOverridePingHandler"/>
/// (the emitted concrete cache is typed to THAT). Deliberately <c>file</c>-local so both
/// RegisterMediatorHandlers and the generator's handler map skip it (file-local types are never
/// discoverable) — this reproduces the real scenario of a runtime-only override of a different
/// concrete type, which the cache's <c>is</c>-typed miss path must survive: no
/// InvalidCastException, override honored on every call, wrong type never cached.
/// </summary>
file sealed class FastPathReplacementHandler : IRequestHandler<FastPathOverridePing, string>
{
    public ValueTask<string> Handle(FastPathOverridePing request, CancellationToken ct)
        => new("override:" + request.N);
}

public sealed record FastPathBasePing(int N) : IRequest<string>;

/// <summary>Non-sealed, NON-virtual Handle — the subclass-hiding scenario's base.</summary>
public class FastPathBaseHandler : IRequestHandler<FastPathBasePing, string>
{
    public ValueTask<string> Handle(FastPathBasePing request, CancellationToken ct)
        => new("base:" + request.N);
}

public sealed record FastPathAsyncPing(int N) : IRequest<string>;

/// <summary>Genuinely suspends — exercises the AwaitAndBox / non-sync-completion paths.</summary>
public sealed class FastPathAsyncPingHandler : IRequestHandler<FastPathAsyncPing, string>
{
    public async ValueTask<string> Handle(FastPathAsyncPing request, CancellationToken ct)
    {
        await Task.Yield();
        return "async:" + request.N;
    }
}

public sealed record FastPathScopedPing() : IRequest<int>;

/// <summary>Scoped mutable state: proves the fast path never pins one scope's instance globally.</summary>
[HandlerLifetime(HandlerLifetime.Scoped)]
public sealed class FastPathScopedPingHandler : IRequestHandler<FastPathScopedPing, int>
{
    private int _calls;
    public ValueTask<int> Handle(FastPathScopedPing request, CancellationToken ct)
        => new(++_calls);
}

/// <summary>
/// Runtime behavior of the ADR-0065 SAFE fast path through the generated TYPED EXTENSIONS
/// (interceptors are suppressed in this project — the typed extension is the dominant real-world
/// dispatch shape and carries the same emitted concrete cache).
/// </summary>
public class SendFastPathIntegrationTests
{
    private static ServiceProvider BuildProvider(Action<IServiceCollection>? postRegistration = null)
    {
        var services = new ServiceCollection();
        services
            .AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();
        postRegistration?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Typed_Send_Dispatches_Through_Concrete_Cache()
    {
        await using var sp = BuildProvider();
        var sender = sp.GetRequiredService<ISender>();

        // Twice: first call takes the miss path (populates the cache), second the concrete hit path.
        (await sender.Send(new FastPathPing(1))).ShouldBe("fastpath:1");
        (await sender.Send(new FastPathPing(2))).ShouldBe("fastpath:2");
    }

    [Fact]
    public async Task User_Override_After_Registration_Is_Honored_On_Every_Call()
    {
        // The generator emitted the cache typed to FastPathOverridePingHandler; at runtime the
        // user re-registers a DIFFERENT implementation (last-wins). The is-typed miss path must
        // fall back to interface dispatch — override honored, no InvalidCastException, and the
        // wrong concrete type must never be cached.
        await using var sp = BuildProvider(services =>
            services.AddTransient<IRequestHandler<FastPathOverridePing, string>, FastPathReplacementHandler>());
        var sender = sp.GetRequiredService<ISender>();

        (await sender.Send(new FastPathOverridePing(1))).ShouldBe("override:1");
        (await sender.Send(new FastPathOverridePing(2))).ShouldBe("override:2");
    }

    [Fact]
    public async Task Two_Containers_Each_Get_Their_Own_Handler()
    {
        // Provider-keyed cache: alternating dispatches from two containers on the SAME thread must
        // re-key per provider and never leak container A's handler into container B.
        await using var spA = BuildProvider();
        await using var spB = BuildProvider(services =>
            services.AddTransient<IRequestHandler<FastPathOverridePing, string>, FastPathReplacementHandler>());

        var senderA = spA.GetRequiredService<ISender>();
        var senderB = spB.GetRequiredService<ISender>();

        (await senderA.Send(new FastPathOverridePing(1))).ShouldBe("original:1");
        (await senderB.Send(new FastPathOverridePing(1))).ShouldBe("override:1");
        (await senderA.Send(new FastPathOverridePing(2))).ShouldBe("original:2");
        (await senderB.Send(new FastPathOverridePing(2))).ShouldBe("override:2");
    }

    [Fact]
    public async Task Scoped_Handler_Gets_Distinct_Instances_Per_Scope()
    {
        // Captive-dependency guard: the fast path must resolve the scoped handler per scope.
        // Each scope's first dispatch must see fresh state (counter restarts at 1).
        await using var sp = BuildProvider();

        using (var scope1 = sp.CreateScope())
        {
            var sender = scope1.ServiceProvider.GetRequiredService<ISender>();
            (await sender.Send(new FastPathScopedPing())).ShouldBe(1);
            (await sender.Send(new FastPathScopedPing())).ShouldBe(2);
        }

        using (var scope2 = sp.CreateScope())
        {
            var sender = scope2.ServiceProvider.GetRequiredService<ISender>();
            (await sender.Send(new FastPathScopedPing())).ShouldBe(1,
                customMessage: "a new scope must get a fresh scoped handler instance — never a pinned one");
        }
    }

    [Fact]
    public async Task Send_Object_Runtime_Dispatch_Uses_Same_Path_Correctly()
    {
        // The Send(object) switch shares the concrete cache with the typed extension.
        await using var sp = BuildProvider();
        var sender = sp.GetRequiredService<ISender>();

        object request = new FastPathPing(7);
        var result = await sender.Send(request);
        result.ShouldBe("fastpath:7");
    }

    [Fact]
    public async Task Subclass_Override_With_Hidden_Handle_Is_Honored()
    {
        // A runtime-registered SUBCLASS of the mapped handler that hides Handle with `new`:
        // an `is`-typed guard would match it and statically bind to the BASE Handle (silent
        // wrong dispatch). The exact-type guard must keep it on interface dispatch, where the
        // derived Handle wins. (Found by the phase-1 review.)
        await using var sp = BuildProvider(services =>
            services.AddTransient<IRequestHandler<FastPathBasePing, string>, FastPathDerivedHandler>());
        var sender = sp.GetRequiredService<ISender>();

        (await sender.Send(new FastPathBasePing(1))).ShouldBe("derived:1");
        (await sender.Send(new FastPathBasePing(2))).ShouldBe("derived:2");
    }

    [Fact]
    public async Task Truly_Async_Handler_Boxes_Correctly_Through_Both_Paths()
    {
        // The AwaitAndBox path in the Send(object) switch + the typed extension, with a handler
        // that genuinely suspends (never IsCompletedSuccessfully at dispatch time).
        await using var sp = BuildProvider();
        var sender = sp.GetRequiredService<ISender>();

        (await sender.Send(new FastPathAsyncPing(5))).ShouldBe("async:5");

        object request = new FastPathAsyncPing(6);
        (await sender.Send(request)).ShouldBe("async:6");
    }

    [Fact]
    public async Task Concurrent_Dispatch_Through_ThreadStatic_Cache_Is_Correct()
    {
        // Miss-fill races across pool threads: every thread must resolve/cach correctly.
        await using var sp = BuildProvider();
        var sender = sp.GetRequiredService<ISender>();

        var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(async () =>
        {
            for (int j = 0; j < 100; j++)
                (await sender.Send(new FastPathPing(i))).ShouldBe("fastpath:" + i);
        }));

        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Subclass of the mapped handler hiding Handle with <c>new</c> — file-local so discovery
/// never sees it (no compile-time ambiguity) and only the runtime registration exists.
/// </summary>
file sealed class FastPathDerivedHandler : FastPathBaseHandler, IRequestHandler<FastPathBasePing, string>
{
    public new ValueTask<string> Handle(FastPathBasePing request, CancellationToken ct)
        => new("derived:" + request.N);
}
