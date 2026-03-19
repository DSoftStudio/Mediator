// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Shared helpers for interceptor generators.
/// Pure static methods — no allocations, no state.
/// </summary>
internal static class InterceptorHelpers
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="containingType"/> is or implements
    /// the interface identified by <paramref name="interfaceMetadataName"/>.
    /// </summary>
    public static bool ImplementsInterface(
        INamedTypeSymbol containingType,
        Compilation compilation,
        string interfaceMetadataName)
    {
        var target = compilation.GetTypeByMetadataName(interfaceMetadataName);
        if (target is null)
            return false;

        if (SymbolEqualityComparer.Default.Equals(containingType, target))
            return true;

        return containingType.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, target));
    }

    /// <summary>
    /// Resolves the first meaningful parameter from a method call that may be either
    /// an explicit generic call or a type-inferred extension method call.
    /// Returns <see langword="null"/> when the parameter cannot be determined.
    /// </summary>
    public static IParameterSymbol? ResolveRequestParameter(IMethodSymbol method)
    {
        if (method.IsExtensionMethod && method.ReducedFrom is not null)
            return method.Parameters[0];

        return method.Parameters.Length >= 2 ? method.Parameters[1] : null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="node"/> is located inside a lambda
    /// expression whose converted type is <see cref="System.Linq.Expressions.Expression{TDelegate}"/>.
    /// <para>
    /// Interceptors must NOT rewrite call sites inside expression trees because the
    /// rewritten static extension method invocation is incompatible with expression
    /// tree compilation. This also prevents breaking mocking frameworks (Moq, NSubstitute,
    /// FakeItEasy) that inspect the expression tree passed to Setup/Verify/Received.
    /// </para>
    /// </summary>
    public static bool IsInsideExpressionTreeLambda(
        SemanticModel semanticModel,
        SyntaxNode node,
        CancellationToken ct)
    {
        var expressionOfT = semanticModel.Compilation
            .GetTypeByMetadataName("System.Linq.Expressions.Expression`1");

        if (expressionOfT is null)
            return false;

        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (current is LambdaExpressionSyntax lambda)
            {
                var typeInfo = semanticModel.GetTypeInfo(lambda, ct);
                if (typeInfo.ConvertedType is INamedTypeSymbol convertedType
                    && SymbolEqualityComparer.Default.Equals(
                        convertedType.OriginalDefinition, expressionOfT))
                {
                    return true;
                }
            }

            current = current.Parent;
        }

        return false;
    }
}
