// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Shared helpers for interceptor generators and typed extension generators.
/// Pure static methods — no allocations, no state.
/// </summary>
internal static class InterceptorHelpers
{
    // ── Shared dispatch body builders ────────────────────────────────

    /// <summary>
    /// Appends the inline <c>Send</c> dispatch body (argument validation, service provider
    /// access, pipeline chain resolution, handler cache fallback) to <paramref name="sb"/>.
    /// <para>
    /// Used by both <see cref="SendInterceptorGenerator"/> (interceptor methods) and
    /// <see cref="MediatorExtensionsGenerator"/> (typed extension methods) so the dispatch
    /// logic is defined in a single place.
    /// </para>
    /// </summary>
    /// <param name="sb">Target builder.</param>
    /// <param name="requestType">Fully-qualified request type name.</param>
    /// <param name="responseType">Fully-qualified response type name.</param>
    /// <param name="isRelease"><see langword="true"/> for <b>interceptors</b> in Release builds
    /// (branchless castclass — test projects suppress interceptors via
    /// <c>DSoftMediatorSuppressInterceptors</c>); <see langword="false"/> for <b>typed extensions</b>
    /// and Debug interceptors (isinst with graceful virtual-dispatch fallback, ~1–2 CPU cycles
    /// overhead, ensuring consistent behaviour across Debug/Release including CI
    /// <c>dotnet test -c Release</c> pipelines).</param>
    /// <param name="indent">Whitespace prefix for each emitted line.</param>
    public static void AppendSendDispatchBody(
        StringBuilder sb,
        string requestType,
        string responseType,
        bool isRelease,
        string indent)
        => AppendSendDispatchBody(sb, requestType, responseType, isRelease, indent,
            concreteCacheClassName: null, emitAggressive: false);

    /// <summary>
    /// Overload with the ADR-0065 fast paths.
    /// <para>
    /// SAFE: when <paramref name="concreteCacheClassName"/> is non-null the no-pipeline tail
    /// dispatches through the emitted concrete-typed cache (see
    /// <see cref="AppendConcreteCacheClass"/>) instead of the interface-typed
    /// <c>HandlerCache</c>, devirtualizing the final <c>Handle</c> call. Callers pass a class
    /// name only when the request type maps to exactly ONE concrete handler nameable from this
    /// compilation.
    /// </para>
    /// <para>
    /// AGGRESSIVE (default-ON, disable via <c>DSoftMediatorDisableAggressive</c>): a null-gated
    /// armed-holder prologue — non-null means "Singleton effective lifetime, no pipeline chain,
    /// single container" (armed lazily by the cache's SlowPath under
    /// <c>AggressiveDispatch&lt;,&gt;</c> gating, disarmed by the process latch on a second
    /// container). The armed check SUBSUMES the <c>HasPipelineChain</c> read. Placement differs
    /// by variant: Release interceptors check before the castclass (a non-Mediator sender threw
    /// there anyway); defensive bodies check AFTER the accessor probe so a mock
    /// <c>ISender</c> still gets its virtual <c>Send</c> — mock behavior is unchanged.
    /// </para>
    /// </summary>
    public static void AppendSendDispatchBody(
        StringBuilder sb,
        string requestType,
        string responseType,
        bool isRelease,
        string indent,
        string? concreteCacheClassName,
        bool emitAggressive = false)
    {
        var i2 = indent + "    ";
        var i3 = indent + "        ";
        bool aggressive = emitAggressive && concreteCacheClassName is not null;

        void AppendArmedGate()
        {
            sb.Append(indent).Append("var __armed = ").Append(concreteCacheClassName).AppendLine(".Armed;");
            sb.Append(indent).AppendLine("if (__armed is not null)");
            sb.Append(i2).AppendLine("return __armed.Handle(request, cancellationToken);");
        }

        sb.Append(indent).AppendLine("global::System.ArgumentNullException.ThrowIfNull(request);");

        if (isRelease)
        {
            if (aggressive)
                AppendArmedGate();

            // Interceptor Release path: branchless castclass — GDV devirtualizes to ~0 ns.
            // Safe because test projects suppress interceptors via DSoftMediatorSuppressInterceptors.
            sb.Append(indent).AppendLine("var sp = ((global::DSoftStudio.Mediator.IServiceProviderAccessor)sender).ServiceProvider;");
        }
        else
        {
            // Defensive dispatch: isinst + virtual-dispatch fallback for mock/test-double safety.
            // Used by typed extensions (always) and interceptors (Debug only). ~1–2 cycle overhead.
            sb.Append(indent).AppendLine("if (sender is not global::DSoftStudio.Mediator.IServiceProviderAccessor __spa)");
            sb.Append(i2).Append("return sender.Send<")
              .Append(requestType).Append(", ").Append(responseType)
              .AppendLine(">(request, cancellationToken);");

            if (aggressive)
                AppendArmedGate();

            sb.Append(indent).AppendLine("var sp = __spa.ServiceProvider;");
        }

        AppendChainProbe(sb, requestType, responseType, providerVar: "sp", chainVar: "chain", indent,
            onChainHit: () => sb.Append(i3).AppendLine("return chain.Handle(request, cancellationToken);"));

        if (concreteCacheClassName is not null)
        {
            // ADR-0065 SAFE tier: concrete-typed provider-keyed cache — devirtualized Handle.
            sb.Append(indent).Append("return ").Append(concreteCacheClassName)
              .AppendLine(".Dispatch(sp, request, cancellationToken);");
        }
        else
        {
            sb.Append(indent).Append("return global::DSoftStudio.Mediator.HandlerCache<")
              .Append(requestType).Append(", ").Append(responseType)
              .AppendLine(">.Resolve(sp).Handle(request, cancellationToken);");
        }
    }

