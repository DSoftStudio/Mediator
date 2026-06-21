// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Drives the real <see cref="StreamInterceptorGenerator"/> in-memory. It intercepts
/// <c>IMediator.CreateStream&lt;TRequest, TResponse&gt;()</c> call sites to skip the interface dispatch
/// and delegate indirection on the stream hot path — it had zero coverage before this.
/// </summary>
public class StreamInterceptorGeneratorTests
{
    private const string CreateStreamCallSite = """
        using System.Collections.Generic;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record Ticker(int N) : IStreamRequest<int>;

        public static class Consumer
        {
            public static IAsyncEnumerable<int> Run(IMediator mediator)
                => mediator.CreateStream<Ticker, int>(new Ticker(3));
        }
        """;

    [Fact]
    public void Emits_Interceptor_For_CreateStream_CallSite()
    {
        // interceptors: true → InterceptorsNamespaces feature set, otherwise the generated
        // [InterceptsLocation] methods would be rejected with CS9137.
        var (result, output) = GeneratorTestHarness.Run<StreamInterceptorGenerator>(
            CreateStreamCallSite, interceptors: true);

        var code = result.AllSource();

        result.GeneratedSources.ShouldNotBeEmpty(
            "StreamInterceptorGenerator should intercept the mediator.CreateStream<Ticker, int>() call site");
        code.ShouldContain("InterceptsLocation");
        code.ShouldContain("DSoftStudio.Mediator.Generated");

        // The interceptor wiring must compile cleanly (no CS9137) with the feature flag on.
        output.GetDiagnostics().Where(d => d.Id == "CS9137").ShouldBeEmpty();
    }

    [Fact]
    public void Intercepts_CreateStream_On_Release_Build()
    {
        // Release flips OptimizationLevel → exercises the Release emit branch of the interceptor generator.
        var (result, output) = GeneratorTestHarness.Run<StreamInterceptorGenerator>(
            CreateStreamCallSite, interceptors: true, release: true);

        result.GeneratedSources.ShouldNotBeEmpty();
        result.AllSource().ShouldContain("InterceptsLocation");
        output.GetDiagnostics().Where(d => d.Id == "CS9137").ShouldBeEmpty();
    }

    [Fact]
    public void Intercepts_Type_Inferred_CreateStream_Via_Generated_Extension()
    {
        // `mediator.CreateStream(request)` (no <Ticker,int>) only BINDS once MediatorExtensionsGenerator has
        // emitted the typed `CreateStream(this IMediator, Ticker)` extension (TResponse can't be inferred from
        // the open IMediator.CreateStream<TRequest,TResponse> alone). Running both generators in sequence, the
        // inferred call binds and is intercepted — exercising the inferred type-resolution path. This is the real
        // two-generator build scenario; a single-generator run would never reach it.
        const string inferred = """
            using System.Collections.Generic;
            using System.Threading;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ticker(int N) : IStreamRequest<int>;

            public sealed class TickerHandler : IStreamRequestHandler<Ticker, int>
            {
                public IAsyncEnumerable<int> Handle(Ticker request, CancellationToken ct) => null!;
            }

            public static class Consumer
            {
                public static IAsyncEnumerable<int> Run(IMediator mediator) => mediator.CreateStream(new Ticker(3));
            }
            """;

        var (result, _) = GeneratorTestHarness.RunChain<MediatorExtensionsGenerator, StreamInterceptorGenerator>(
            inferred, interceptors: true);

        result.AllSource().ShouldContain("InterceptsLocation");
    }

    [Fact]
    public void Ignores_CreateStream_On_Non_Mediator_Type()
    {
        // A CreateStream<,> method on a type that is NOT IMediator must not be intercepted (the generator
        // verifies the receiver implements IMediator). Covers the receiver-type exclusion branch.
        const string nonMediator = """
            using System.Collections.Generic;

            namespace TestApp;

            public sealed class Faker
            {
                public IAsyncEnumerable<int> CreateStream<TRequest, TResponse>(TRequest r) => null!;
            }

            public static class Consumer
            {
                public static IAsyncEnumerable<int> Run(Faker f) => f.CreateStream<int, int>(0);
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<StreamInterceptorGenerator>(nonMediator, interceptors: true);

        result.GeneratedSources
            .SelectMany(s => s.SourceText.ToString().Split('\n'))
            .Where(l => l.Contains("InterceptsLocation"))
            .ShouldBeEmpty("CreateStream on a non-IMediator type must not be intercepted");
    }

    [Fact]
    public void Does_Not_Emit_When_No_CreateStream_CallSite()
    {
        // A stream request type with no CreateStream() invocation → nothing to intercept.
        const string noCallSite = """
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ticker(int N) : IStreamRequest<int>;
            """;

        var (result, _) = GeneratorTestHarness.Run<StreamInterceptorGenerator>(noCallSite, interceptors: true);

        result.GeneratedSources
            .SelectMany(s => s.SourceText.ToString().Split('\n'))
            .Where(line => line.Contains("InterceptsLocation"))
            .ShouldBeEmpty("no CreateStream call site → no interceptor methods");
    }

    [Fact]
    public void Ignores_Open_Generic_CreateStream_Call_Site()
    {
        // A generic forwarding method — mediator.CreateStream<TRequest, TResponse>(request) with OPEN type
        // parameters — cannot be intercepted; the call dispatches through Mediator.CreateStream at runtime. The
        // generator must skip it. Regression: it used to emit an interceptor referencing TRequest/TResponse → CS0246.
        const string openGeneric = """
            using System.Collections.Generic;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public static class Dispatcher
            {
                public static IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(IMediator mediator, TRequest request)
                    where TRequest : IStreamRequest<TResponse>
                    => mediator.CreateStream<TRequest, TResponse>(request);
            }
            """;

        var (result, output) = GeneratorTestHarness.Run<StreamInterceptorGenerator>(openGeneric, interceptors: true);

        result.AllSource().ShouldNotContain("InterceptsLocation",
            customMessage: "an open-generic CreateStream call site must not be intercepted");
        output.GetDiagnostics().Where(d => d.Id == "CS0246").ShouldBeEmpty(
            "the generated interceptor must not reference unbound type parameters");
    }
}
