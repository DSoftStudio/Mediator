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
}
