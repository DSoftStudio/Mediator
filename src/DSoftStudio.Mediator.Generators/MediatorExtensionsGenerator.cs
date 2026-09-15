// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
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

        // ── Handler map (ADR-0065 SAFE fast path) ────────────────
        // (requestType, responseType) → unique nameable concrete handler; pairs found here get a
        // concrete-typed dispatch cache in the typed Send extension + Send(object) case body.
        var localHandlerMap = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => SendFastPath.GetLocalHandlerMapEntry(ctx, ct))
            .Where(static entry => entry is not null)
            .Select(static (entry, _) => entry!.Value)
            .Collect();

        var externalHandlerMap = context.CompilationProvider
            .Select(static (compilation, _) => new EquatableArray<SendFastPath.HandlerMapEntry>(
                ReferencedAssemblyScanner.GetExternalRequestHandlerMap(compilation)
                    .Select(static e => new SendFastPath.HandlerMapEntry(e.RequestType, e.ResponseType, e.HandlerType))
                    .OrderBy(static e => e.RequestType, System.StringComparer.Ordinal)
                    .ThenBy(static e => e.HandlerType, System.StringComparer.Ordinal)
                    .ToArray()));

        // ── Combine and emit ─────────────────────────────────────
        var assemblyName = context.CompilationProvider
            .Select(static (c, _) => c.AssemblyName ?? "Assembly");

        var combined = localRequests
            .Combine(externalRequests)
            .Combine(selfCollected)
            .Combine(localStreams)
            .Combine(externalStreams)
            .Combine(assemblyName)
            .Combine(localHandlerMap)
            .Combine(externalHandlerMap)
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            var ((((((((localReqs, extReqs), selfReqs), localStrs), extStrs), asmName), localHandlers), externalHandlers), optionsProvider) = data;

            // ADR-0065 AGGRESSIVE tier is default-ON; DSoftMediatorDisableAggressive forces it off.
            bool emitAggressive = !(optionsProvider.GlobalOptions.TryGetValue(
                    "build_property.DSoftMediatorDisableAggressive", out var disableAggressive)
                && string.Equals(disableAggressive, "true", System.StringComparison.OrdinalIgnoreCase));

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
                .OrderBy(static p => p.RequestType, StringComparer.Ordinal)
                .ToList();

            var localStrList = localStrs.IsDefaultOrEmpty
                ? []
                : localStrs.Distinct();

            var streams = localStrList
                .Concat(extStrs)
                .Distinct()
                .OrderBy(static p => p.RequestType, StringComparer.Ordinal)
                .ToList();

            var handlerMap = SendFastPath.BuildUniqueHandlerMap(localHandlers, externalHandlers);
            var code = GenerateCode(requests, streams, asmName, handlerMap, emitAggressive);

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

        if (!HandlerDiscovery.IsReferenceableFromGeneratedCode(typeDecl, symbol))
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

        if (!HandlerDiscovery.IsReferenceableFromGeneratedCode(classDecl, symbol))
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

        if (!HandlerDiscovery.IsReferenceableFromGeneratedCode(classDecl, symbol))
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
        string assemblyName,
        Dictionary<(string Request, string Response), string> handlerMap,
        bool emitAggressive)
    {
        // Per-(request, response) concrete cache classes (ADR-0065 SAFE tier). This generator OWNS
        // them: its pair set (local + external + self-handlers) is a superset of the intercepted
        // call sites SendInterceptorGenerator sees, so every cache an interceptor needs exists here.
        // The name is derived from the TYPES (InterceptorHelpers.ConcreteCacheName), not from an
        // index, so SendInterceptorGenerator computes the same name and references THIS class
        // instead of emitting a rival file-local one — one holder per pair, one arm attempt.
        var cacheClasses = new List<(string ClassName, string ReqType, string ResType, string HandlerType)>();
        string? CacheNameFor(in RequestResponsePair pair)
        {
            if (!handlerMap.TryGetValue((pair.RequestType, pair.ResponseType), out var handlerType))
                return null;
            var name = InterceptorHelpers.ConcreteCacheName(pair.RequestType, pair.ResponseType);
            if (!cacheClasses.Exists(c => c.ClassName == name))
                cacheClasses.Add((name, pair.RequestType, pair.ResponseType, handlerType));
            return name;
        }

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
        for (int reqIndex = 0; reqIndex < requests.Count; reqIndex++)
        {
            var pair = requests[reqIndex];
            var cacheClassName = CacheNameFor(pair);

            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Sends a <see cref=\"{EscapeXml(pair.RequestType)}\"/> through the pipeline. Type-inferred shorthand.");
            sb.AppendLine("        /// </summary>");
            // AggressiveInlining: the ADR-0065 armed-gate + concrete-cache body exceeds the
            // inliner's discretionary budget at ordinary call sites (measured: the framed real
            // extension ran ~1.8 ns over the pasted-body shape); forcing the inline recovers it.
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.Append("        public static global::System.Threading.Tasks.ValueTask<");
            sb.Append(pair.ResponseType);
            sb.Append("> Send(this global::DSoftStudio.Mediator.Abstractions.ISender sender, ");
            sb.Append(pair.RequestType);
            sb.AppendLine(" request, global::System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            InterceptorHelpers.AppendSendDispatchBody(sb, pair.RequestType, pair.ResponseType, isRelease: false, "            ", cacheClassName, emitAggressive);
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
        //
        // Each case is a CALL to an outlined body, never the body itself. Pasting the bodies in
        // made this one method grow ~1.2 KB of machine code per request type, with two costs that
        // only showed up as the type count rose:
        //   - past 9 types the method left the inliner's budget, so the whole thing stopped being
        //     inlined into the caller and Send(object) jumped 5.5 -> 9.5 ns at a single new type;
        //   - it kept growing to 29-32 KB by 26-40 types, roughly a whole 32 KB L1 instruction
        //     cache, so each call thrashed it: 14.9 ns at 40 types, 20.5 ns at 80.
        // Outlined, the switch is type tests plus a call and stays flat: 5.4 ns at 26 types,
        // 6.8 ns at 80. Verified against DOTNET_JitDisasm, not inferred.
        if (requests.Count > 0)
        {
            sb.AppendLine("            switch (request)");
            sb.AppendLine("            {");
            for (int i = 0; i < requests.Count; i++)
            {
                var pair = requests[i];
                sb.AppendLine($"                case {pair.RequestType} __r{i}:");
                sb.AppendLine($"                    return {SendObjectCaseName(pair)}(__sp, __r{i}, cancellationToken);");
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

        // ── Send(object) outlined case bodies ────────────────────
        // NoInlining is load-bearing, not caution: without it the JIT pulls every one of these
        // back into the switch and rebuilds exactly the oversized method this outlining exists to
        // avoid. SendObjectOutliningTests covers that.
        foreach (var pair in requests)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Send(object) dispatch for <see cref=\"{EscapeXml(pair.RequestType)}\"/>, kept out of the switch.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            sb.Append("        private static global::System.Threading.Tasks.ValueTask<object?> ")
              .Append(SendObjectCaseName(pair))
              .Append("(global::System.IServiceProvider __sp, ")
              .Append(pair.RequestType)
              .AppendLine(" __r, global::System.Threading.CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            EmitSendObjectCaseBody(sb, pair.RequestType, pair.ResponseType, "__r", "            ", CacheNameFor(pair));
            sb.AppendLine("        }");
            sb.AppendLine();
        }

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

        // ADR-0065 SAFE tier: file-local concrete cache classes, siblings of the extensions class.
        foreach (var cache in cacheClasses)
        {
            sb.AppendLine();
            InterceptorHelpers.AppendConcreteCacheClass(
                sb, cache.ClassName, cache.ReqType, cache.ResType, cache.HandlerType, emitAggressive);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeXml(string input)
        => input.Replace("<", "{").Replace(">", "}");

    /// <summary>
    /// The name of the outlined <c>Send(object)</c> body for a pair. Derived from the TYPES, like
    /// <see cref="InterceptorHelpers.ConcreteCacheName"/>, so it is stable across builds and unique
    /// per pair without depending on the order the switch happens to emit its cases in.
    /// </summary>
    private static string SendObjectCaseName(in RequestResponsePair pair)
        => "__SendObjectCase_"
           + HandlerDiscovery.SanitizeIdentifier(pair.RequestType)
           + "_"
           + HandlerDiscovery.SanitizeIdentifier(pair.ResponseType);

    /// <summary>
    /// Emits the dispatch body for a single request type, into its own method rather than into the
    /// Send(object) switch — see the outlining note at the switch for why that placement matters.
    /// The protocol itself lives in <see cref="InterceptorHelpers.AppendSendObjectDispatchBody"/>,
    /// shared with the AOT-safe <c>RequestObjectDispatch</c> delegate; only the identifiers differ.
    /// </summary>
    private static void EmitSendObjectCaseBody(
        StringBuilder sb,
        string requestType,
        string responseType,
        string varName,
        string indent,
        string? concreteCacheClassName)
        => InterceptorHelpers.AppendSendObjectDispatchBody(
            sb, requestType, responseType,
            requestVar: varName,
            providerVar: "__sp",
            ctVar: "cancellationToken",
            resultVar: "__vt",
            indent,
            concreteCacheClassName);

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
