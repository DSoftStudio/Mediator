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
        // NOTE: Typed extensions ALWAYS use isRelease: false (isinst + graceful
        // fallback) rather than honouring the compilation's OptimizationLevel.
        //
        // Unlike interceptors — which are transparent, call-site-specific rewrites
        // that test projects suppress via DSoftMediatorSuppressInterceptors — typed
        // extensions are PUBLIC API surface generated into every referencing project,
        // including test projects that exercise mock ISender implementations.
        //
        // The isinst check costs ~1-2 extra CPU cycles vs. castclass on the hot
        // path (< 0.05% of total request processing) while guaranteeing consistent
        // behaviour across Debug and Release builds — critical because enterprise
        // CI/CD pipelines routinely run `dotnet test -c Release`.
        foreach (var pair in requests)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Sends a <see cref=\"{EscapeXml(pair.RequestType)}\"/> through the pipeline. Type-inferred shorthand.");
            sb.AppendLine("        /// </summary>");
            sb.Append("        public static global::System.Threading.Tasks.ValueTask<");
            sb.Append(pair.ResponseType);
            sb.Append("> Send(this global::DSoftStudio.Mediator.Abstractions.ISender sender, ");
            sb.Append(pair.RequestType);
            sb.AppendLine(" request, global::System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            InterceptorHelpers.AppendSendDispatchBody(sb, pair.RequestType, pair.ResponseType, isRelease: false, "            ");
            sb.AppendLine("        }");
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
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        public static global::System.Threading.Tasks.ValueTask<object?> Send(");
        sb.AppendLine("            this global::DSoftStudio.Mediator.Abstractions.ISender sender,");
        sb.AppendLine("            object request,");
        sb.AppendLine("            global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(request);");
        sb.AppendLine("            // Mock-safe guard: 'is not' pattern avoids castclass so mock/test-double");
        sb.AppendLine("            // ISender instances get a clear InvalidOperationException instead of");
        sb.AppendLine("            // InvalidCastException. The throw lives in a [DoesNotReturn] helper,");
        sb.AppendLine("            // keeping this method free of IL throw instructions for JIT inlining.");
        sb.AppendLine("            if (sender is not global::DSoftStudio.Mediator.IServiceProviderAccessor __acc)");
        sb.AppendLine("            {");
        sb.AppendLine("                ThrowSenderNotMediator();");
        sb.AppendLine("                return default; // unreachable — satisfies definite assignment analysis");
        sb.AppendLine("            }");
        sb.AppendLine("            var __sp = __acc.ServiceProvider;");

        // Source-generated type switch: eliminates FrozenDictionary lookup + delegate
        // invocation (~3-5 ns saving). Falls back to RequestObjectDispatch for types
        // not known at compile time (e.g. from referenced assemblies without source).
        if (requests.Count > 0)
        {
            sb.AppendLine("            switch (request)");
            sb.AppendLine("            {");
            for (int i = 0; i < requests.Count; i++)
            {
                var pair = requests[i];
                sb.AppendLine($"                case {pair.RequestType} __r{i}:");
                sb.AppendLine("                {");
                EmitSendObjectCaseBody(sb, pair.RequestType, pair.ResponseType, $"__r{i}", "                    ");
                sb.AppendLine("                }");
            }
            sb.AppendLine("                default:");
            sb.AppendLine("                    return global::DSoftStudio.Mediator.RequestObjectDispatch.Dispatch(request, __sp, cancellationToken);");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("            return global::DSoftStudio.Mediator.RequestObjectDispatch.Dispatch(request, __sp, cancellationToken);");
        }
        sb.AppendLine("        }");
        sb.AppendLine();

        // ── CreateStream extensions ──────────────────────────────
        // Same defensive dispatch rationale as Send extensions above.
        foreach (var pair in streams)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Creates an async stream from a <see cref=\"{EscapeXml(pair.RequestType)}\"/>. Type-inferred shorthand.");
            sb.AppendLine("        /// </summary>");
            sb.Append("        public static global::System.Collections.Generic.IAsyncEnumerable<");
            sb.Append(pair.ResponseType);
            sb.Append("> CreateStream(this global::DSoftStudio.Mediator.Abstractions.IMediator mediator, ");
            sb.Append(pair.RequestType);
            sb.AppendLine(" request, global::System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            InterceptorHelpers.AppendStreamDispatchBody(sb, pair.RequestType, pair.ResponseType, isRelease: false, "            ");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // AwaitAndBox helper for Send(object) type switch — boxes async results.
        if (requests.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Async fallback: awaits the result and boxes it. Only allocated when the handler is truly async.</summary>");
            sb.AppendLine("        private static async global::System.Threading.Tasks.ValueTask<object?> AwaitAndBox<T>(");
            sb.AppendLine("            global::System.Threading.Tasks.ValueTask<T> task) => await task;");
        }

        // ThrowSenderNotMediator — mock-safe guard for Send(object).
        // [DoesNotReturn] + [NoInlining] keeps the throw out of the caller's IL.
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Throws when Send(object) is called on a non-Mediator ISender (e.g. mock/test double).</summary>");
        sb.AppendLine("        [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("        private static void ThrowSenderNotMediator()");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new global::System.InvalidOperationException(");
        sb.AppendLine("                \"Send(object) requires the real Mediator (IServiceProviderAccessor). \" +");
        sb.AppendLine("                \"For test doubles, use the explicit generic overload sender.Send<TRequest, TResponse>(request).\");");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeXml(string input)
        => input.Replace("<", "{").Replace(">", "}");

    /// <summary>
    /// Emits the inline dispatch body for a single request type inside the
    /// Send(object) type switch. Includes pipeline chain check + handler cache
    /// + sync fast-path boxing.
    /// </summary>
    private static void EmitSendObjectCaseBody(
        StringBuilder sb,
        string requestType,
        string responseType,
        string varName,
        string indent)
    {
        sb.Append(indent).AppendLine($"global::System.Threading.Tasks.ValueTask<{responseType}> __vt;");
        sb.Append(indent).AppendLine($"if (global::DSoftStudio.Mediator.RequestDispatch<{requestType}, {responseType}>.HasPipelineChain)");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).AppendLine($"    var __chain = global::DSoftStudio.Mediator.RequestDispatch<{requestType}, {responseType}>.IsPipelineChainCacheable");
        sb.Append(indent).AppendLine($"        ? global::DSoftStudio.Mediator.PipelineChainCache<{requestType}, {responseType}>.Resolve(__sp)");
        sb.Append(indent).AppendLine($"        : global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<global::DSoftStudio.Mediator.PipelineChainHandler<{requestType}, {responseType}>>(__sp);");
        sb.Append(indent).AppendLine("    if (__chain is not null)");
        sb.Append(indent).AppendLine("    {");
        sb.Append(indent).AppendLine($"        __vt = __chain.Handle({varName}, cancellationToken);");
        sb.Append(indent).AppendLine("        return __vt.IsCompletedSuccessfully");
        sb.Append(indent).AppendLine("            ? new global::System.Threading.Tasks.ValueTask<object?>(__vt.Result)");
        sb.Append(indent).AppendLine("            : AwaitAndBox(__vt);");
        sb.Append(indent).AppendLine("    }");
        sb.Append(indent).AppendLine("}");
        sb.Append(indent).AppendLine($"__vt = global::DSoftStudio.Mediator.HandlerCache<{requestType}, {responseType}>.Resolve(__sp).Handle({varName}, cancellationToken);");
        sb.Append(indent).AppendLine("return __vt.IsCompletedSuccessfully");
        sb.Append(indent).AppendLine("    ? new global::System.Threading.Tasks.ValueTask<object?>(__vt.Result)");
        sb.Append(indent).AppendLine("    : AwaitAndBox(__vt);");
    }

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