    /// <summary>
    /// Emits the pipeline-chain probe shared by every dispatch body: the <c>HasPipelineChain</c>
    /// gate, the cache resolve, and the null check. <paramref name="onChainHit"/> writes the
    /// statements that consume the resolved chain.
    /// <para>
    /// The gate stays in the emitted body on purpose: it is a plain static <see langword="bool"/>
    /// read, and for the overwhelmingly common no-chain pair it costs a predicted not-taken branch
    /// while skipping the <c>[ThreadStatic]</c> access inside <c>Resolve</c> entirely.
    /// </para>
    /// <para>
    /// Cacheability is NOT probed here. Whether a chain may be cached per (thread, provider) is one
    /// fact about the pair, settled at registration; <c>PipelineChainCache.Resolve</c> consults it on
    /// its own cold miss path. Emitting that ternary into the dispatch body — as this used to —
    /// duplicated the decision across all four bodies and bought nothing.
    /// </para>
    /// </summary>
    private static void AppendChainProbe(
        StringBuilder sb,
        string requestType,
        string responseType,
        string providerVar,
        string chainVar,
        string indent,
        System.Action onChainHit)
    {
        var i2 = indent + "    ";

        sb.Append(indent).Append("if (global::DSoftStudio.Mediator.RequestDispatch<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.HasPipelineChain)");
        sb.Append(indent).AppendLine("{");
        sb.Append(i2).Append("var ").Append(chainVar)
          .Append(" = global::DSoftStudio.Mediator.PipelineChainCache<")
          .Append(requestType).Append(", ").Append(responseType)
          .Append(">.Resolve(").Append(providerVar).AppendLine(");");
        sb.Append(i2).Append("if (").Append(chainVar).AppendLine(" is not null)");
        onChainHit();
        sb.Append(indent).AppendLine("}");
    }

