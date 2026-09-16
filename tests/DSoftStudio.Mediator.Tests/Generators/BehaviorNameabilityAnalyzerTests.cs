// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// <c>DSOFT011</c> — a pipeline behavior the generated registry cannot name.
/// <para>
/// The shapes below were measured before the rule existed, each as its own project published to a
/// native binary: a <c>file</c> behavior and a private nested one both leave the open registration
/// in place, both dispatch correctly on the ordinary runtime, and both kill the native binary on the
/// first request with a value-type response. Build and AOT publish reported zero warnings in every
/// case, because the reflection is the container's rather than the application's, so ILC has nothing
/// to complain about. This rule is the only place the developer is told.
/// </para>
/// </summary>
public class BehaviorNameabilityAnalyzerTests
{
    private const string Preamble = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public sealed record Ping : IRequest<int>;
        public sealed class PingHandler : IRequestHandler<Ping, int>
        { public ValueTask<int> Handle(Ping r, CancellationToken ct) => new(1); }

        """;

    private const string BehaviorBody = """
            {
                public ValueTask<TResponse> Handle(TRequest request,
                    IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                    => next.Handle(request, ct);
            }
        """;

    private static List<string> Run(string source)
    {
        var (result, _) = GeneratorTestHarness.Run<BehaviorNameabilityAnalyzer>(source);

        return result.Diagnostics
            .Where(d => d.Id == "DSOFT011")
            .Select(d => d.GetMessage())
            .ToList();
    }

    /// <summary>
    /// A warning nobody can navigate to is barely a warning. Location.Create hands the caller the
    /// LINE span, and a zero one puts every report at (1,1) — the squiggle on the first character of
    /// the file. Both sibling rules shipped that way; this one must not.
    /// </summary>
    [Fact]
    public void Reports_At_The_Declaration_Not_At_The_Top_Of_The_File()
    {
        var (result, _) = GeneratorTestHarness.Run<BehaviorNameabilityAnalyzer>(Preamble + """
            file sealed class Hidden<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            """ + BehaviorBody);

        var reported = result.Diagnostics.Where(d => d.Id == "DSOFT011").ToArray();

        reported.ShouldNotBeEmpty();
        reported[0].Location.GetLineSpan().StartLinePosition.Line.ShouldBeGreaterThan(0,
            "the behavior is declared well below the first line");
    }

    [Fact]
    public void Reports_A_File_Local_Behavior()
    {
        var diagnostics = Run(Preamble + """
            file sealed class Hidden<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            """ + BehaviorBody);

        diagnostics.Count.ShouldBe(1,
            "a file behavior keeps working and silently loses AOT compatibility — nobody chooses that by writing 'file'");
        diagnostics[0].ShouldContain("Hidden");
        diagnostics[0].ShouldContain("pipeline behavior");
    }

    [Fact]
    public void Reports_A_Private_Nested_Behavior()
    {
        var diagnostics = Run(Preamble + """
            public static class Host
            {
                private sealed class Nested<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                    where TRequest : IRequest<TResponse>
                {
                    public ValueTask<TResponse> Handle(TRequest request,
                        IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                        => next.Handle(request, ct);
                }
            }
            """);

        diagnostics.Count.ShouldBe(1);
        diagnostics[0].ShouldContain("Nested");
    }

    [Fact]
    public void Reports_A_Behavior_Nested_In_A_Generic_Type()
    {
        var diagnostics = Run(Preamble + """
            public static class Outer<T>
            {
                public sealed class Inner<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                    where TRequest : IRequest<TResponse>
                {
                    public ValueTask<TResponse> Handle(TRequest request,
                        IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                        => next.Handle(request, ct);
                }
            }
            """);

        diagnostics.Count.ShouldBe(1, "typeof(Outer<>.Inner<,>) is not something generated code can write");
    }

    [Fact]
    public void Reports_A_Stream_Behavior_Too()
    {
        var diagnostics = Run(Preamble + """
            file sealed class HiddenStream<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
                where TRequest : IStreamRequest<TResponse>
            {
                public System.Collections.Generic.IAsyncEnumerable<TResponse> Handle(TRequest request,
                    IStreamRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                    => next.Handle(request, ct);
            }
            """);

        diagnostics.Count.ShouldBe(1);
        diagnostics[0].ShouldContain("stream pipeline behavior");
    }

    /// <summary>
    /// The half that keeps the rule from becoming noise. An ordinary behavior is named perfectly
    /// well, and a type that implements none of the pipeline interfaces is nobody's business however
    /// it is declared.
    /// </summary>
    [Fact]
    public void Says_Nothing_About_A_Behavior_It_Can_Name()
    {
        Run(Preamble + """
            internal sealed class Ordinary<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            """ + BehaviorBody).ShouldBeEmpty();
    }

    [Fact]
    public void Says_Nothing_About_An_Unnameable_Type_That_Is_Not_A_Behavior()
    {
        Run(Preamble + """
            file sealed class JustAGenericPair<TFirst, TSecond>
            {
                public TFirst? First { get; init; }
                public TSecond? Second { get; init; }
            }
            """).ShouldBeEmpty();
    }

    /// <summary>
    /// A CLOSED behavior is registered by naming the closed type, so the container never constructs
    /// anything and none of this applies — reporting it would be a false alarm.
    /// </summary>
    [Fact]
    public void Says_Nothing_About_A_Closed_Behavior()
    {
        Run(Preamble + """
            file sealed class ClosedOnly : IPipelineBehavior<Ping, int>
            {
                public ValueTask<int> Handle(Ping request,
                    IRequestHandler<Ping, int> next, CancellationToken ct)
                    => next.Handle(request, ct);
            }
            """).ShouldBeEmpty();
    }
}
