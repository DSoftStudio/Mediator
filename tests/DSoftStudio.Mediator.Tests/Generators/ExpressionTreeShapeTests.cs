// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// The shapes <c>IsInsideExpressionTreeLambda</c> has to answer yes or no to.
/// <para>
/// The list came from the Pipeline Explorer team, who keep a copy of that method and wanted the
/// behaviour pinned rather than the implementation shared. Their observation is the reason this file
/// exists: every test that covered the guard asserted it says <b>no</b>, and none asserted it can say
/// <b>yes</b>. A guard that answers no to everything passes all of them while silently dropping every
/// interception in the application — dispatch still works, it just goes the slow way, and nothing
/// reports it.
/// </para>
/// </summary>
public class ExpressionTreeShapeTests
{
    private const string Preamble = """
        using System;
        using System.Linq;
        using System.Linq.Expressions;
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record Ping(int N) : IRequest<int>;

        public sealed class PingHandler : IRequestHandler<Ping, int>
        {
            public ValueTask<int> Handle(Ping request, CancellationToken ct) => new(request.N);
        }

        """;

    // ── Must be REFUSED: the call is data, not a call ─────────────────

    [Theory]
    [InlineData("Expression<Func<>>", """
        public static class C
        {
            public static Expression<Func<ISender, ValueTask<int>>> M()
                => s => s.Send<Ping, int>(new Ping(1));
        }
        """)]
    [InlineData("Expression<Action<>>", """
        public static class C
        {
            public static Expression<Action<ISender>> M()
                => s => s.Send<Ping, int>(new Ping(1));
        }
        """)]
    [InlineData("lambda nested inside the tree", """
        public static class C
        {
            public static Expression<Func<ISender, Func<int>>> M()
                => s => () => s.Send<Ping, int>(new Ping(1)).Result;
        }
        """)]
    [InlineData("tree built inside a normal lambda", """
        public static class C
        {
            public static Func<Expression<Func<ISender, ValueTask<int>>>> M()
                => () => s => s.Send<Ping, int>(new Ping(1));
        }
        """)]
    [InlineData("conditional access inside the tree", """
        public static class C
        {
            public static Expression<Func<ISender, ValueTask<int>?>> M()
                => s => s == null ? (ValueTask<int>?)null : s.Send<Ping, int>(new Ping(1));
        }
        """)]
    [InlineData("LINQ over IQueryable", """
        public static class C
        {
            public static IQueryable<int> M(IQueryable<int> source, ISender s)
                => source.Where(x => s.Send<Ping, int>(new Ping(x)).Result > 0);
        }
        """)]
    public void Refused(string shape, string body)
    {
        var (result, _) = GeneratorTestHarness.Run<SendInterceptorGenerator>(Preamble + body, interceptors: true);

        Interceptions(result).ShouldBe(0, $"{shape}: an interceptor cannot attach to an expression tree");
    }

    // ── Must be ACCEPTED: an ordinary delegate is an ordinary call ────

    /// <summary>
    /// A statement lambda converted to a delegate is not an expression tree, and the call inside it is
    /// a real call site the compiler will emit.
    /// </summary>
    /// <remarks>
    /// This is the case the Pipeline Explorer team singled out, and they were right that nothing
    /// covered it. Every other test here passes if the guard simply answers "yes, it is a tree" to
    /// everything — and so does the application, which keeps working through the runtime path while
    /// every interceptor quietly disappears. The failure is invisible: no error, no diagnostic, just
    /// dispatch that stopped taking the fast route.
    /// </remarks>
    [Theory]
    [InlineData("statement lambda", """
        public static class C
        {
            public static Func<ISender, ValueTask<int>> M()
                => s => { return s.Send<Ping, int>(new Ping(1)); };
        }
        """)]
    [InlineData("expression lambda to a delegate", """
        public static class C
        {
            public static Func<ISender, ValueTask<int>> M()
                => s => s.Send<Ping, int>(new Ping(1));
        }
        """)]
    [InlineData("plain method body", """
        public static class C
        {
            public static ValueTask<int> M(ISender s) => s.Send<Ping, int>(new Ping(1));
        }
        """)]
    public void Accepted(string shape, string body)
    {
        var (result, _) = GeneratorTestHarness.Run<SendInterceptorGenerator>(Preamble + body, interceptors: true);

        Interceptions(result).ShouldBeGreaterThan(0,
            $"{shape}: this is a real call site and must be intercepted. A guard that answers " +
            "'expression tree' to everything passes every other test in this file while silently " +
            "removing every interception in the application.");
    }

    private static int Interceptions(GeneratorRunResult result)
        => result.GeneratedSources
            .SelectMany(s => s.SourceText.ToString().Split('\n'))
            .Count(l => l.Contains("InterceptsLocation"));
}
