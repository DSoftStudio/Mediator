// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// The order of the steps inside the generated <c>AddMediator(configure)</c> is a contract, not an
/// implementation detail, and this is the test that says so.
/// <para>
/// <c>configure(builder)</c> has to run BEFORE the pipeline chains are registered and dispatch is
/// frozen. That window is the only place an open-generic component still counts: once
/// <c>RegisterPipelineChains</c> has closed the open generics over the discovered pairs, anything
/// registered afterwards belongs to no chain.
/// </para>
/// <para>
/// <b>Why a test rather than a comment.</b> Reordering these lines produces no error anywhere. The
/// library builds, the consumer builds, the container builds — and every request silently loses the
/// components registered in the lambda. A signature change breaks a caller's compile; an ordering
/// change breaks nothing until someone notices a behavior that never ran.
/// </para>
/// <para>
/// The Pipeline Explorer team depends on this window from outside the repository: they register their
/// always-on components inside the lambda for exactly the reason above, and capture afterwards to
/// inherit the closed descriptors. They asked to be told if it moves. This is better than telling
/// them — it means it cannot move quietly.
/// </para>
/// </summary>
public class GeneratedRegistrationOrderTests
{
    private const string MinimalApp = """
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

    [Fact]
    public void Configure_Runs_Before_The_Chains_Are_Registered_And_Dispatch_Is_Frozen()
    {
        var generated = AddMediatorConfigureBody();

        var configure = generated.IndexOf("configure(builder);", StringComparison.Ordinal);
        var chains = generated.IndexOf("RegisterPipelineChains(services);", StringComparison.Ordinal);
        var freeze = generated.IndexOf("RequestObjectDispatch.Freeze();", StringComparison.Ordinal);

        configure.ShouldBeGreaterThan(-1, "the generated overload no longer calls configure(builder)");
        chains.ShouldBeGreaterThan(-1, "the generated overload no longer calls RegisterPipelineChains");
        freeze.ShouldBeGreaterThan(-1, "the generated overload no longer freezes dispatch");

        configure.ShouldBeLessThan(chains,
            "configure(builder) must run before RegisterPipelineChains. After it, the open generics " +
            "have already been closed over the discovered pairs, so a component registered in the " +
            "lambda joins no chain — and nothing reports it.");

        configure.ShouldBeLessThan(freeze,
            "configure(builder) must run before dispatch is frozen.");
    }

    [Fact]
    public void Handlers_Are_Registered_Before_Configure_Sees_The_Collection()
    {
        // The other half of the window. A component registered in the lambda is closed over the
        // DISCOVERED pairs, so discovery has to have happened by then or there is nothing to close over.
        var generated = AddMediatorConfigureBody();

        var register = generated.IndexOf("services.RegisterMediatorHandlers();", StringComparison.Ordinal);
        var configure = generated.IndexOf("configure(builder);", StringComparison.Ordinal);

        register.ShouldBeGreaterThan(-1);
        register.ShouldBeLessThan(configure,
            "handlers must be discovered before the lambda runs, or a component registered there has " +
            "no pairs to be closed over");
    }

    [Fact]
    public void All_Three_Dispatch_Tables_Are_Armed()
    {
        // Documented in the README as the reason to prefer the builder overload over the manual chain,
        // where forgetting PrecompileNotifications leaves Publish reaching no handlers in silence.
        var generated = AddMediatorConfigureBody();

        generated.ShouldContain("RegisterPipelineChains(services);");
        generated.ShouldContain("services.PrecompileNotifications();");
        generated.ShouldContain("services.PrecompileStreams();");
    }

    /// <summary>
    /// The body of the generated <c>AddMediator(configure)</c> overload, and nothing else.
    /// </summary>
    /// <remarks>
    /// The first version of these tests searched the whole generated output. RegisterPipelineChains
    /// appears in PrecompilePipelines() too, so IndexOf found an occurrence in a different method and
    /// the comparison was between two unrelated positions. It failed rather than passing by luck, which
    /// is the only reason it was caught — the same search written the other way round would have been
    /// green and meaningless.
    /// </remarks>
    private static string AddMediatorConfigureBody()
    {
        var all = GeneratorTestHarness.Run<MediatorPipelineGenerator>(MinimalApp).Result.AllSource();

        var start = all.IndexOf("global::System.Action<global::DSoftStudio.Mediator.MediatorBuilder> configure)",
            StringComparison.Ordinal);
        start.ShouldBeGreaterThan(-1, "the generated AddMediator(configure) overload is gone");

        // To the next method declaration, or the end. Enough to isolate one body without parsing C#.
        var end = all.IndexOf("        public static", start + 1, StringComparison.Ordinal);
        return end > start ? all[start..end] : all[start..];
    }

}
