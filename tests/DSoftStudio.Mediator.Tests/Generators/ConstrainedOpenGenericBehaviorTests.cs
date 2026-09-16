// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// A behavior that narrows itself with its own constraint is the idiomatic way to apply one to a
/// subset of requests:
/// <code>
///   class AuditOnly&lt;TReq, TRes&gt; : IPipelineBehavior&lt;TReq, TRes&gt; where TReq : IAuditable
///   services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(AuditOnly&lt;,&gt;));
/// </code>
/// <para>
/// MSDI's own semantics for that are "apply to the pairs that satisfy the constraint" — measured:
/// GetServices returns one behavior for a satisfying pair and zero for a non-satisfying one, with no
/// exception. Both of this generator's paths have to agree with that.
/// </para>
/// <para>
/// Chain PREDICTION did not. It treats any open-generic registration as applying to every pair, so
/// it emitted a chain link naming <c>AuditOnly&lt;NonSatisfyingRequest, int&gt;</c> — CS0311 in a
/// generated file the consumer cannot edit. Registration CLOSING got this right by refusing to
/// handle constrained behaviors at all, which is why only prediction broke.
/// </para>
/// </summary>
public class ConstrainedOpenGenericBehaviorTests
{
    /// <summary>
    /// Two pairs, one satisfying the behavior's extra constraint and one not, with the behavior
    /// registered as an open generic — the shape that has to compile.
    /// </summary>
    private const string ConstrainedBehavior = """
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using DSoftStudio.Mediator;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public interface IAuditable { }

        public sealed record Audited : IRequest<int>, IAuditable;
        public sealed record Plain : IRequest<int>;

        public sealed class AuditedHandler : IRequestHandler<Audited, int>
        {
            public ValueTask<int> Handle(Audited request, CancellationToken ct) => new(1);
        }

        public sealed class PlainHandler : IRequestHandler<Plain, int>
        {
            public ValueTask<int> Handle(Plain request, CancellationToken ct) => new(2);
        }

        // The constraint on TRequest goes BEYOND what IPipelineBehavior itself requires.
        public sealed class AuditOnly<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
            where TRequest : IRequest<TResponse>, IAuditable
        {
            public ValueTask<TResponse> Handle(TRequest request,
                IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                => next.Handle(request, ct);
        }

        public static class Startup
        {
            public static void Configure(IServiceCollection services)
            {
                services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuditOnly<,>));
            }
        }
        """;

    [Fact]
    public void Constrained_Open_Generic_Behavior_Does_Not_Break_The_Consumer_Build()
    {
        var (_, output) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(ConstrainedBehavior);

        // Constraint-violation diagnostics only. Running ONE generator in isolation legitimately
        // leaves the other generators' extension methods undefined (CS1061), which says nothing
        // about this bug and would mask it behind noise.
        string[] constraintViolations =
        [
            "CS0310", // no public parameterless constructor — new() constraint
            "CS0311", // no implicit reference conversion
            "CS0314", // no boxing or type-parameter conversion
            "CS0315", // boxing conversion
            "CS0452", // not a reference type — class constraint
            "CS0453", // not a non-nullable value type — struct constraint
        ];

        var errors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error && constraintViolations.Contains(d.Id))
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .Distinct()
            .ToList();

