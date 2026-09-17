// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

/// <summary>
/// Every constant on <see cref="MediatorCapabilities"/> is a promise to code we do not own, so each one
/// is tied here to the behaviour it claims.
/// <para>
/// The point is not to assert that the constant has a value — that cannot fail. It is that the
/// constant and the behaviour move together. A marker left declaring a capability the core has since
/// dropped is worse than no marker at all: a downstream generator stops inferring, which might be
/// wrong, and starts trusting, which is definitely wrong.
/// </para>
/// </summary>
public class MediatorCapabilitiesTests
{
    /// <summary>
    /// The fold this constant advertises, asserted through its effect on the chain descriptor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verified by mutation, and the mutation is worth recording because two obvious ones prove
    /// nothing here. Emptying <c>HandlerLifetimeOptimizer.Apply</c> does not fail this: it decides the
    /// HANDLER lifetime, not the chain's. Nor does removing one of the three sites that set
    /// <c>hasTransientChainDependency</c> in the generated code — the other two still set it.
    /// </para>
    /// <para>
    /// What does fail it is removing the branch that ACTS on that flag, so the chain no longer
    /// inherits the handler's Transient and falls through to Scoped. That is the capability itself,
    /// rather than one of its inputs.
    /// </para>
    /// </remarks>
    [Fact]
    public void ChainLifetimeFold_Declares_A_Fold_That_Actually_Happens()
    {
        MediatorCapabilities.ChainLifetimeFold.ShouldBeGreaterThanOrEqualTo(1);

        var services = new ServiceCollection();
        services.AddTransient<CoverageTransientDep>();

        // A component, so a chain exists at all. With no pipeline component the pair is dispatched
        // straight to its handler and there is no PipelineChainHandler descriptor to read a lifetime
        // from -- which is correct behaviour, and would have made this test throw rather than assert.
        services.AddMediator(b => b.AddRequestPreProcessor<FoldScopedPreProcessor>(ServiceLifetime.Scoped));

        services.Last(d => d.ServiceType == typeof(IRequestHandler<FoldTransientPing, int>))
            .Lifetime.ShouldBe(
                ServiceLifetime.Transient,
                "the premise: this handler stays Transient, so the fold has something to read");

        services.Last(d => d.ServiceType == typeof(PipelineChainHandler<FoldTransientPing, int>))
            .Lifetime.ShouldBe(
                ServiceLifetime.Transient,
                "MediatorCapabilities.ChainLifetimeFold says the handler's lifetime constrains the " +
                "chain's. If this is Scoped, the constant is advertising a capability this build does " +
                "not have, and a downstream generator will register Singleton components against it.");
    }

    /// <summary>
    /// The marker has to stay reachable the way a generator reaches it: a public constant on a public
    /// type, in the assembly a composition root already references. Renaming either, or making the
    /// field non-const, breaks a reader that cannot be recompiled from here.
    /// </summary>
    [Fact]
    public void The_Marker_Stays_Readable_From_Outside()
    {
        var type = typeof(MediatorCapabilities);

        type.FullName.ShouldBe("DSoftStudio.Mediator.MediatorCapabilities");
        type.IsPublic.ShouldBeTrue();

        var field = type.GetField(nameof(MediatorCapabilities.ChainLifetimeFold));

        field.ShouldNotBeNull("a generator looks this up by name");
        field!.IsLiteral.ShouldBeTrue(
            "it must be a const: a generator reads IFieldSymbol.ConstantValue, which is null for a " +
            "static readonly field");
        field.GetValue(null).ShouldBeOfType<int>(
            "an int, so a capability can advance without needing a new name");
    }
}
