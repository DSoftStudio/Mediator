// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

// ── How stream dispatch scales with pipeline depth ────────────────
// The stream suites measure a bare CreateStream with NO behaviors registered, so the whole stream
// pipeline -- the chain, its lifetime fold, the per-enumeration resolution -- was unmeasured. This
// mirrors DSoftBehaviorScalingBenchmarks exactly: the same depths, one request type per depth so a
// single container holds them all, the same job, and pass-through links that do nothing but call the
// next one.
public sealed record SScale0 : IStreamRequest<int>;
public sealed record SScale1 : IStreamRequest<int>;
public sealed record SScale2 : IStreamRequest<int>;
public sealed record SScale3 : IStreamRequest<int>;
public sealed record SScale5 : IStreamRequest<int>;
public sealed record SScale8 : IStreamRequest<int>;
public sealed record SScaleVerify : IStreamRequest<int>;

public sealed class SScale0Handler : IStreamRequestHandler<SScale0, int>
{
    public async IAsyncEnumerable<int> Handle(SScale0 r, [EnumeratorCancellation] CancellationToken ct)
    { yield return 42; await Task.CompletedTask; }
}
public sealed class SScale1Handler : IStreamRequestHandler<SScale1, int>
{
    public async IAsyncEnumerable<int> Handle(SScale1 r, [EnumeratorCancellation] CancellationToken ct)
    { yield return 42; await Task.CompletedTask; }
}
public sealed class SScale2Handler : IStreamRequestHandler<SScale2, int>
{
    public async IAsyncEnumerable<int> Handle(SScale2 r, [EnumeratorCancellation] CancellationToken ct)
    { yield return 42; await Task.CompletedTask; }
}
public sealed class SScale3Handler : IStreamRequestHandler<SScale3, int>
{
    public async IAsyncEnumerable<int> Handle(SScale3 r, [EnumeratorCancellation] CancellationToken ct)
    { yield return 42; await Task.CompletedTask; }
}
public sealed class SScale5Handler : IStreamRequestHandler<SScale5, int>
{
    public async IAsyncEnumerable<int> Handle(SScale5 r, [EnumeratorCancellation] CancellationToken ct)
    { yield return 42; await Task.CompletedTask; }
}
public sealed class SScale8Handler : IStreamRequestHandler<SScale8, int>
{
    public async IAsyncEnumerable<int> Handle(SScale8 r, [EnumeratorCancellation] CancellationToken ct)
    { yield return 42; await Task.CompletedTask; }
}
public sealed class SScaleVerifyHandler : IStreamRequestHandler<SScaleVerify, int>
{
    public async IAsyncEnumerable<int> Handle(SScaleVerify r, [EnumeratorCancellation] CancellationToken ct)
    { yield return 42; await Task.CompletedTask; }
}

// Pass-through links. Distinct types, so a library that chains by type cannot collapse them.
public sealed class SChain1<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}
public sealed class SChain2<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}
public sealed class SChain3<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}
public sealed class SChain4<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}
public sealed class SChain5<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}
public sealed class SChain6<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}
public sealed class SChain7<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}
public sealed class SChain8<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}

internal static class SScaleCounter
{
    public static int Count;
}

public sealed class SScaleCount1<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SScaleCounter.Count); return next.Handle(request, ct); }
}
public sealed class SScaleCount2<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SScaleCounter.Count); return next.Handle(request, ct); }
}
public sealed class SScaleCount3<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SScaleCounter.Count); return next.Handle(request, ct); }
}
public sealed class SScaleCount4<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SScaleCounter.Count); return next.Handle(request, ct); }
}
public sealed class SScaleCount5<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(
        TRequest request, IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SScaleCounter.Count); return next.Handle(request, ct); }
}

/// <summary>
/// How DSoft's stream dispatch cost scales with the number of stream pipeline behaviors.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 12, iterationCount: 30)]
[MedianColumn]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Declared)]
public class DSoftStreamScalingBenchmarks
{
    private static readonly SScale0 M0 = new();
    private static readonly SScale1 M1 = new();
    private static readonly SScale2 M2 = new();
    private static readonly SScale3 M3 = new();
    private static readonly SScale5 M5 = new();
    private static readonly SScale8 M8 = new();

    private IServiceScope _scope = null!;
    private IMediator _mediator = null!;
    private SScale0Handler _direct = null!;

    private static async Task<int> Consume(IAsyncEnumerable<int> stream)
    {
        int result = 0;
        await foreach (var item in stream)
            result = item;
        return result;
    }

