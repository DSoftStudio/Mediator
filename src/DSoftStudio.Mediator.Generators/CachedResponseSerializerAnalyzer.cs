// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Emits <c>DSOFT012</c> when a request is cached, the build publishes without a JIT, and the
/// response type has no serializer this compilation can see.
/// <para>
/// <c>HybridCache</c> serializes every payload it caches, and <c>AddHybridCache</c> pre-registers a
/// serializer for exactly two types — <c>string</c> and <c>byte[]</c>. Everything else reaches the
/// reflection-based <c>System.Text.Json</c> fallback, which Native AOT and trimming disable.
/// Measured on a native binary: the build succeeds, the publish succeeds, the application starts,
/// and the first dispatch of a cacheable request throws from inside Microsoft's serializer, naming
/// neither the request nor the remedy.
/// </para>
/// <para>
/// The rule reports at the REQUEST, because that is the thing the author can act on, and it names
/// the response type because that is what needs registering.
/// </para>
/// <para>
/// <b>Coupling.</b> <c>ICachedRequest</c> lives in the caching companion, not here. The rule looks
/// it up by metadata name and does nothing at all when the lookup fails, so a compilation that does
/// not use the companion never pays for this and the core generator gains no reference to it.
/// </para>
/// </summary>
[Generator]
public sealed class CachedResponseSerializerAnalyzer : IIncrementalGenerator
{
    private const string CachedRequestFullName = "DSoftStudio.Mediator.HybridCache.ICachedRequest";
    private const string SerializerMetadataName = "IHybridCacheSerializer`1";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Only a build that publishes without a JIT can hit this. Anything else would be pure noise:
        // the reflective fallback works, and the developer has nothing to do.
        var publishesWithoutJit = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            IsTrue(provider, "build_property.PublishAot")
            || IsTrue(provider, "build_property.PublishTrimmed"));

        var cachedRequests = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null }
                    || node is RecordDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => CachedRequestOf(ctx, ct))
            .Where(static c => c.RequestName is not null)
            .Collect();

        // Serializer registrations, read from call sites. Incomplete by nature — a registration in
        // another assembly or behind a helper cannot be seen — which is why finding one SILENCES the
        // rule but failing to find one is the only thing that lets it speak.
        var registeredTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax,
                transform: static (ctx, ct) => SerializerRegistrationOf(ctx, ct))
            .Where(static r => r is not null)
            .Select(static (r, _) => r!)
            .Collect();

        var combined = cachedRequests.Combine(registeredTypes).Combine(publishesWithoutJit);

        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            var ((requests, registrations), withoutJit) = data;

            if (!withoutJit || requests.IsDefaultOrEmpty)
                return;

            // A blanket registration keyed on the OPEN generic covers every type at once, which is
            // the shape the documentation recommends, so it silences the rule wholesale.
            if (registrations.Contains(string.Empty))
                return;

            var covered = new HashSet<string>(registrations.Where(static r => r.Length > 0), System.StringComparer.Ordinal);

            foreach (var request in requests)
            {
                if (covered.Contains(request.ResponseName!))
                    continue;

                var location = Location.Create(
                    request.FilePath!, request.Span, request.LineSpan);

                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.CachedResponseNeedsSerializer,
                    location,
                    request.RequestName,
                    request.ResponseName));
            }
        });
    }

    private static bool IsTrue(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider provider, string key)
        => provider.GlobalOptions.TryGetValue(key, out var value)
           && string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A request that implements <c>ICachedRequest</c>, paired with the response it returns — or a
    /// default when the type is not one, is not cached, or returns something HybridCache already has
    /// a serializer for.
    /// </summary>
    private static CachedRequest CachedRequestOf(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var typeDecl = (TypeDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(typeDecl, ct) is not INamedTypeSymbol symbol)
            return default;

        var cachedRequest = ctx.SemanticModel.Compilation.GetTypeByMetadataName(CachedRequestFullName);
        if (cachedRequest is null)
            return default;

        if (!symbol.AllInterfaces.Contains(cachedRequest, SymbolEqualityComparer.Default))
            return default;

        ITypeSymbol? response = null;
        foreach (var iface in symbol.AllInterfaces)
        {
            ct.ThrowIfCancellationRequested();

            if (iface.OriginalDefinition.ContainingNamespace?.ToDisplayString()
                    != "DSoftStudio.Mediator.Abstractions"
                || iface.OriginalDefinition.MetadataName != "IRequest`1")
            {
                continue;
            }

            response = iface.TypeArguments[0];
            break;
        }

        if (response is null)
            return default;

        // The two HybridCache ships a serializer for. Reporting these would be a false alarm.
        if (response.SpecialType == SpecialType.System_String
            || response is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
        {
            return default;
        }

        var identifier = typeDecl.Identifier;
        return new CachedRequest(
            symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            response.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            typeDecl.SyntaxTree.FilePath,
            identifier.Span,
            identifier.GetLocation().GetLineSpan().Span);
    }

    /// <summary>
    /// The response type a call site registers a serializer for: its display name, or the empty
    /// string for a blanket registration keyed on the open generic, or <see langword="null"/> when
    /// the call registers no serializer at all.
    /// </summary>
    private static string? SerializerRegistrationOf(
        GeneratorSyntaxContext ctx,
        System.Threading.CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            ct.ThrowIfCancellationRequested();

            if (argument.Expression is not TypeOfExpressionSyntax typeOf)
                continue;

            if (ctx.SemanticModel.GetTypeInfo(typeOf.Type, ct).Type is not INamedTypeSymbol named)
                continue;

            if (named.OriginalDefinition.MetadataName != SerializerMetadataName)
                continue;

            // typeof(IHybridCacheSerializer<>) — the blanket key the recommended wiring uses.
            if (named.IsUnboundGenericType || named.TypeArguments.Length == 0)
                return string.Empty;

            return named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        // AddSingleton<IHybridCacheSerializer<ProductDto>, ProductDtoSerializer>()
        if (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax generic })
        {
            foreach (var argument in generic.TypeArgumentList.Arguments)
            {
                if (ctx.SemanticModel.GetTypeInfo(argument, ct).Type is not INamedTypeSymbol named)
                    continue;

                if (named.OriginalDefinition.MetadataName != SerializerMetadataName
                    || named.TypeArguments.Length == 0)
                {
                    continue;
                }

                return named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }
        }

        return null;
    }

    /// <summary>
    /// Cached data for a cacheable request. Value types only, for correct incremental caching.
    /// </summary>
    internal readonly struct CachedRequest : System.IEquatable<CachedRequest>
    {
        public readonly string? RequestName;
        public readonly string? ResponseName;
        public readonly string? FilePath;
        public readonly TextSpan Span;
        public readonly LinePositionSpan LineSpan;

        public CachedRequest(
            string requestName, string responseName, string filePath, TextSpan span, LinePositionSpan lineSpan)
        {
            RequestName = requestName;
            ResponseName = responseName;
            FilePath = filePath;
            Span = span;
            LineSpan = lineSpan;
        }

        public bool Equals(CachedRequest other) =>
            RequestName == other.RequestName
            && ResponseName == other.ResponseName
            && FilePath == other.FilePath
            && Span.Equals(other.Span)
            && LineSpan.Equals(other.LineSpan);

        public override bool Equals(object obj) => obj is CachedRequest other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RequestName?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (ResponseName?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (FilePath?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ Span.GetHashCode();
                hash = (hash * 397) ^ LineSpan.GetHashCode();
                return hash;
            }
        }
    }
}
