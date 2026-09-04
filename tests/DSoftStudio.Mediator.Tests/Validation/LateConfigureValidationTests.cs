// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Validation;

// ── Fixtures ──────────────────────────────────────────────────────
// Dependency-free: an assembly-wide test resolves every discovered handler, so a fixture with an
// unregistered constructor argument fails somebody else's test rather than its own.

public sealed record LatePing : IRequest<int>;

public sealed class LatePingHandler : IRequestHandler<LatePing, int>
{
    public ValueTask<int> Handle(LatePing r, CancellationToken ct) => new(3);
}

public sealed class LateBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(r, ct);
}

/// <summary>
/// A second <c>AddMediator(configure)</c> registers components that no chain is rebuilt around.
/// <para>
/// The generated overload runs <c>configure(builder)</c> unconditionally — it must, or the lambda would
/// be ignored — while <c>RegisterPipelineChains</c> returns early once its sentinel exists. So the
/// second call's components either never run, or, when the first scan produced a Singleton chain, are
/// captured by it and the container refuses to build under <c>ValidateScopes</c>.
/// </para>
/// <para>
/// This cannot be an analyzer rule: counting calls across a compilation would fire on every test file
/// that builds several collections, and across assemblies there is no ordering to consult. The sentinel
/// knows, because it exists at run time. What the tests below pin is that the check reports by
/// OBSERVATION and therefore has nothing to be wrong about — the three silent cases are the point of
/// the design, not an afterthought.
/// </para>
/// </summary>
public class LateConfigureValidationTests
{
    /// <summary>The words the report uses. Matching on them keeps these tests about THIS rule.</summary>
    private const string LateMessage = "AddMediator(configure) call that ran";

    private static IEnumerable<Exception> Validate(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();

        // Other handlers in this assembly need dependencies a minimal container does not register, so
        // validation reports things either way. Match on the message rather than on throwing.
        var aggregate = Record.Exception(() => provider.ValidateMediatorHandlers()) as AggregateException;
        return aggregate?.InnerExceptions ?? (IEnumerable<Exception>)[];
    }

    [Fact]
    public void A_second_configure_that_adds_a_component_is_reported()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));

        // The chains are frozen by now. This lambda's behavior gets none built for it.
        services.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));

        Validate(services).ShouldContain(
            e => e.Message.Contains(LateMessage),
            "the second call's component missed the scan, and nothing else would have said so");
    }

    [Fact]
    public void A_single_configure_is_silent()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));

        Validate(services).ShouldNotContain(e => e.Message.Contains(LateMessage));
    }

    /// <summary>
    /// The first silent case: a second call whose lambda registers nothing. The sentinel is present, so
    /// inference alone would report it; observing what was appended does not.
    /// </summary>
    [Fact]
    public void A_second_configure_that_adds_nothing_is_silent()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));

        services.AddMediator(_ => { });

        Validate(services).ShouldNotContain(e => e.Message.Contains(LateMessage));
    }

    /// <summary>
    /// The second silent case, and the reason the check tests service TYPES rather than counting
    /// descriptors: a notification publisher is not a pipeline component and no chain is built around it.
    /// </summary>
    [Fact]
    public void A_second_configure_that_only_sets_the_publisher_is_silent()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));

        services.AddMediator(b => b.AddParallelNotificationPublisher());

        Validate(services).ShouldNotContain(e => e.Message.Contains(LateMessage));
    }

    /// <summary>
    /// The third silent case: a second bare <c>PrecompilePipelines()</c> is documented as a no-op that
    /// returns early and changes nothing. It registers no component, so it stays a no-op here too.
    /// </summary>
    [Fact]
    public void A_second_precompile_alone_is_silent()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));

        services.PrecompilePipelines();

        Validate(services).ShouldNotContain(e => e.Message.Contains(LateMessage));
    }

    /// <summary>
    /// Two collections, one call each. The report rides on the collection it was recorded into, so a
    /// sibling cannot make this one look late — which is what a compilation-wide analyzer count could
    /// not have guaranteed.
    /// </summary>
    [Fact]
    public void One_call_each_on_two_collections_is_silent()
    {
        var first = new ServiceCollection();
        first.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));

        var second = new ServiceCollection();
        second.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));

        Validate(first).ShouldNotContain(e => e.Message.Contains(LateMessage));
        Validate(second).ShouldNotContain(e => e.Message.Contains(LateMessage));
    }

    /// <summary>The report names the component, so the message points at the registration to move.</summary>
    [Fact]
    public void The_report_names_the_component_service_type()
    {
        var services = new ServiceCollection();
        services.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));
        services.AddMediator(b => b.AddOpenBehavior(typeof(LateBehavior<,>)));

        Validate(services).ShouldContain(e =>
            e.Message.Contains(LateMessage) && e.Message.Contains(nameof(IPipelineBehavior<LatePing, int>)));
    }
}
