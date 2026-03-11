// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Generates typed extension methods on <c>ISender</c> / <c>IMediator</c>
/// so the user can write:
/// <code>
///   await mediator.Send(new Ping());              // inferred → Send&lt;Ping, int&gt;
///   await foreach (var x in mediator.CreateStream(new PingStream()))  // inferred
/// </code>
/// Zero overhead: the extension methods are thin wrappers that call the
/// strongly-typed overload directly.
/// </summary>
[Generator]
public sealed class MediatorExtensionsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ── Request handlers (Send) ──────────────────────────────
        var requestHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => GetRequestInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var localRequests = requestHandlers.Collect();

        var externalRequests = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var external = ReferencedAssemblyScanner.GetExternalPipelineHandlers(compilation);
                var array = external
                    .Select(e => new RequestResponsePair(e.RequestType, e.ResponseType))
                    .ToArray();
                return new EquatableArray<RequestResponsePair>(array);
            });

        // ── Stream handlers (CreateStream) ───────────────────────
        var streamHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => GetStreamInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var localStreams = streamHandlers.Collect();

        var externalStreams = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var external = ReferencedAssemblyScanner.GetExternalStreamHandlers(compilation);
                var array = external
                    .Select(e => new RequestResponsePair(e.RequestType, e.ResponseType))
                    .ToArray();
                return new EquatableArray<RequestResponsePair>(array);
            });

        // ── Combine and emit ─────────────────────────────────────
        var combined = localRequests
            .Combine(externalRequests)
            .Combine(localStreams)
            .Combine(externalStreams);

        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            var (((localReqs, extReqs), localStrs), extStrs) = data;

            var localReqList = localReqs.IsDefaultOrEmpty
                ? Enumerable.Empty<RequestResponsePair>()
                : localReqs.Distinct();

            var requests = localReqList
                .Concat(extReqs)
                .Distinct()
                .OrderBy(static p => p.RequestType)
                .ToList();

            var localStrList = localStrs.IsDefaultOrEmpty
                ? Enumerable.Empty<RequestResponsePair>()
                : localStrs.Distinct();

            var streams = localStrList
                .Concat(extStrs)
                .Distinct()
                .OrderBy(static p => p.RequestType)
                .ToList();

            var code = GenerateCode(requests, streams);

            spc.AddSource(
                "MediatorExtensions.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
    }

    // ── Discovery ────────────────────────────────────────────────

    private static RequestResponsePair? GetRequestInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract || symbol.TypeKind != TypeKind.Class)
            return null;

        if (HandlerDiscovery.IsFileLocal(classDecl))
            return null;

        if (!HandlerDiscovery.TryGetRequestHandler(
                symbol, ct, out var requestType, out var responseType))
            return null;

        return new RequestResponsePair(requestType, responseType);
    }

    private static RequestResponsePair? GetStreamInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract || symbol.TypeKind != TypeKind.Class)
            return null;

        if (HandlerDiscovery.IsFileLocal(classDecl))
            return null;

        if (!HandlerDiscovery.TryGetStreamHandler(
                symbol, ct,
                out var requestType, out var responseType, out _))
            return null;

        return new RequestResponsePair(requestType, responseType);
    }

    // ── Code generation ──────────────────────────────────────────

    private static string GenerateCode(
        List<RequestResponsePair> requests,
        List<RequestResponsePair> streams)
    {
        var sb = new StringBuilder(2048);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace DSoftStudio.Mediator");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Auto-generated typed extension methods for <see cref=\"global::DSoftStudio.Mediator.Abstractions.ISender\"/>");
        sb.AppendLine("    /// and <see cref=\"global::DSoftStudio.Mediator.Abstractions.IMediator\"/>.");
        sb.AppendLine("    /// Enables <c>mediator.Send(new Ping())</c> with full type inference — zero overhead.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static class MediatorTypedExtensions");
        sb.AppendLine("    {");

        // ── Send extensions ──────────────────────────────────────
        foreach (var pair in requests)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Sends a <see cref=\"{EscapeXml(pair.RequestType)}\"/> through the pipeline. Type-inferred shorthand.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.Append("        public static global::System.Threading.Tasks.ValueTask<");
            sb.Append(pair.ResponseType);
            sb.Append("> Send(this global::DSoftStudio.Mediator.Abstractions.ISender sender, ");
            sb.Append(pair.RequestType);
            sb.AppendLine(" request, global::System.Threading.CancellationToken cancellationToken = default)");
            sb.Append("            => sender.Send<");
            sb.Append(pair.RequestType);
            sb.Append(", ");
            sb.Append(pair.ResponseType);
            sb.AppendLine(">(request, cancellationToken);");
            sb.AppendLine();
        }

        // ── CreateStream extensions ──────────────────────────────
        foreach (var pair in streams)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Creates an async stream from a <see cref=\"{EscapeXml(pair.RequestType)}\"/>. Type-inferred shorthand.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.Append("        public static global::System.Collections.Generic.IAsyncEnumerable<");
            sb.Append(pair.ResponseType);
            sb.Append("> CreateStream(this global::DSoftStudio.Mediator.Abstractions.IMediator mediator, ");
            sb.Append(pair.RequestType);
            sb.AppendLine(" request, global::System.Threading.CancellationToken cancellationToken = default)");
            sb.Append("            => mediator.CreateStream<");
            sb.Append(pair.RequestType);
            sb.Append(", ");
            sb.Append(pair.ResponseType);
            sb.AppendLine(">(request, cancellationToken);");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeXml(string input)
        => input.Replace("<", "{").Replace(">", "}");

    // ── Data model ───────────────────────────────────────────────

    internal readonly struct RequestResponsePair : System.IEquatable<RequestResponsePair>
    {
        public string RequestType { get; }
        public string ResponseType { get; }

        public RequestResponsePair(string requestType, string responseType)
        {
            RequestType = requestType;
            ResponseType = responseType;
        }

        public bool Equals(RequestResponsePair other) =>
            RequestType == other.RequestType &&
            ResponseType == other.ResponseType;

        public override bool Equals(object obj) =>
            obj is RequestResponsePair other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (RequestType.GetHashCode() * 397) ^ ResponseType.GetHashCode();
            }
        }
    }
}
