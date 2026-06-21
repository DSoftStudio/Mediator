// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Incremental generator that emits <c>DSOFT006</c> (Info) when a type implements
/// <c>IRequest&lt;T&gt;</c> directly instead of the CQRS marker interfaces
/// <c>ICommand&lt;T&gt;</c> or <c>IQuery&lt;T&gt;</c>.
/// <para>
/// <c>ICommand&lt;T&gt;</c> and <c>IQuery&lt;T&gt;</c> extend <c>IRequest&lt;T&gt;</c>
/// with zero runtime overhead, giving pipeline behaviors a convenient runtime
/// type check (<c>request is ICommand</c> / <c>request is IQuery</c>) and making
/// intent explicit at the type level.
/// </para>
/// </summary>
[Generator]
public sealed class CqrsSemanticAnalyzer : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null }
                    || node is RecordDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) =>
                {
                    var typeDecl = (TypeDeclarationSyntax)ctx.Node;

                    if (ctx.SemanticModel.GetDeclaredSymbol(typeDecl, ct)
                        is not INamedTypeSymbol symbol)
                        return default;

                    if (symbol.IsAbstract)
                        return default;

                    if (HandlerDiscovery.IsFileLocal(typeDecl))
                        return default;

                    bool implementsRequest = false;
                    string? responseType = null;
                    bool hasCqrsMarker = false;

                    foreach (var iface in symbol.AllInterfaces)
                    {
                        ct.ThrowIfCancellationRequested();

                        var ns = iface.ContainingNamespace?.ToDisplayString() ?? "";
                        if (ns != "DSoftStudio.Mediator.Abstractions")
                            continue;

                        var meta = iface.MetadataName;

                        if (meta == "IRequest`1")
                        {
                            implementsRequest = true;
                            responseType = iface.TypeArguments[0]
                                .ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                        }
                        else if (meta is "ICommand" or "IQuery" or "ICommand`1" or "IQuery`1")
                        {
                            hasCqrsMarker = true;
                        }
                    }

                    if (!implementsRequest || hasCqrsMarker)
                        return default;

                    // Span the whole type header — identifier through base list —
                    // so the IDE offers the ConvertToCqrs fix when hovering the
                    // offending `IRequest<T>` base type too, not just the type
                    // name. DSOFT006 is Info severity: VS renders suggestion dots
                    // only at the span start, so the wider span adds lightbulb
                    // reach without squiggle noise. Mirrors the Enterprise
                    // CqrsSemanticAnalyzerEnterprise (DiagnosticLocations.TypeHeader).
                    var headerSpan = typeDecl.BaseList is { } baseList
                        ? TextSpan.FromBounds(typeDecl.Identifier.SpanStart, baseList.Span.End)
                        : typeDecl.Identifier.Span;

                    return new CqrsCandidate(
                        symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        responseType ?? "TResponse",
                        typeDecl.SyntaxTree.FilePath,
                        headerSpan);
                })
            .Where(static c => c.FilePath is not null);

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            // Reconstruct a minimal location from the stored file path + span.
            var location = Location.Create(candidate.FilePath!, candidate.Span,
                new LinePositionSpan(LinePosition.Zero, LinePosition.Zero));

            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.PreferCqrsInterface,
                location,
                candidate.TypeName,
                candidate.ResponseType));
        });
    }

    /// <summary>
    /// Cached data for a type that implements <c>IRequest&lt;T&gt;</c> without CQRS markers.
    /// Uses value types only for correct incremental-generation caching.
    /// </summary>
    internal readonly struct CqrsCandidate : System.IEquatable<CqrsCandidate>
    {
        public readonly string? TypeName;
        public readonly string? ResponseType;
        public readonly string? FilePath;
        public readonly TextSpan Span;

        public CqrsCandidate(string typeName, string responseType, string filePath, TextSpan span)
        {
            TypeName = typeName;
            ResponseType = responseType;
            FilePath = filePath;
            Span = span;
        }

        public bool Equals(CqrsCandidate other)
            => TypeName == other.TypeName
            && ResponseType == other.ResponseType
            && FilePath == other.FilePath
            && Span.Equals(other.Span);

        public override bool Equals(object obj)
            => obj is CqrsCandidate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (TypeName?.GetHashCode() ?? 0);
                hash = hash * 31 + (ResponseType?.GetHashCode() ?? 0);
                hash = hash * 31 + (FilePath?.GetHashCode() ?? 0);
                hash = hash * 31 + Span.GetHashCode();
                return hash;
            }
        }
    }
}