    /// <summary>
    /// Appends the <c>Send(object)</c> dispatch body — the weakly-typed sibling of
    /// <see cref="AppendSendDispatchBody(StringBuilder, string, string, bool, string, string?, bool)"/>,
    /// which returns <c>ValueTask&lt;object?&gt;</c> and therefore boxes the response.
    /// <para>
    /// Two generators emit this: <c>MediatorExtensionsGenerator</c> (the closed-generic
    /// <c>Send(object)</c> type switch) and <c>MediatorPipelineGenerator</c> (the AOT-safe
    /// <c>RequestObjectDispatch</c> delegate, where the types are the open <c>TRequest</c> /
    /// <c>TResponse</c>). They differ only in identifiers, so both pass their own names here rather
    /// than keeping two hand-maintained copies of the same protocol.
    /// </para>
    /// <para>
    /// Both call sites must have an <c>AwaitAndBox&lt;T&gt;</c> helper in scope; the sync fast path
    /// reads <c>.Result</c> directly so a synchronously-completed handler allocates no state machine.
    /// </para>
    /// </summary>
    /// <param name="requestVar">Identifier holding the strongly-typed request.</param>
    /// <param name="providerVar">Identifier holding the <c>IServiceProvider</c>.</param>
    /// <param name="ctVar">Identifier holding the <c>CancellationToken</c>.</param>
    /// <param name="resultVar">Identifier to declare for the un-boxed <c>ValueTask&lt;TResponse&gt;</c>.</param>
    /// <param name="concreteCacheClassName">ADR-0065 SAFE-tier cache class, or <see langword="null"/>
    /// to fall back to the interface-typed <c>HandlerCache</c>.</param>
    public static void AppendSendObjectDispatchBody(
        StringBuilder sb,
        string requestType,
        string responseType,
        string requestVar,
        string providerVar,
        string ctVar,
        string resultVar,
        string indent,
        string? concreteCacheClassName)
    {
        var i2 = indent + "    ";
        var i3 = indent + "        ";

        void AppendBoxingReturn(string at)
        {
            sb.Append(at).Append("return ").Append(resultVar).AppendLine(".IsCompletedSuccessfully");
            sb.Append(at).Append("    ? new global::System.Threading.Tasks.ValueTask<object?>(")
              .Append(resultVar).AppendLine(".Result)");
            sb.Append(at).Append("    : AwaitAndBox(").Append(resultVar).AppendLine(");");
        }

        sb.Append(indent).Append("global::System.Threading.Tasks.ValueTask<").Append(responseType)
          .Append("> ").Append(resultVar).AppendLine(";");

        AppendChainProbe(sb, requestType, responseType, providerVar, "__chain", indent, onChainHit: () =>
        {
            sb.Append(i2).AppendLine("{");
            sb.Append(i3).Append(resultVar).Append(" = __chain.Handle(").Append(requestVar)
              .Append(", ").Append(ctVar).AppendLine(");");
            AppendBoxingReturn(i3);
            sb.Append(i2).AppendLine("}");
        });

        if (concreteCacheClassName is not null)
        {
            // ADR-0065 SAFE tier: the same concrete cache the typed Send extension uses (shared TLS pair).
            sb.Append(indent).Append(resultVar).Append(" = ").Append(concreteCacheClassName)
              .Append(".Dispatch(").Append(providerVar).Append(", ").Append(requestVar)
              .Append(", ").Append(ctVar).AppendLine(");");
        }
        else
        {
            sb.Append(indent).Append(resultVar)
              .Append(" = global::DSoftStudio.Mediator.HandlerCache<")
              .Append(requestType).Append(", ").Append(responseType)
              .Append(">.Resolve(").Append(providerVar).Append(").Handle(").Append(requestVar)
              .Append(", ").Append(ctVar).AppendLine(");");
        }

        AppendBoxingReturn(indent);
    }

