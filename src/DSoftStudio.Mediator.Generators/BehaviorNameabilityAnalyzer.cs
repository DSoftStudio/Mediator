// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Emits <c>DSOFT011</c> when a pipeline behavior cannot be named from generated code, so its
/// open-generic registration is left for the container to close at runtime.
/// <para>
/// The generated registry normally REPLACES an open-generic behavior registration with literal
/// closed ones, which is what keeps the pipeline AOT-safe and what lets a specialized chain be
/// emitted. It can only write <c>typeof(TheBehavior&lt;Request, Response&gt;)</c> for a type it can
/// spell from a different file in the same assembly. For a <c>file</c> type, a private or protected
/// nested one, or one nested inside a generic, it emits nothing at all and the open descriptor
/// survives.
/// </para>
/// <para>
/// Measured, on two projects identical but for one modifier on the behavior: as <c>file</c>, nothing
/// is emitted and the native binary dies on the first request with a value-type response —
/// <c>Unable to create a generic service ... because 'System.Int32' is a ValueType</c>. As
/// <c>internal</c>, the closed descriptors and chain links appear and the same binary runs. The
/// build reported zero warnings either way, and so did the AOT publish: ILC never sees a problem
/// because the reflection is the container's, not the application's.
/// </para>
/// <para>
/// <b>Why this reports <c>file</c> types when <see cref="HandlerAccessibilityAnalyzer"/> does not.</b>
/// That rule reasons that a <c>file</c> type is deliberately invisible and the author chose it, so
/// saying so would be noise. It holds for a handler, which is simply not registered. It does not
/// hold here: a <c>file</c> behavior keeps working, and what it silently costs is AOT compatibility
/// and the specialized chain — neither of which anyone chooses by writing <c>file</c>.
/// </para>
/// </summary>
[Generator]
public sealed class BehaviorNameabilityAnalyzer : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) =>
                {
                    var typeDecl = (TypeDeclarationSyntax)ctx.Node;

                    if (ctx.SemanticModel.GetDeclaredSymbol(typeDecl, ct)
                        is not INamedTypeSymbol symbol)
                        return default;

                    // Only OPEN generic behaviors are affected: a closed one is registered by naming
                    // the closed type, which the container never has to construct.
                    if (symbol.IsAbstract || !symbol.IsGenericType || symbol.TypeParameters.Length != 2)
                        return default;

                    var kind = BehaviorKindOf(symbol, ct);
                    if (kind is null)
                        return default;

                    // allowInternal: the generated registry lives in this same assembly, so internal
                    // is nameable here. This is the very check discovery uses, asked the same way, so
                    // the rule cannot drift from the behaviour it describes.
                    if (ReferencedAssemblyScanner.IsNameableBehaviorType(symbol, allowInternal: true))
                        return default;

                    // The LINE span as well as the text span. Location.Create shows the caller the
                    // line span, so passing LinePositionSpan.Zero — as the sibling rule does — puts
                    // every one of these at (1,1) and the warning cannot be navigated to.
                    var identifier = typeDecl.Identifier;
                    return new UnnameableBehavior(
                        symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        kind,
                        typeDecl.SyntaxTree.FilePath,
                        identifier.Span,
                        identifier.GetLocation().GetLineSpan().Span);
                })
            .Where(static c => c.FilePath is not null);

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var location = Location.Create(candidate.FilePath!, candidate.Span, candidate.LineSpan);

            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BehaviorNotNameable,
                location,
                candidate.TypeName,
                candidate.BehaviorKind));
        });
    }

    /// <summary>
    /// The pipeline abstraction this type implements, or <see langword="null"/> when it implements
    /// none — in which case how it is named is nobody's business.
    /// </summary>
    private static string? BehaviorKindOf(INamedTypeSymbol symbol, System.Threading.CancellationToken ct)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            ct.ThrowIfCancellationRequested();

            if (iface.ContainingNamespace?.ToDisplayString() != "DSoftStudio.Mediator.Abstractions")
                continue;

            switch (iface.MetadataName)
            {
                case "IPipelineBehavior`2": return "pipeline behavior";
                case "IStreamPipelineBehavior`2": return "stream pipeline behavior";
                case "IRequestPostProcessor`2": return "post-processor";
                case "IRequestExceptionHandler`2": return "exception handler";
            }
        }

        return null;
    }

    /// <summary>
    /// Cached data for a behavior the generator cannot name. Value types only, for correct
    /// incremental caching.
    /// </summary>
    internal readonly struct UnnameableBehavior : System.IEquatable<UnnameableBehavior>
    {
        public readonly string? TypeName;
        public readonly string? BehaviorKind;
        public readonly string? FilePath;
        public readonly TextSpan Span;
        public readonly LinePositionSpan LineSpan;

        public UnnameableBehavior(
            string typeName, string behaviorKind, string filePath, TextSpan span, LinePositionSpan lineSpan)
        {
            TypeName = typeName;
            BehaviorKind = behaviorKind;
            FilePath = filePath;
            Span = span;
            LineSpan = lineSpan;
        }

        public bool Equals(UnnameableBehavior other) =>
            TypeName == other.TypeName
            && BehaviorKind == other.BehaviorKind
            && FilePath == other.FilePath
            && Span.Equals(other.Span)
            && LineSpan.Equals(other.LineSpan);

        public override bool Equals(object obj) => obj is UnnameableBehavior other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TypeName?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (BehaviorKind?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (FilePath?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ Span.GetHashCode();
                hash = (hash * 397) ^ LineSpan.GetHashCode();
                return hash;
            }
        }
    }
}