        errors.ShouldBeEmpty(
            customMessage:
            "the generated registry does not compile. A behavior may only be named closed over a pair " +
            "that satisfies its constraints — MSDI skips the others rather than resolving them, so " +
            $"naming them is wrong as well as uncompilable. Got: {string.Join(" | ", errors)}");
    }

    /// <summary>
    /// The other half of the contract: narrowing must not become "never applies". The behavior still
    /// has to be named for the pair that DOES satisfy it, or the fix would have bought a clean build
    /// by silently dropping the pipeline.
    /// </summary>
    [Fact]
    public void Satisfying_Pair_Still_Gets_The_Behavior()
    {
        var (result, _) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(ConstrainedBehavior);
        var code = result.AllSource();

        code.ShouldContain("AuditOnly<global::TestApp.Audited, int>",
            customMessage: "the satisfying pair lost its behavior");
        code.ShouldNotContain("AuditOnly<global::TestApp.Plain, int>",
            customMessage: "the non-satisfying pair was named anyway");
    }

    /// <summary>
    /// Shapes an earlier version of the constraint check got wrong. Each one reached a consumer as a
    /// broken build or a dead generator, so each gets its own case rather than a shared fixture.
    /// </summary>
    public static string WithConstraint(string constraint, string extraTypes = "") => $$"""
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using DSoftStudio.Mediator;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        {{extraTypes}}

        public sealed record TextRequest : IRequest<string>;
        public sealed record NumberRequest : IRequest<int>;
        // An ANNOTATED response. Without one, a notnull constraint has nothing to be wrong about.
        public sealed record MaybeRequest : IRequest<string?>;

        public sealed class TextHandler : IRequestHandler<TextRequest, string>
        { public ValueTask<string> Handle(TextRequest r, CancellationToken ct) => new("x"); }
        public sealed class NumberHandler : IRequestHandler<NumberRequest, int>
        { public ValueTask<int> Handle(NumberRequest r, CancellationToken ct) => new(1); }
        public sealed class MaybeHandler : IRequestHandler<MaybeRequest, string?>
        { public ValueTask<string?> Handle(MaybeRequest r, CancellationToken ct) => new((string?)null); }

        public sealed class TheBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
            where TRequest : IRequest<TResponse>
            {{constraint}}
        {
            public ValueTask<TResponse> Handle(TRequest request,
                IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                => next.Handle(request, ct);
        }

        public static class Startup
        {
            public static void Configure(IServiceCollection services)
                => services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TheBehavior<,>));
        }
        """;

    private static (System.Collections.Generic.List<string> Errors, string Code) RunWith(
        string constraint, string extraTypes = "")
    {
        var (result, output) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(
            WithConstraint(constraint, extraTypes));

        string[] constraintViolations =
            ["CS0310", "CS0311", "CS0314", "CS0315", "CS0452", "CS0453", "CS8714"];

        var errors = output.GetDiagnostics()
            .Where(d => constraintViolations.Contains(d.Id))
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .Distinct()
            .ToList();

        return (errors, result.AllSource());
    }

    /// <summary>
    /// A user-defined implicit conversion is an implicit conversion that generic constraints still
    /// reject. Accepting every implicit conversion named the int pair and emitted CS0315.
    /// </summary>
    [Fact]
    public void User_Defined_Conversion_Does_Not_Count_As_Satisfying_A_Constraint()
    {
        // Money must NOT be sealed — a sealed type is not a legal constraint at all (CS0701), and a
        // constraint the compiler already rejected tells us nothing about the check under test.
        var (errors, code) = RunWith(
            "where TResponse : Money",
            "public class Money { public static implicit operator Money(int v) => new(); }");

        // Not vacuous: the pair the implicit operator tempts the check with must be the one left
        // unnamed. "TheBehavior<" alone would match typeof(TheBehavior<,>), which is legal.
        code.ShouldNotContain("TheBehavior<global::TestApp.NumberRequest, int>",
            customMessage: "the int pair was named despite only a user-defined conversion");

        errors.ShouldBeEmpty(
            customMessage: $"an implicit operator is not a constraint conversion. Got: {string.Join(" | ", errors)}");
    }

    /// <summary>
    /// Roslyn reports IsGenericType for a non-generic type nested in a generic one, while its type
    /// argument list is empty. Constructing from that threw and took the entire generator down.
    /// </summary>
    [Fact]
    public void Constraint_On_A_Type_Nested_In_A_Generic_Does_Not_Kill_The_Generator()
    {
        var (result, _) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(
            WithConstraint(
                "where TResponse : Holder<int>.Marker",
                "public static class Holder<T> { public class Marker { } }"));

        result.Exception.ShouldBeNull("the generator crashed instead of declining to decide");
        result.GeneratedSources.Length.ShouldBeGreaterThan(0,
            customMessage: "the generator produced nothing, so the consumer loses PrecompilePipelines");
    }

    /// <summary>
    /// notnull is about nullable annotations, which survive into the emitted name even though MSDI
    /// erases them. Naming an annotated argument under it is CS8714 for the consumer.
    /// </summary>
    [Fact]
    public void NotNull_Constraint_Does_Not_Name_An_Annotated_Argument()
    {
        var (errors, code) = RunWith("where TResponse : notnull");

        // The annotated pair is the one that must stay unnamed; the others still get the behavior.
        code.ShouldNotContain("TheBehavior<global::TestApp.MaybeRequest, string?>",
            customMessage: "an annotated argument was named under a notnull constraint");
        errors.ShouldBeEmpty(
            customMessage: $"nullability was ignored when choosing what to name. Got: {string.Join(" | ", errors)}");
    }

    /// <summary>
    /// A struct constraint has to split the pairs the way the container splits them.
    /// </summary>
    [Fact]
    public void Struct_Constraint_Selects_Only_The_Value_Type_Pair()
    {
        var (errors, code) = RunWith("where TResponse : struct");

        errors.ShouldBeEmpty();
        code.ShouldContain("TheBehavior<global::TestApp.NumberRequest, int>");
        code.ShouldNotContain("TheBehavior<global::TestApp.TextRequest, string>");
    }
}
