// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// A dispatch call captured inside an <c>Expression&lt;...&gt;</c> must not be intercepted. An
/// interceptor attaches to a call site the compiler will emit; an expression tree is data, and there is
/// no call to attach to.
/// <para>
/// The scenario is Moq. <c>Setup(m =&gt; m.Send(...))</c> and <c>Verify(m =&gt; m.Publish(...))</c> are
/// expression trees, and they live exactly where people put them: on the mediator interfaces.
/// </para>
/// <para>
/// <b>Send had a test for this and it proved nothing.</b> GeneratorTestHarness did not reference
/// System.Linq.Expressions, and netstandard does not forward <c>Expression&lt;T&gt;</c>, so the type
/// never resolved — <c>IsInsideExpressionTreeLambda</c> compared against an error symbol and returned
/// false every time. The test passed because its fixture source failed to compile, which the assertion
/// does not read. Verified by mutation: replacing the guard with <c>if (false)</c> left it green.
/// Reported by the Pipeline Explorer team, who hit the same gap in their own copy of that helper.
/// </para>
/// <para>
/// Publish and CreateStream carry the same guard and had no test at all. They do now, here, so the
/// three live together and the next person adding a fourth interceptor finds the pattern.
/// </para>
/// </summary>
public class ExpressionTreeInterceptionTests
{
    [Fact]
    public void Publish_Inside_An_Expression_Tree_Is_Not_Intercepted()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Pinged(int N) : INotification;

            public static class Consumer
            {
                // The shape a Moq Verify takes.
                public static Expression<Func<IPublisher, Task>> Verify()
                    => publisher => publisher.Publish(new Pinged(1), default);
            }
            """;

        AssertNothingIntercepted<PublishInterceptorGenerator>(source, "Publish");
    }

    [Fact]
    public void CreateStream_Inside_An_Expression_Tree_Is_Not_Intercepted()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq.Expressions;
            using System.Threading;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ticks(int N) : IStreamRequest<int>;

            public static class Consumer
            {
                public static Expression<Func<IMediator, IAsyncEnumerable<int>>> Setup()
                    => mediator => mediator.CreateStream<Ticks, int>(new Ticks(1), default);
            }
            """;

        AssertNothingIntercepted<StreamInterceptorGenerator>(source, "CreateStream");
    }

    /// <summary>
    /// Guards the guard: the harness must be able to resolve <c>Expression&lt;T&gt;</c>, or every test
    /// above passes for the wrong reason. Asserted directly rather than trusted, because the failure
    /// mode is silence.
    /// </summary>
    [Fact]
    public void The_Harness_Can_Resolve_Expression_Of_T()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;

            namespace TestApp;

            public static class Probe
            {
                public static Expression<Func<int, int>> Identity() => x => x;
            }
            """;

        var (_, output) = GeneratorTestHarness.Run<SendInterceptorGenerator>(source, interceptors: true);

        var unresolved = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => d.Id is "CS0246" or "CS0234" or "CS0012")
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .Distinct()
            .ToList();

        unresolved.ShouldBeEmpty(
            customMessage:
            "Expression<T> does not resolve in the test harness, so IsInsideExpressionTreeLambda " +
            "compares against an error symbol and returns false for everything. Every expression-tree " +
            "test then passes while the guard never runs. Got: " + string.Join(" | ", unresolved));
    }

    private static void AssertNothingIntercepted<TGenerator>(string source, string operation)
        where TGenerator : IIncrementalGenerator, new()
    {
        var (result, _) = GeneratorTestHarness.Run<TGenerator>(source, interceptors: true);

        result.GeneratedSources
            .SelectMany(s => s.SourceText.ToString().Split('\n'))
            .Where(l => l.Contains("InterceptsLocation"))
            .ShouldBeEmpty($"{operation} inside an expression-tree lambda must not be intercepted");
    }
}
