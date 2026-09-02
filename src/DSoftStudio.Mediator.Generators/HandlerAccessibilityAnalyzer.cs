// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Emits <c>DSOFT009</c> when a handler the generator would otherwise register cannot be named from
/// generated code, so discovery skipped it.
/// <para>
/// Generated registration lives in its own file in the same assembly, which can reach a
/// <c>public</c> or <c>internal</c> type but not a <c>private</c>/<c>protected</c> nested one, and
/// cannot spell a type nested inside a generic one at all. Before
/// <see cref="HandlerDiscovery.IsReferenceableFromGeneratedCode(TypeDeclarationSyntax, INamedTypeSymbol)"/>
/// existed, discovery only rejected <c>file</c> types and emitted references to the rest anyway —
/// a private nested handler inside a test fixture produced hundreds of CS0122 errors in files the
/// user cannot edit. Discovery now skips them, and this rule says so at the declaration instead of
/// letting the handler go missing silently until a request fails to dispatch at runtime.
/// </para>
/// <para>
/// It mirrors <c>DSOFT005</c>, which reports the same thing for an <c>internal</c> handler in a
/// referenced assembly: skipped, with the accessibility change that would include it.
/// </para>
/// </summary>
[Generator]
public sealed class HandlerAccessibilityAnalyzer : IIncrementalGenerator
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

                    if (symbol.IsAbstract || symbol.TypeKind != TypeKind.Class)
                        return default;

                    // `file` types are deliberately invisible and the user chose that; saying so on
                    // every one of them would be noise, not news.
                    if (HandlerDiscovery.IsFileLocal(typeDecl))
                        return default;

                    if (HandlerDiscovery.IsReferenceableFromGeneratedCode(symbol))
                        return default;

                    var kind = HandlerKindOf(symbol, ct);
                    if (kind is null)
                        return default;

                    return new InaccessibleHandler(
                        symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        kind,
                        typeDecl.SyntaxTree.FilePath,
                        typeDecl.Identifier.Span);
                })
            .Where(static c => c.FilePath is not null);

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var location = Location.Create(candidate.FilePath!, candidate.Span,
                new LinePositionSpan(LinePosition.Zero, LinePosition.Zero));

            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InaccessibleHandlerSkipped,
                location,
                candidate.TypeName,
                candidate.HandlerKind));
        });
    }

    /// <summary>
    /// The mediator abstraction this type implements ("request handler", "stream handler",
    /// "notification handler"), or <see langword="null"/> when it implements none — in which case
    /// its accessibility is nobody's business.
    /// </summary>
    private static string? HandlerKindOf(INamedTypeSymbol symbol, System.Threading.CancellationToken ct)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            ct.ThrowIfCancellationRequested();

            if (iface.ContainingNamespace?.ToDisplayString() != "DSoftStudio.Mediator.Abstractions")
                continue;

            switch (iface.MetadataName)
            {
                case "IRequestHandler`2": return "request handler";
                case "IStreamRequestHandler`2": return "stream handler";
                case "INotificationHandler`1": return "notification handler";
            }
        }

        return null;
    }

    /// <summary>
    /// Cached data for a skipped handler. Value types only, for correct incremental caching.
    /// </summary>
    internal readonly struct InaccessibleHandler : System.IEquatable<InaccessibleHandler>
    {
        public readonly string? TypeName;
        public readonly string? HandlerKind;
        public readonly string? FilePath;
        public readonly TextSpan Span;

        public InaccessibleHandler(string typeName, string handlerKind, string filePath, TextSpan span)
        {
            TypeName = typeName;
            HandlerKind = handlerKind;
            FilePath = filePath;
            Span = span;
        }

        public bool Equals(InaccessibleHandler other)
            => TypeName == other.TypeName
            && HandlerKind == other.HandlerKind
            && FilePath == other.FilePath
            && Span == other.Span;

        public override bool Equals(object? obj) => obj is InaccessibleHandler other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TypeName?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (HandlerKind?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (FilePath?.GetHashCode() ?? 0);
                return (hash * 397) ^ Span.GetHashCode();
            }
        }
    }
}
