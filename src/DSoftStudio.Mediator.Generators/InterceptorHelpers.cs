// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis;

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
}