    /// <summary>
    /// Emits the ADR-0065 SAFE-tier support class: a file-local, non-generic, per-request-type
    /// cache whose mechanics mirror <c>HandlerCache&lt;TReq,TRes&gt;</c> (provider-keyed
    /// <c>[ThreadStatic]</c> pair + <c>ReferenceEquals</c> guard) but whose cached field is typed
    /// to the CONCRETE handler, so the final <c>Handle</c> call devirtualizes (measured net11
    /// 6.41 → 5.05 ns; net8 8.62 → 7.72; net10 neutral — see the ADR-0065 Amendment §A3/§A4).
    /// <para>
    /// Correctness: the miss path resolves through the existing interface-typed
    /// <c>HandlerCache</c> (keeping it as the L2 tier) and only caches when the resolved
    /// instance is EXACTLY the generator-known concrete type — a user override, decorator,
    /// test replacement, or SUBCLASS (which could hide <c>Handle</c> with <c>new</c>) degrades
    /// gracefully to interface dispatch on every call (never an <c>InvalidCastException</c>),
    /// at today's cached-resolve cost. Correct for multiple containers; lifetime pinning
    /// envelope matches <c>HandlerCache</c>, to which the miss path delegates (Transient
    /// handlers get the same per-(thread, provider) pinning both tiers share).
    /// </para>
    /// </summary>
    /// <summary>
    /// The ONE cache-class name for a (request, response) pair, derived from the types so both
    /// Send emitters compute the same string.
    /// <para>
    /// It used to be index-derived, and the two emitters index differently:
    /// <c>SendInterceptorGenerator</c> by call-site group index, <c>MediatorExtensionsGenerator</c>
    /// by request index. Emitted <c>file</c>-local in two different files, that produced TWO rival
    /// holders for the same pair — while <c>AggressiveDispatch&lt;,&gt;.TryArm</c> is one-shot
    /// (AggressiveDispatch.cs:279). Whichever call form dispatched first consumed the single arm
    /// attempt and the other holder stayed null forever, so "armed" was a property of which call
    /// FORM ran first, not of the request type. Same fix ADR-0066 already applied on the Publish
    /// side (NotificationFastPath.cs:134).
    /// </para>
    /// </summary>
    public static string ConcreteCacheName(string requestType, string responseType)
        => "__SendConcreteCache_"
           + HandlerDiscovery.SanitizeIdentifier(requestType)
           + "_"
           + HandlerDiscovery.SanitizeIdentifier(responseType);

