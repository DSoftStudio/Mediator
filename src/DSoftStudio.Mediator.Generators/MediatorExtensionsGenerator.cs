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

        // ── Self-handling request classes (IRequest<T> + static Execute) ──
        var selfHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null }
                    || node is RecordDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => GetSelfHandlerRequestInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var selfCollected = selfHandlers.Collect();

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
        var assemblyName = context.CompilationProvider
            .Select(static (c, _) => c.AssemblyName ?? "Assembly");

        var combined = localRequests
            .Combine(externalRequests)
            .Combine(selfCollected)
            .Combine(localStreams)
            .Combine(externalStreams)
            .Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            var (((((localReqs, extReqs), selfReqs), localStrs), extStrs), asmName) = data;

            var localReqList = localReqs.IsDefaultOrEmpty
                ? []
                : localReqs.Distinct();

            IEnumerable<RequestResponsePair> selfReqPairs = selfReqs.IsDefaultOrEmpty
                ? []
                : selfReqs.Select(static s => new RequestResponsePair(s.RequestType, s.ResponseType));

            var requests = localReqList
                .Concat(extReqs)
                .Concat(selfReqPairs)
                .Distinct()
                .OrderBy(static p => p.RequestType)
                .ToList();

            var localStrList = localStrs.IsDefaultOrEmpty
                ? []
                : localStrs.Distinct();

            var streams = localStrList
                .Concat(extStrs)
                .Distinct()
                .OrderBy(static p => p.RequestType)
                .ToList();

            var code = GenerateCode(requests, streams, asmName);

            spc.AddSource(
                "MediatorExtensions.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
    }

    /// <summary>
    /// Extracts (requestType, responseType) from self-handling request classes
    /// for typed extension method generation.
    /// </summary>
    private static SelfHandlerDetail? GetSelfHandlerRequestInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var typeDecl = (TypeDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(typeDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract || symbol.TypeKind != TypeKind.Class)
            return null;

        if (HandlerDiscovery.IsFileLocal(typeDecl))
            return null;

        if (!HandlerDiscovery.TryGetSelfHandlingRequest(symbol, ct, out var detail))
            return null;

        return detail;
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
        List<RequestResponsePair> streams,
        string assemblyName)
    {
        var sanitizedAsm = HandlerDiscovery.SanitizeIdentifier(assemblyName);
        var sb = new StringBuilder(2048);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"global using DSoftStudio.Mediator.Generated.{sanitizedAsm};");
        sb.AppendLine();
        sb.AppendLine($"namespace DSoftStudio.Mediator.Generated.{sanitizedAsm}");
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

        // ── Send(object) runtime dispatch ────────────────────────
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Sends a request whose compile-time type is unknown (runtime dispatch).");
        sb.AppendLine("        /// The object must be a type discovered at compile time by the source generator.");
        sb.AppendLine("        /// <para>");
        sb.AppendLine("        /// Uses the compile-time generated dispatch table — no reflection, AOT-safe.");
        sb.AppendLine("        /// </para>");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <returns>The handler response boxed as <see cref=\"object\"/>.</returns>");
        sb.AppendLine("        public static global::System.Threading.Tasks.ValueTask<object?> Send(");
        sb.AppendLine("            this global::DSoftStudio.Mediator.Abstractions.ISender sender,");
        sb.AppendLine("            object request,");
        sb.AppendLine("            global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (sender == null) throw new global::System.ArgumentNullException(nameof(sender));");
        sb.AppendLine("            if (request == null) throw new global::System.ArgumentNullException(nameof(request));");
        sb.AppendLine("            // Mock-safe guard: when ISender is a test double (Moq, NSubstitute, etc.)");
        sb.AppendLine("            // it won't implement IServiceProviderAccessor — throw a clear error.");
        sb.AppendLine("            if (sender is not global::DSoftStudio.Mediator.IServiceProviderAccessor __accessor)");
        sb.AppendLine("                throw new global::System.InvalidOperationException(");
        sb.AppendLine("                    \"Runtime object dispatch (Send(object)) requires the real Mediator instance. \" +");
        sb.AppendLine("                    \"When mocking, use the explicit generic overload: sender.Send<TRequest, TResponse>(request).\");");
        sb.AppendLine("            var serviceProvider = __accessor.ServiceProvider;");
        sb.AppendLine("            return global::DSoftStudio.Mediator.RequestObjectDispatch.Dispatch(request, serviceProvider, cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine();

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

    internal readonly struct RequestResponsePair(string requestType, string responseType) : System.IEquatable<RequestResponsePair>
    {
        public string RequestType { get; } = requestType;
        public string ResponseType { get; } = responseType;

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
