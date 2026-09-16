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
}
