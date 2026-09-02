// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Shared discovery for the ADR-0065 SAFE fast path: maps a closed
/// (requestType, responseType) pair to its concrete <c>IRequestHandler&lt;,&gt;</c>
/// implementation so the dispatch tail can use a concrete-typed cache
/// (devirtualized <c>Handle</c>) instead of the interface-typed <c>HandlerCache</c>.
/// Used by <see cref="SendInterceptorGenerator"/> and <see cref="MediatorExtensionsGenerator"/>.
/// </summary>
internal static class SendFastPath
{
    /// <summary>
    /// One (request, response) → concrete handler mapping.
    /// Value-equatable strings keep the incremental pipeline cacheable.
    /// </summary>
    internal readonly struct HandlerMapEntry(
        string requestType,
        string responseType,
        string handlerType) : System.IEquatable<HandlerMapEntry>
    {
        public string RequestType { get; } = requestType;
        public string ResponseType { get; } = responseType;
        public string HandlerType { get; } = handlerType;

        public bool Equals(HandlerMapEntry other) =>
            RequestType == other.RequestType &&
            ResponseType == other.ResponseType &&
            HandlerType == other.HandlerType;

        public override bool Equals(object obj) =>
            obj is HandlerMapEntry other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RequestType.GetHashCode() * 397;
                hash = (hash ^ ResponseType.GetHashCode()) * 397;
                return hash ^ HandlerType.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Local <c>IRequestHandler&lt;,&gt;</c> implementations in this compilation. Same-compilation
    /// types are always nameable from generated code (internal is fine; file-local and
    /// open-generic implementations are excluded — mirroring <see cref="DependencyInjectionGenerator"/>).
    /// </summary>
    public static HandlerMapEntry? GetLocalHandlerMapEntry(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract || symbol.TypeKind != TypeKind.Class)
            return null;

        // A generic handler implementation has no single closed concrete type to cache.
        if (symbol.IsGenericType)
            return null;

        if (!HandlerDiscovery.IsReferenceableFromGeneratedCode(classDecl, symbol))
            return null;

        if (!HandlerDiscovery.TryGetRequestHandler(symbol, ct, out var requestType, out var responseType))
            return null;

        // Explicit interface implementations expose no public Handle on the concrete type —
        // the emitted `concrete.Handle(...)` would not compile (CS1061). Keep the interface tail.
        if (!HasPublicImplicitHandle(symbol))
            return null;

        return new HandlerMapEntry(
            requestType,
            responseType,
            symbol.ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat));
    }

    /// <summary>
    /// True when the type's implementation of <c>IRequestHandler&lt;,&gt;.Handle</c> is a public
    /// implicit member callable on the CONCRETE type (possibly inherited). Explicit interface
    /// implementations return false — the concrete-typed fast path cannot call them.
    /// When <paramref name="specificInterface"/> is null, the FIRST implemented
    /// <c>IRequestHandler&lt;,&gt;</c> is checked (matching <see cref="HandlerDiscovery.TryGetRequestHandler"/>,
    /// which produced the map entry).
    /// </summary>
    public static bool HasPublicImplicitHandle(
        INamedTypeSymbol symbol,
        INamedTypeSymbol? specificInterface = null)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            var original = iface.OriginalDefinition;
            if (original.ContainingNamespace?.ToDisplayString() != "DSoftStudio.Mediator.Abstractions"
                || original.MetadataName != "IRequestHandler`2")
            {
                continue;
            }

            if (specificInterface is not null
                && !SymbolEqualityComparer.Default.Equals(iface, specificInterface))
            {
                continue;
            }

            var handleMember = iface.GetMembers("Handle").OfType<IMethodSymbol>().FirstOrDefault();
            if (handleMember is null)
                return false;

            // MethodKind.Ordinary rejects explicit interface implementations; the ContainingType
            // TypeKind check rejects C# 8 default interface methods (the implementation lives on
            // the INTERFACE — `concrete.Handle(...)` would still be CS1061 on the class).
            return symbol.FindImplementationForInterfaceMember(handleMember) is IMethodSymbol impl
                && impl.MethodKind == MethodKind.Ordinary
                && impl.DeclaredAccessibility == Accessibility.Public
                && impl.ContainingType.TypeKind == TypeKind.Class;
        }

        return false;
    }

    /// <summary>
    /// Merges local + external handler entries into a (request, response) → concrete handler map,
    /// dropping any pair with MORE than one distinct implementation: with multiple registrations
    /// MSDI's last-wins winner is a registration-order question the generator cannot answer, so
    /// ambiguous pairs keep the interface-typed <c>HandlerCache</c> tail (fail open). The emitted
    /// cache's miss path makes even a wrong compile-time guess safe at runtime — this filter just
    /// avoids emitting caches that would never hit.
    /// <para>
    /// That miss path is an EXACT type test — <c>svc.GetType() == typeof(THandler)</c>
    /// (<c>InterceptorHelpers.cs:218</c>) — and it has to stay exact. An <c>is</c> test would also
    /// match a subclass, and a subclass that hides <c>Handle</c> with <c>new</c> would then be
    /// called through the BASE type's statically-bound implementation: a silent misdispatch.
    /// Decorators registered at runtime rely on the same exactness to degrade to interface
    /// dispatch instead of being cached as the concrete handler.
    /// </para>
    /// </summary>
    public static Dictionary<(string Request, string Response), string> BuildUniqueHandlerMap(
        System.Collections.Immutable.ImmutableArray<HandlerMapEntry> localEntries,
        EquatableArray<HandlerMapEntry> externalEntries)
    {
        var map = new Dictionary<(string, string), string?>();

        if (!localEntries.IsDefaultOrEmpty)
        {
            foreach (var entry in localEntries)
                Add(map, entry);
        }

        foreach (var entry in externalEntries)
            Add(map, entry);

        var result = new Dictionary<(string, string), string>();
        foreach (var kvp in map)
        {
            if (kvp.Value is not null)
                result[kvp.Key] = kvp.Value;
        }

        return result;

        static void Add(Dictionary<(string, string), string?> map, in HandlerMapEntry entry)
        {
            var key = (entry.RequestType, entry.ResponseType);
            if (map.TryGetValue(key, out var existing))
            {
                if (existing != entry.HandlerType)
                    map[key] = null; // ambiguous — disable the concrete fast path for this pair
            }
            else
            {
                map[key] = entry.HandlerType;
            }
        }
    }
}