    public static void AppendConcreteCacheClass(
        StringBuilder sb,
        string cacheClassName,
        string requestType,
        string responseType,
        string handlerType,
        bool emitAggressive = false)
    {
        // internal, NOT file-local: MediatorExtensionsGenerator OWNS these classes and
        // SendInterceptorGenerator references them from its own generated file, so the two call
        // forms share one holder and one arm attempt. (Mirrors NotificationFastPath.cs:148-149.)
        sb.Append("    internal static class ").AppendLine(cacheClassName);
        sb.AppendLine("    {");

        if (emitAggressive)
        {
            // AGGRESSIVE holder: non-null <=> armed <=> (Singleton, no chain, single container).
            // Written only via AggressiveDispatch<,>.TryArm (under the process latch) and the
            // registered disarm callback — the fast path reads it with Volatile (no CSE/hoisting).
            sb.Append("        private static ").Append(handlerType).AppendLine("? _armed;");
            sb.AppendLine();
            sb.Append("        internal static ").Append(handlerType).AppendLine("? Armed");
            sb.AppendLine("        {");
            sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine("            get => global::System.Threading.Volatile.Read(ref _armed);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // One slot object instead of two [ThreadStatic] fields: one thread-local lookup on the hot
        // path instead of two, and — the reason it exists — a slot CAN be emptied by the thread that
        // disposes the scope, which a [ThreadStatic] never can. Without that, this cache kept the
        // disposed scope and everything it resolved reachable until this same thread happened to
        // dispatch this same request type again against a different provider.
        sb.Append("        [global::System.ThreadStatic] private static global::DSoftStudio.Mediator.DispatchCacheSlot<")
          .Append(handlerType).AppendLine(">? _slot;");
        sb.AppendLine();
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.Append("        internal static global::System.Threading.Tasks.ValueTask<").Append(responseType)
          .Append("> Dispatch(global::System.IServiceProvider sp, ").Append(requestType)
          .AppendLine(" request, global::System.Threading.CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.AppendLine("            var __slot = _slot;");
        sb.AppendLine("            if (__slot is not null && global::System.Object.ReferenceEquals(__slot.Provider, sp))");
        sb.AppendLine("            {");
        sb.AppendLine("                var __h = __slot.Value;");
        sb.AppendLine("                if (__h is not null)");
        sb.AppendLine("                    return __h.Handle(request, cancellationToken);");
        sb.AppendLine("            }");
        sb.AppendLine("            return ResolveSlow(sp, request, cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.Append("        private static global::System.Threading.Tasks.ValueTask<").Append(responseType)
          .Append("> ResolveSlow(global::System.IServiceProvider sp, ").Append(requestType)
          .AppendLine(" request, global::System.Threading.CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.Append("            var svc = global::DSoftStudio.Mediator.HandlerCache<").Append(requestType)
          .Append(", ").Append(responseType).AppendLine(">.Resolve(sp);");
        // EXACT-type guard (not `is`): a runtime-registered SUBCLASS of the mapped handler that
        // hides Handle (`new`) would statically bind to the base implementation through the
        // concrete-typed call. Exact match keeps such overrides on interface dispatch.
        // The lifetime verdict is NOT re-derived here. HandlerCache.Resolve, called on the line
        // above, has already asked this container whether the instance may be reused and stored the
        // answer for this (thread, provider); IsCacheableFor reads it back. Deriving it a second
        // time would put one decision in two places and let them drift. Without this check the
        // concrete tier pinned Transient handlers even after HandlerCache stopped doing so.
        sb.Append("            if (svc.GetType() == typeof(").Append(handlerType)
          .Append(") && global::DSoftStudio.Mediator.HandlerCache<").Append(requestType)
          .Append(", ").Append(responseType).AppendLine(">.IsCacheableFor(sp))");
        sb.AppendLine("            {");
        sb.Append("                var concrete = (").Append(handlerType).AppendLine(")svc;");
        sb.AppendLine("                var __s = _slot ??= new global::DSoftStudio.Mediator.DispatchCacheSlot<" + handlerType + ">();");
        sb.AppendLine("                __s.Value = concrete;");
        sb.AppendLine("                __s.Provider = sp;");
        sb.AppendLine("                global::DSoftStudio.Mediator.DispatchCacheReleaser.Track(sp, __s);");

        if (emitAggressive)
        {
            sb.AppendLine();
            sb.AppendLine("                // AGGRESSIVE arming: one-shot, miss-path only. TryArm re-verifies the winning");
            sb.AppendLine("                // descriptor at arm time and runs the arm under the process latch (poison-safe).");
            sb.Append("                if (global::DSoftStudio.Mediator.AggressiveDispatch<").Append(requestType)
              .Append(", ").Append(responseType).AppendLine(">.ShouldAttemptArm)");
            sb.AppendLine("                {");
            sb.Append("                    global::DSoftStudio.Mediator.AggressiveDispatch<").Append(requestType)
              .Append(", ").Append(responseType).AppendLine(">.TryArm(");
            sb.AppendLine("                        concrete,");
            sb.AppendLine("                        () => global::System.Threading.Volatile.Write(ref _armed, concrete),");
            sb.AppendLine("                        static () => global::System.Threading.Volatile.Write(ref _armed, null));");
            sb.AppendLine("                }");
        }

        sb.AppendLine("                return concrete.Handle(request, cancellationToken);");
        sb.AppendLine("            }");
        sb.AppendLine("            return svc.Handle(request, cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Appends the inline <c>CreateStream</c> dispatch body (argument validation, service
    /// provider access, stream pipeline chain resolution, handler cache fallback) to
    /// <paramref name="sb"/>.
    /// <para>
    /// Used by both <see cref="StreamInterceptorGenerator"/> (interceptor methods) and
    /// <see cref="MediatorExtensionsGenerator"/> (typed extension methods).
    /// </para>
    /// </summary>
    public static void AppendStreamDispatchBody(
        StringBuilder sb,
        string requestType,
        string responseType,
        bool isRelease,
        string indent)
    {
        var i2 = indent + "    ";

        sb.Append(indent).AppendLine("global::System.ArgumentNullException.ThrowIfNull(request);");

        if (isRelease)
        {
            // Interceptor Release path: branchless castclass — GDV devirtualizes to ~0 ns.
            // Safe because test projects suppress interceptors via DSoftMediatorSuppressInterceptors.
            sb.Append(indent).AppendLine("var sp = ((global::DSoftStudio.Mediator.IServiceProviderAccessor)mediator).ServiceProvider;");
        }
        else
        {
            // Defensive dispatch: isinst + virtual-dispatch fallback for mock/test-double safety.
            // Used by typed extensions (always) and interceptors (Debug only). ~1–2 cycle overhead.
            sb.Append(indent).AppendLine("if (mediator is not global::DSoftStudio.Mediator.IServiceProviderAccessor __spa)");
            sb.Append(i2).Append("return mediator.CreateStream<")
              .Append(requestType).Append(", ").Append(responseType)
              .AppendLine(">(request, cancellationToken);");
            sb.Append(indent).AppendLine("var sp = __spa.ServiceProvider;");
        }

        // Behaviors path: Resolve decides for itself whether this chain may be cached per
        // (thread, provider) — see StreamPipelineChainCache. Returns null when no chain is registered.
        sb.Append(indent).Append("var chain = global::DSoftStudio.Mediator.StreamPipelineChainCache<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.Resolve(sp);");

        sb.Append(indent).AppendLine("if (chain is not null)");
        sb.Append(i2).AppendLine("return chain.Handle(request, cancellationToken);");

        // No-behaviors fast path: resolve the stream handler directly via its ThreadStatic cache.
        // Resolve already raises the "not registered" InvalidOperationException — with a message that
        // also names the PrecompileStreams() fix — when the Handler factory is missing, so the emitted
        // body does not re-check it. The guard that used to sit here was a third copy of that
        // contract, read on every stream dispatch to produce a strictly worse exception.
        sb.Append(indent).Append("return global::DSoftStudio.Mediator.StreamHandlerCache<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.Resolve(sp).Handle(request, cancellationToken);");
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is — or transitively contains — a
    /// type parameter (an open / not-fully-constructed type).
    /// <para>
    /// An interceptor must reference fully-constructed, concrete types: the
    /// <c>[InterceptsLocation]</c> mechanism rewrites a single syntactic call site, but an open-generic
    /// call site (e.g. <c>mediator.Send&lt;TRequest, TResponse&gt;(request)</c> inside a generic forwarding
    /// method) is instantiated for every set of type arguments the enclosing method is called with —
    /// no single concrete interceptor can represent all of them. Emitting one anyway produces a method
    /// that references the bare type-parameter names out of scope (CS0246). Such call sites must be
    /// skipped so they dispatch through the real <c>Mediator.Send/Publish/CreateStream</c> at runtime.
    /// </para>
    /// </summary>
    public static bool ContainsTypeParameter(ITypeSymbol? type) => type switch
    {
        null => false,
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
        IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsTypeParameter),
        _ => false,
    };

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

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="node"/> is located inside a lambda
    /// expression whose converted type is <see cref="System.Linq.Expressions.Expression{TDelegate}"/>.
    /// <para>
    /// Interceptors must NOT rewrite call sites inside expression trees because the
    /// rewritten static extension method invocation is incompatible with expression
    /// tree compilation. This also prevents breaking mocking frameworks (Moq, NSubstitute,
    /// FakeItEasy) that inspect the expression tree passed to Setup/Verify/Received.
    /// </para>
    /// </summary>
    public static bool IsInsideExpressionTreeLambda(
        SemanticModel semanticModel,
        SyntaxNode node,
        CancellationToken ct)
    {
        var expressionOfT = semanticModel.Compilation
            .GetTypeByMetadataName("System.Linq.Expressions.Expression`1");

        if (expressionOfT is null)
            return false;

        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (current is LambdaExpressionSyntax lambda)
            {
                var typeInfo = semanticModel.GetTypeInfo(lambda, ct);
                if (typeInfo.ConvertedType is INamedTypeSymbol convertedType
                    && SymbolEqualityComparer.Default.Equals(
                        convertedType.OriginalDefinition, expressionOfT))
                {
                    return true;
                }
            }

            current = current.Parent;
        }

        return false;
    }
}