    [GlobalSetup]
    public void Setup()
    {
        _direct = new SScale0Handler();

        var services = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
            .RegisterMediatorHandlers();

        // SScale0 deliberately gets no behaviors at all.

        services.AddScoped(typeof(IStreamPipelineBehavior<SScale1, int>), typeof(SChain1<SScale1, int>));

        services.AddScoped(typeof(IStreamPipelineBehavior<SScale2, int>), typeof(SChain1<SScale2, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale2, int>), typeof(SChain2<SScale2, int>));

        services.AddScoped(typeof(IStreamPipelineBehavior<SScale3, int>), typeof(SChain1<SScale3, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale3, int>), typeof(SChain2<SScale3, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale3, int>), typeof(SChain3<SScale3, int>));

        services.AddScoped(typeof(IStreamPipelineBehavior<SScale5, int>), typeof(SChain1<SScale5, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale5, int>), typeof(SChain2<SScale5, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale5, int>), typeof(SChain3<SScale5, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale5, int>), typeof(SChain4<SScale5, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale5, int>), typeof(SChain5<SScale5, int>));

        services.AddScoped(typeof(IStreamPipelineBehavior<SScale8, int>), typeof(SChain1<SScale8, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale8, int>), typeof(SChain2<SScale8, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale8, int>), typeof(SChain3<SScale8, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale8, int>), typeof(SChain4<SScale8, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale8, int>), typeof(SChain5<SScale8, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale8, int>), typeof(SChain6<SScale8, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale8, int>), typeof(SChain7<SScale8, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScale8, int>), typeof(SChain8<SScale8, int>));

        // Counting links on their OWN pair: registration alone does not show that the chain runs.
        services.AddScoped(typeof(IStreamPipelineBehavior<SScaleVerify, int>), typeof(SScaleCount1<SScaleVerify, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScaleVerify, int>), typeof(SScaleCount2<SScaleVerify, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScaleVerify, int>), typeof(SScaleCount3<SScaleVerify, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScaleVerify, int>), typeof(SScaleCount4<SScaleVerify, int>));
        services.AddScoped(typeof(IStreamPipelineBehavior<SScaleVerify, int>), typeof(SScaleCount5<SScaleVerify, int>));

        services.PrecompileStreams();

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();

        var sp = _scope.ServiceProvider;
        BenchmarkVerification.RequireCount(
            sp.GetServices<IStreamPipelineBehavior<SScale0, int>>().Count(), 0,
            "DSoft stream scaling", "the depth-0 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<IStreamPipelineBehavior<SScale1, int>>().Count(), 1,
            "DSoft stream scaling", "the depth-1 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<IStreamPipelineBehavior<SScale2, int>>().Count(), 2,
            "DSoft stream scaling", "the depth-2 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<IStreamPipelineBehavior<SScale3, int>>().Count(), 3,
            "DSoft stream scaling", "the depth-3 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<IStreamPipelineBehavior<SScale5, int>>().Count(), 5,
            "DSoft stream scaling", "the depth-5 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<IStreamPipelineBehavior<SScale8, int>>().Count(), 8,
            "DSoft stream scaling", "the depth-8 behavior chain");

        // And they RUN, not merely resolve.
        SScaleCounter.Count = 0;
        var verifyValue = Consume(_mediator.CreateStream<SScaleVerify, int>(new SScaleVerify())).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            SScaleCounter.Count, 5, "DSoft stream scaling", "the verification chain");
        BenchmarkVerification.Require(
            verifyValue == 42, "DSoft stream scaling", "the verification stream yielded " + verifyValue);

        // Warm every path, and check each one produced its item.
        BenchmarkVerification.Require(
            Consume(_direct.Handle(M0, default)).GetAwaiter().GetResult() == 42,
            "DSoft stream scaling", "the direct handler yielded nothing");
        BenchmarkVerification.Require(
            Consume(_mediator.CreateStream<SScale0, int>(M0)).GetAwaiter().GetResult() == 42,
            "DSoft stream scaling", "the depth-0 stream yielded nothing");
        BenchmarkVerification.Require(
            Consume(_mediator.CreateStream<SScale1, int>(M1)).GetAwaiter().GetResult() == 42,
            "DSoft stream scaling", "the depth-1 stream yielded nothing");
        BenchmarkVerification.Require(
            Consume(_mediator.CreateStream<SScale2, int>(M2)).GetAwaiter().GetResult() == 42,
            "DSoft stream scaling", "the depth-2 stream yielded nothing");
        BenchmarkVerification.Require(
            Consume(_mediator.CreateStream<SScale3, int>(M3)).GetAwaiter().GetResult() == 42,
            "DSoft stream scaling", "the depth-3 stream yielded nothing");
        BenchmarkVerification.Require(
            Consume(_mediator.CreateStream<SScale5, int>(M5)).GetAwaiter().GetResult() == 42,
            "DSoft stream scaling", "the depth-5 stream yielded nothing");
        BenchmarkVerification.Require(
            Consume(_mediator.CreateStream<SScale8, int>(M8)).GetAwaiter().GetResult() == 42,
            "DSoft stream scaling", "the depth-8 stream yielded nothing");
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall() => await Consume(_direct.Handle(M0, default));

    [Benchmark] public async Task<int> Stream_0Behaviors() => await Consume(_mediator.CreateStream<SScale0, int>(M0));
    [Benchmark] public async Task<int> Stream_1Behaviors() => await Consume(_mediator.CreateStream<SScale1, int>(M1));
    [Benchmark] public async Task<int> Stream_2Behaviors() => await Consume(_mediator.CreateStream<SScale2, int>(M2));
    [Benchmark] public async Task<int> Stream_3Behaviors() => await Consume(_mediator.CreateStream<SScale3, int>(M3));
    [Benchmark] public async Task<int> Stream_5Behaviors() => await Consume(_mediator.CreateStream<SScale5, int>(M5));
    [Benchmark] public async Task<int> Stream_8Behaviors() => await Consume(_mediator.CreateStream<SScale8, int>(M8));
}
