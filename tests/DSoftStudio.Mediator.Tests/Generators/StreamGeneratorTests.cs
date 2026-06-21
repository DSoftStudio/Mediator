// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Drives the real <see cref="StreamGenerator"/> in-memory and asserts the generated
/// <c>StreamRegistry.g.cs</c> — the stream registration path had zero coverage before this.
/// </summary>
public class StreamGeneratorTests
{
    private const string StreamHandler = """
        using System.Collections.Generic;
        using System.Threading;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record Countdown(int From) : IStreamRequest<int>;

        public sealed class CountdownHandler : IStreamRequestHandler<Countdown, int>
        {
            // Body is irrelevant to the generator (it works off the symbol's interfaces); null! keeps it terse.
            public IAsyncEnumerable<int> Handle(Countdown request, CancellationToken ct) => null!;
        }
        """;

    [Fact]
    public void Generates_StreamRegistry_With_Handler_Registration()
    {
        var (result, _) = GeneratorTestHarness.Run<StreamGenerator>(StreamHandler);
        var code = result.AllSource();

        code.ShouldContain("StreamRegistry");
        code.ShouldContain("PrecompileStreams");
        code.ShouldContain("TryInitializeHandler");      // handler factory wired
        code.ShouldContain("RegisterStreamPipeline");    // per-handler pipeline registration
        code.ShouldContain("Countdown");                 // the discovered stream request flows into the registry
    }

    [Fact]
    public void Emits_OpenGeneric_Behavior_Closure_When_StreamBehavior_Present()
    {
        // A local open-generic IStreamPipelineBehavior<,> alongside a handler triggers the AOT-safe
        // closure-emit path (CloseAllOpenGenericStreamBehaviors / RemoveOpenGenericStreamBehaviorDescriptors).
        const string withBehavior = """
            using System.Collections.Generic;
            using System.Threading;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Countdown(int From) : IStreamRequest<int>;

            public sealed class CountdownHandler : IStreamRequestHandler<Countdown, int>
            {
                public IAsyncEnumerable<int> Handle(Countdown request, CancellationToken ct) => null!;
            }

            public sealed class LoggingStreamBehavior<TRequest, TResponse>
                : IStreamPipelineBehavior<TRequest, TResponse>
                where TRequest : IStreamRequest<TResponse>
            {
                public IAsyncEnumerable<TResponse> Handle(
                    TRequest request,
                    IStreamRequestHandler<TRequest, TResponse> next,
                    CancellationToken ct) => next.Handle(request, ct);
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<StreamGenerator>(withBehavior);
        var code = result.AllSource();

        code.ShouldContain("CloseAllOpenGenericStreamBehaviors");
        code.ShouldContain("RemoveOpenGenericStreamBehaviorDescriptors");
        code.ShouldContain("LoggingStreamBehavior");
    }

    [Fact]
    public void Generates_Empty_Registry_When_No_Stream_Handlers()
    {
        // No stream handler anywhere: the registry skeleton (+ PrecompileStreams entry point) is still
        // emitted, but with no per-handler registration line — covers the empty-registrations branch.
        const string noStream = """
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ping : IRequest<string>;
            """;

        var (result, _) = GeneratorTestHarness.Run<StreamGenerator>(noStream);
        var code = result.AllSource();

        code.ShouldContain("StreamRegistry");
        code.ShouldContain("PrecompileStreams");
        code.ShouldNotContain("TryInitializeHandler");
    }
}
