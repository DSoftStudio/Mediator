// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Incremental generator that detects mixed usage of the mediator registration APIs.
/// <para>
/// <c>AddMediator(Action&lt;MediatorBuilder&gt;)</c> is a single entry point that
/// registers core services, handlers, and precompiled pipelines in one call.
/// Calling <c>RegisterMediatorHandlers()</c> or <c>PrecompilePipelines()</c>
/// separately when using the builder overload causes double registration.
/// </para>
/// <para>
/// Emits <c>DSOFT007</c> when both the builder overload and individual
/// registration methods are detected in the same compilation.
/// </para>
/// </summary>
[Generator]
public sealed class MixedRegistrationApiAnalyzer : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Scan all invocation expressions in user code (non-generated) for
        // mediator registration method calls.
        var registrationCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsRegistrationCandidate(node),
                transform: static (ctx, ct) =>
                {
                    var invocation = (InvocationExpressionSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol as IMethodSymbol;
                    if (symbol is null)
                        return default;

                    var name = symbol.Name;

                    if (name == "AddMediator" && HasMediatorBuilderParameter(symbol))
                    {
                        return new RegistrationCall(RegistrationCallKind.BuilderOverload, default);
                    }

                    if (name == "RegisterMediatorHandlers")
                    {
                        return new RegistrationCall(
                            RegistrationCallKind.RegisterHandlers,
                            invocation.GetLocation());
                    }

                    if (name == "PrecompilePipelines")
                    {
                        return new RegistrationCall(
                            RegistrationCallKind.PrecompilePipelines,
                            invocation.GetLocation());
                    }

                    return default;
                })
            .Where(static c => c.Kind != RegistrationCallKind.None);

        var collected = registrationCalls.Collect();

        context.RegisterSourceOutput(collected, static (spc, calls) =>
        {
            if (calls.IsDefaultOrEmpty)
                return;

            bool hasBuilderOverload = false;

            foreach (var call in calls)
            {
                if (call.Kind == RegistrationCallKind.BuilderOverload)
                {
                    hasBuilderOverload = true;
                    break;
                }
            }

            if (!hasBuilderOverload)
                return;

            // Report DSOFT007 on each redundant individual call.
            foreach (var call in calls)
            {
                switch (call.Kind)
                {
                    case RegistrationCallKind.RegisterHandlers:
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.MixedRegistrationApi,
                            call.Location,
                            "RegisterMediatorHandlers()",
                            "registers handlers"));
                        break;

                    case RegistrationCallKind.PrecompilePipelines:
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.MixedRegistrationApi,
                            call.Location,
                            "PrecompilePipelines()",
                            "precompiles pipelines"));
                        break;
                }
            }
        });
    }

    /// <summary>
    /// Fast syntactic filter: matches invocations of AddMediator,
    /// RegisterMediatorHandlers, or PrecompilePipelines.
    /// </summary>
    private static bool IsRegistrationCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        string? name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };

        return name is "AddMediator" or "RegisterMediatorHandlers" or "PrecompilePipelines";
    }

    /// <summary>
    /// Checks whether the method has an <c>Action&lt;MediatorBuilder&gt;</c> parameter,
    /// identifying the builder overload of <c>AddMediator</c>.
    /// Works for both reduced extension method form and static invocation form.
    /// </summary>
    private static bool HasMediatorBuilderParameter(IMethodSymbol method)
    {
        foreach (var param in method.Parameters)
        {
            if (param.Type is INamedTypeSymbol { Name: "Action", TypeArguments.Length: 1 } actionType
                && actionType.TypeArguments[0].Name == "MediatorBuilder")
            {
                return true;
            }
        }

        return false;
    }

    private enum RegistrationCallKind : byte
    {
        None = 0,
        BuilderOverload,
        RegisterHandlers,
        PrecompilePipelines
    }

    private readonly struct RegistrationCall(
        MixedRegistrationApiAnalyzer.RegistrationCallKind kind,
        Location? location) : System.IEquatable<RegistrationCall>
    {
        public RegistrationCallKind Kind { get; } = kind;
        public Location? Location { get; } = location;

        // Equality by kind + source location to support Distinct() in the pipeline.
        public bool Equals(RegistrationCall other) =>
            Kind == other.Kind && Equals(Location, other.Location);

        public override bool Equals(object obj) =>
            obj is RegistrationCall other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ (Location?.GetHashCode() ?? 0);
            }
        }
    }
}
