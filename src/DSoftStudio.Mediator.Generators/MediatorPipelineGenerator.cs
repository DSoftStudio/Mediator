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
/// Incremental source generator that detects all implementations of
/// IRequestHandler&lt;TRequest, TResponse&gt; and generates a MediatorRegistry
/// class that precompiles all dispatch pipelines at startup.
/// </summary>
[Generator]
public sealed class MediatorPipelineGenerator : IIncrementalGenerator
{
    private const string HandlerInterfaceMetadataName =
        "DSoftStudio.Mediator.Abstractions.IRequestHandler`2";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Check once per compilation if IRequestHandler exists
        var hasHandlerInterface = context.CompilationProvider
            .Select(static (compilation, _) =>
                compilation.GetTypeByMetadataName(HandlerInterfaceMetadataName) is not null);

        // Only classes with base types enter semantic analysis
        var handlerInfos = context.SyntaxProvider
        .CreateSyntaxProvider(
            predicate: static (node, _) =>
                node is ClassDeclarationSyntax { BaseList: not null },
            transform: static (ctx, ct) => GetHandlerInfo(ctx, ct))
        .Where(static info => info is not null)
        .Select(static (info, _) => info!.Value);

        var localCollected = handlerInfos.Collect();

        // Scan referenced assemblies for IRequestHandler registrations
        var externalHandlers = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var external = ReferencedAssemblyScanner.GetExternalPipelineHandlers(compilation);
                var array = external
                    .Select(e => new HandlerInfo(e.RequestType, e.ResponseType))
                    .OrderBy(static h => h.RequestType, StringComparer.Ordinal)
                    .ThenBy(static h => h.ResponseType, StringComparer.Ordinal)
                    .ToArray();
                return new EquatableArray<HandlerInfo>(array);
            });

        // Discover self-handling request classes (IRequest<T> + static Execute)
        var selfHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null }
                    || node is RecordDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => GetSelfHandlerPair(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var selfCollected = selfHandlers.Collect();

        // Discover open-generic pipeline behavior types (local + external)
        // for AOT-safe closed-generic DI registration.
        var allBehaviors = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var results = ReferencedAssemblyScanner.GetExternalOpenGenericBehaviors(compilation);

                // Also scan the current compilation for local behavior types
                CollectLocalBehaviors(compilation.Assembly.GlobalNamespace, results);

                var array = results
                    .Distinct()
                    .OrderBy(static b => b.BaseTypeName, StringComparer.Ordinal)
                    .ToArray();
                return new EquatableArray<BehaviorTypeInfo>(array);
            });

        var assemblyName = context.CompilationProvider
            .Select(static (c, _) => c.AssemblyName ?? "Assembly");

        // Registration call sites, for PREDICTING the ordered behavior chain of each pair. Incomplete
        // by nature — see BehaviorRegistrationScanner — but never load-bearing: the emitted factory
        // verifies the prediction against the resolved instances and falls back on any mismatch.
        var behaviorRegistrations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => BehaviorRegistrationScanner.IsCandidate(node),
                transform: static (ctx, ct) => BehaviorRegistrationScanner.Resolve(ctx, ct))
            .Where(static r => r is not null)
            .Select(static (r, _) => r!.Value)
            .Collect();

        var combined = localCollected
            .Combine(hasHandlerInterface)
            .Combine(externalHandlers)
            .Combine(selfCollected)
            .Combine(allBehaviors)
            .Combine(assemblyName)
            .Combine(behaviorRegistrations);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var ((((((localHandlers, interfaceExists), external), selfHandlers), behaviors), asmName), registrations) = pair;

            var hasSelfHandlers = !selfHandlers.IsDefaultOrEmpty && selfHandlers.Length > 0;

            if (!interfaceExists && external.Length == 0 && !hasSelfHandlers)
            {
                spc.AddSource(
                    "MediatorRegistry.g.cs",
                    SourceText.From(
                        GenerateRegistryCode([], asmName, behaviors, registrations),
                        Encoding.UTF8));
                return;
            }

            // Merge local + external + self-handlers, deduplicate
            var localList = localHandlers.IsDefaultOrEmpty
                ? []
                : localHandlers.Distinct();

            IEnumerable<HandlerInfo> selfPairs = hasSelfHandlers
                ? selfHandlers.Select(static s => new HandlerInfo(s.RequestType, s.ResponseType))
                : [];

            var uniqueRegistrations = localList
                .Concat(external)
                .Concat(selfPairs)
                .Distinct()
                .OrderBy(static h => h.RequestType, StringComparer.Ordinal)
                .ThenBy(static h => h.ResponseType, StringComparer.Ordinal)
                .ToList();

            var code = GenerateRegistryCode(uniqueRegistrations, asmName, behaviors, registrations);

            spc.AddSource(
                "MediatorRegistry.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
    }

    /// <summary>
    /// Extracts (requestType, responseType) from self-handling request classes
    /// for pipeline chain registration.
    /// </summary>
    private static SelfHandlerDetail? GetSelfHandlerPair(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var typeDecl = (TypeDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(typeDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract || symbol.TypeKind != TypeKind.Class || symbol.TypeParameters.Length > 0)
            return null;

        if (!HandlerDiscovery.IsReferenceableFromGeneratedCode(typeDecl, symbol))
            return null;

        if (!HandlerDiscovery.TryGetSelfHandlingRequest(symbol, ct, out var detail))
            return null;

        return detail;
    }

    private static HandlerInfo? GetHandlerInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var classDeclaration = (ClassDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDeclaration, ct) is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract ||
            symbol.TypeKind != TypeKind.Class ||
            symbol.TypeParameters.Length > 0)
            return null;

        if (!HandlerDiscovery.IsReferenceableFromGeneratedCode(classDeclaration, symbol))
            return null;

        if (!HandlerDiscovery.TryGetRequestHandler(
                symbol,
                ct,
                out var requestType,
                out var responseType))
            return null;

        return new HandlerInfo(requestType, responseType);
    }

    /// <summary>
    /// Walks the current compilation's namespace tree — and each type's NESTED types — to discover
    /// local open-generic pipeline behavior types (classes that implement
    /// <c>IPipelineBehavior&lt;,&gt;</c>, <c>IRequestPostProcessor&lt;,&gt;</c>,
    /// <c>IRequestExceptionHandler&lt;,&gt;</c>, or <c>IStreamPipelineBehavior&lt;,&gt;</c>).
    /// <para>
    /// Shares <see cref="ReferencedAssemblyScanner.CollectBehaviorsFromTypeTree"/> with the
    /// referenced-assembly scanner so the two discovery paths cannot drift; the only difference is
    /// that <c>internal</c> types are nameable here (the generated registry is in this assembly).
    /// </para>
    /// </summary>
    private static void CollectLocalBehaviors(
        INamespaceSymbol ns,
        List<BehaviorTypeInfo> results)
    {
        foreach (var type in ns.GetTypeMembers())
            ReferencedAssemblyScanner.CollectBehaviorsFromTypeTree(type, results, allowInternal: true);

        foreach (var child in ns.GetNamespaceMembers())
            CollectLocalBehaviors(child, results);
    }

    private static string GenerateRegistryCode(
        List<HandlerInfo> registrations,
        string assemblyName,
        EquatableArray<BehaviorTypeInfo> behaviors,
        System.Collections.Immutable.ImmutableArray<BehaviorRegistration> behaviorRegistrations)
    {
        var sanitizedAsm = HandlerDiscovery.SanitizeIdentifier(assemblyName);
        // Pre-sized: this emitter routinely produces four figures of lines.
        var sb = new StringBuilder(8192);

        // Predicted behavior chain per pair, from registration syntax. Absent for any pair whose
        // registrations this generator cannot read; present-but-wrong is caught at runtime by the
        // emitted factory's exact-type verification. Either way the fallback is today's per-link chain.
        var predictedChains = new Dictionary<(string Request, string Response), List<List<string>>>();
        if (!behaviorRegistrations.IsDefaultOrEmpty)
        {
            foreach (var handler in registrations)
            {
                var predicted = BehaviorRegistrationScanner.PredictChains(
                    behaviorRegistrations, handler.RequestType, handler.ResponseType);

                predicted.RemoveAll(static c => c.Count == 0);

                if (predicted.Count > 0)
                    predictedChains[(handler.RequestType, handler.ResponseType)] = predicted;
            }
        }

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"global using DSoftStudio.Mediator.Generated.{sanitizedAsm};");
        sb.AppendLine();
        sb.AppendLine("namespace DSoftStudio.Mediator");
        sb.AppendLine("{");

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Auto-generated mediator pipeline registry.");
        sb.AppendLine("    /// Inspects the service collection at startup to determine the optimal dispatch");
        sb.AppendLine("    /// strategy (direct handler vs full pipeline) for each request type.");
        sb.AppendLine("    /// </summary>");

        sb.AppendLine("    file static class MediatorRegistry");
        sb.AppendLine("    {");
        sb.AppendLine("        private sealed class __PipelineSentinel { }");
        sb.AppendLine();

        // Whether the chains were ALREADY frozen. AddMediator(configure) asks BEFORE running the
        // configure lambda, because that lambda registers unconditionally while RegisterPipelineChains
        // below returns early -- so on a second call the components it adds get no chain built for them.
        sb.AppendLine("        public static bool PipelinesAlreadyRegistered(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var d in services)");
        sb.AppendLine("                if (d.ServiceType == typeof(__PipelineSentinel))");
        sb.AppendLine("                    return true;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        public static void RegisterPipelineChains(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var d in services)");
        sb.AppendLine("                if (d.ServiceType == typeof(__PipelineSentinel))");
        sb.AppendLine("                    return;");
        sb.AppendLine("            global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<__PipelineSentinel>(services);");
        sb.AppendLine();

        // Filter behaviors relevant to the request pipeline (not stream)
        var requestBehaviors = new List<BehaviorTypeInfo>();
        foreach (var b in behaviors)
        {
            if (b.Kind != PipelineInterfaceKind.StreamBehavior)
                requestBehaviors.Add(b);
        }

        // AOT-safe: emit open-generic closure calls before RegisterPipeline
        if (requestBehaviors.Count > 0 && registrations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("            // AOT-safe: close open-generic pipeline behavior registrations in a single O(S) pass.");
            sb.AppendLine("            // Replaces each open-generic ServiceDescriptor IN PLACE with its per-handler-pair");
            sb.AppendLine("            // closed-generic descriptors, so DI never calls MakeGenericType (which fails for");
            sb.AppendLine("            // value-type TResponse under Native AOT when RuntimeFeature.IsDynamicCodeSupported");
            sb.AppendLine("            // is false) AND the author's registration order — hence outer-to-inner pipeline");
            sb.AppendLine("            // order — is preserved for pipelines that mix open and closed behaviors.");
            sb.AppendLine("            CloseAllOpenGenericBehaviors(services);");
            sb.AppendLine();
        }

        foreach (var handler in registrations)
        {
            sb.AppendLine(
                $"            RegisterPipeline<{handler.RequestType}, {handler.ResponseType}>(services);");

            if (predictedChains.TryGetValue((handler.RequestType, handler.ResponseType), out var candidates))
            {
                var suffix = HandlerDiscovery.SanitizeIdentifier(handler.RequestType)
                             + "_" + HandlerDiscovery.SanitizeIdentifier(handler.ResponseType);

                // One candidate per composition root. The registry tries them in order and keeps the
                // first whose exact-type verification passes, so a file that builds several containers
                // for the same pair gets the right chain in each.
                for (int c = 0; c < candidates.Count; c++)
                {
                    sb.AppendLine(
                        $"            global::DSoftStudio.Mediator.BehaviorChainRegistry<{handler.RequestType}, {handler.ResponseType}>"
                        + $".Register(__ChainFactory{c}_{suffix}.Build);");
                }
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // Emit AOT-safe open-generic closure methods when behaviors are discovered
        if (requestBehaviors.Count > 0 && registrations.Count > 0)
        {
            EmitCloseAllOpenGenericBehaviorsMethod(sb, requestBehaviors, registrations);
            sb.AppendLine();
        }

        // Generic helper that inspects service collection and sets optimal dispatch
        sb.AppendLine("        private static void RegisterPipeline<TRequest, TResponse>(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("            where TRequest : global::DSoftStudio.Mediator.Abstractions.IRequest<TResponse>");
        sb.AppendLine("        {");
        sb.AppendLine("            bool needsChain = false;");
        sb.AppendLine("            bool allSingleton = true;");
        sb.AppendLine("            global::Microsoft.Extensions.DependencyInjection.ServiceLifetime? handlerLifetime = null;");
        sb.AppendLine("            bool hasTransientChainDependency = false;");
        sb.AppendLine("            bool hasDispatchObserver = false;");
        sb.AppendLine("            foreach (var descriptor in services)");
        sb.AppendLine("            {");
        sb.AppendLine("                var st = descriptor.ServiceType;");
        sb.AppendLine("                if (st == typeof(global::DSoftStudio.Mediator.Abstractions.IPipelineBehavior<TRequest, TResponse>) ||");
        sb.AppendLine("                    st == typeof(global::DSoftStudio.Mediator.Abstractions.IRequestPreProcessor<TRequest>) ||");
        sb.AppendLine("                    st == typeof(global::DSoftStudio.Mediator.Abstractions.IRequestPostProcessor<TRequest, TResponse>) ||");
        sb.AppendLine("                    st == typeof(global::DSoftStudio.Mediator.Abstractions.IRequestExceptionHandler<TRequest, TResponse>) ||");
        sb.AppendLine("                    (st.IsGenericTypeDefinition && (");
        sb.AppendLine("                        st == typeof(global::DSoftStudio.Mediator.Abstractions.IPipelineBehavior<,>) ||");
        sb.AppendLine("                        st == typeof(global::DSoftStudio.Mediator.Abstractions.IRequestPreProcessor<>) ||");
        sb.AppendLine("                        st == typeof(global::DSoftStudio.Mediator.Abstractions.IRequestPostProcessor<,>) ||");
        sb.AppendLine("                        st == typeof(global::DSoftStudio.Mediator.Abstractions.IRequestExceptionHandler<,>))))");
        sb.AppendLine("                {");
        sb.AppendLine("                    needsChain = true;");
        sb.AppendLine("                    if (descriptor.Lifetime != global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)");
        sb.AppendLine("                        allSingleton = false;");
        sb.AppendLine("                    if (descriptor.Lifetime == global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient)");
        sb.AppendLine("                        hasTransientChainDependency = true;");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (st == typeof(global::DSoftStudio.Mediator.Abstractions.IMediatorDispatchObserver))");
        sb.AppendLine("                {");
        sb.AppendLine("                    hasDispatchObserver = true;");
        sb.AppendLine();
        sb.AppendLine("                    // The chain's ctor consumes IEnumerable<IMediatorDispatchObserver>, so an observer is a");
        sb.AppendLine("                    // chain DEPENDENCY and constrains it exactly as the handler below does. Without this a");
        sb.AppendLine("                    // Scoped observer alongside all-singleton components yields a singleton chain that");
        sb.AppendLine("                    // captures the observer and its scoped graph -- ValidateScopes throws on the first");
        sb.AppendLine("                    // dispatch, and with validation off it silently shares them for the process lifetime.");
        sb.AppendLine("                    if (descriptor.Lifetime != global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)");
        sb.AppendLine("                        allSingleton = false;");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (st == typeof(global::DSoftStudio.Mediator.Abstractions.IRequestHandler<TRequest, TResponse>))");
        sb.AppendLine("                {");
        sb.AppendLine("                    // The chain's ctor consumes the handler, so the handler's lifetime constrains the");
        sb.AppendLine("                    // chain. RECORDED here rather than folded, because this service type resolves SINGLE:");
        sb.AppendLine("                    // the container hands the chain whatever the LAST descriptor names, so that is the only");
        sb.AppendLine("                    // one whose lifetime is real. Folding every descriptor instead let the generator's own");
        sb.AppendLine("                    // Transient registration -- which HandlerLifetimeOptimizer deliberately leaves in place");
        sb.AppendLine("                    // once a user appends an override -- decide the chain for a handler the container never");
        sb.AppendLine("                    // builds, so the documented services.AddScoped<IRequestHandler<..>,..>() override");
        sb.AppendLine("                    // silently produced a Transient, uncached chain.");
        sb.AppendLine("                    // The components above keep the OR on purpose: IPipelineBehavior and friends are");
        sb.AppendLine("                    // ENUMERABLE, so every descriptor for them is live and a Transient one really does run");
        sb.AppendLine("                    // per dispatch.");
        sb.AppendLine("                    // It does NOT set needsChain: a handler on its own never needs a chain.");
        sb.AppendLine("                    handlerLifetime = descriptor.Lifetime;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // The handler, folded once the winning descriptor is known. A TRANSIENT handler takes the");
        sb.AppendLine("            // chain ALL the way down, not merely off Singleton: HandlerLifetimeOptimizer leaves a handler");
        sb.AppendLine("            // Transient exactly when a dependency of its own is transient or unregistered -- when a fresh");
        sb.AppendLine("            // instance per resolve is the whole point -- and a Scoped chain is CACHEABLE, so it would");
        sb.AppendLine("            // construct that handler once for the scope and hand every dispatch the same instance,");
        sb.AppendLine("            // sharing the very dependency the optimizer had just refused to share.");
        sb.AppendLine("            if (handlerLifetime is not null)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (handlerLifetime != global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)");
        sb.AppendLine("                    allSingleton = false;");
        sb.AppendLine("                if (handlerLifetime == global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient)");
        sb.AppendLine("                    hasTransientChainDependency = true;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // A dispatch observer wraps EVERY request at the dispatch boundary (it lives inside the");
        sb.AppendLine("            // PipelineChainHandler), even handler-only requests with no behaviors/processors. Force a");
        sb.AppendLine("            // chain so such requests are still observed. The lifetime is NOT pinned here: the loop above");
        sb.AppendLine("            // already folded the handler's (and every component's) lifetime into allSingleton, so a");
        sb.AppendLine("            // singleton handler keeps a cached Singleton chain (no per-request resolution) while a");
        sb.AppendLine("            // scoped/transient handler yields a Scoped/Transient chain.");
        sb.AppendLine("            if (hasDispatchObserver && !needsChain)");
        sb.AppendLine("            {");
        sb.AppendLine("                needsChain = true;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (needsChain)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (allSingleton)");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::DSoftStudio.Mediator.PipelineChainHandler<TRequest, TResponse>>(services);");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (hasTransientChainDependency)");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddTransient<global::DSoftStudio.Mediator.PipelineChainHandler<TRequest, TResponse>>(services);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<global::DSoftStudio.Mediator.PipelineChainHandler<TRequest, TResponse>>(services);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                // Mark the static dispatch table so the interceptor can branch without a delegate.");
        sb.AppendLine("                global::DSoftStudio.Mediator.RequestDispatch<TRequest, TResponse>.MarkPipelineChainRegistered();");
        sb.AppendLine();
        sb.AppendLine("                // Scoped and Singleton chains are safe to cache per thread (same instance within a scope).");
        sb.AppendLine("                // Transient chains must be resolved fresh each call.");
        sb.AppendLine("                if (!hasTransientChainDependency)");
        sb.AppendLine("                    global::DSoftStudio.Mediator.RequestDispatch<TRequest, TResponse>.MarkPipelineChainCacheable();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // ADR-0065 AGGRESSIVE tier eligibility: runs AFTER HandlerLifetimeOptimizer.Apply, so the");
        sb.AppendLine("            // handler descriptor lifetime read below is FINAL. Eligible = no pipeline chain AND the");
        sb.AppendLine("            // LAST-registered IRequestHandler descriptor is a Singleton (MSDI last-wins semantics).");
        sb.AppendLine("            // The winning implementation type is re-verified at arm time (first dispatch) against the");
        sb.AppendLine("            // captured collection, so post-precompile re-registrations demote to the SAFE tier.");
        sb.AppendLine("            global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor? __lastHandler = null;");
        sb.AppendLine("            foreach (var descriptor in services)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (descriptor.ServiceType == typeof(global::DSoftStudio.Mediator.Abstractions.IRequestHandler<TRequest, TResponse>))");
        sb.AppendLine("                    __lastHandler = descriptor;");
        sb.AppendLine("            }");
        sb.AppendLine("            global::DSoftStudio.Mediator.AggressiveDispatch<TRequest, TResponse>.SetEligibility(");
        sb.AppendLine("                services,");
        sb.AppendLine("                eligible: !needsChain");
        sb.AppendLine("                    && __lastHandler is not null");
        sb.AppendLine("                    && __lastHandler.Lifetime == global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton);");
        sb.AppendLine();
        sb.AppendLine("            // AOT-safe Send(object) dispatch — register a runtime-typed delegate for this request type.");
        sb.AppendLine("            // Uses same static flags + ThreadStatic caches as the generic Send<T,R> path.");
        sb.AppendLine("            // Sync fast path avoids async state machine allocation when handler completes synchronously.");
        sb.AppendLine("            global::DSoftStudio.Mediator.RequestObjectDispatch.Register<TRequest, TResponse>(");
        sb.AppendLine("                static (request, sp, ct) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    var typed = (TRequest)request;");
        InterceptorHelpers.AppendSendObjectDispatchBody(
            sb, "TRequest", "TResponse",
            requestVar: "typed",
            providerVar: "sp",
            ctVar: "ct",
            resultVar: "result",
            indent: "                    ",
            concreteCacheClassName: null);
        sb.AppendLine("                });");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Async fallback: awaits the result and boxes it. Only allocated when the handler is truly async.</summary>");
        sb.AppendLine("        private static async global::System.Threading.Tasks.ValueTask<object?> AwaitAndBox<T>(");
        sb.AppendLine("            global::System.Threading.Tasks.ValueTask<T> task) => await task;");

        sb.AppendLine("    }");

        // Specialized chains live in this same namespace and file: they are `file`-local, and
        // RegisterPipelineChains above references their factories by simple name.
        foreach (var entry in predictedChains)
        {
            for (int c = 0; c < entry.Value.Count; c++)
                EmitSpecializedChain(sb, entry.Key.Request, entry.Key.Response, entry.Value[c], c);
        }

        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine($"namespace DSoftStudio.Mediator.Generated.{sanitizedAsm}");
        sb.AppendLine("{");
        sb.AppendLine();

        sb.AppendLine("    internal static class MediatorRegistryExtensions");
        sb.AppendLine("    {");

        sb.AppendLine(
            "        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection PrecompilePipelines(");

        sb.AppendLine(
            "            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");

        sb.AppendLine("        {");

        sb.AppendLine("            global::DSoftStudio.Mediator.HandlerLifetimeOptimizer.Apply(services);");
        sb.AppendLine("            MediatorRegistry.RegisterPipelineChains(services);");
        sb.AppendLine("            global::DSoftStudio.Mediator.RequestObjectDispatch.Freeze();");
        sb.AppendLine("            return services;");

        sb.AppendLine("        }");

        sb.AppendLine();

        // ── AddMediator(Action<MediatorBuilder>) — single entry point (Option B: automatic) ──
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all mediator services, discovered handlers, and precompiled pipelines,");
        sb.AppendLine("        /// notifications and streams in a single call. The <paramref name=\"configure\"/> lambda");
        sb.AppendLine("        /// allows registering open-generic behaviors, custom notification publishers, and more.");
        sb.AppendLine("        /// </summary>");

        sb.AppendLine(
            "        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediator(");
        sb.AppendLine(
            "            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services,");
        sb.AppendLine(
            "            global::System.Action<global::DSoftStudio.Mediator.MediatorBuilder> configure)");
        sb.AppendLine("        {");

        // 1. Core services (IMediator, ISender, IPublisher) — hand-written in ServiceCollectionExtensions.
        sb.AppendLine("            global::DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services);");

        // 2. Generated handler registrations — public extension from DependencyInjectionGenerator.
        sb.AppendLine("            services.RegisterMediatorHandlers();");

        // 3. User customization (open behaviors, parallel publisher, etc.)
        //
        // The sentinel is read BEFORE the lambda runs, and the collection's count with it. This step is
        // unguarded by design -- configure() must always be honoured -- while RegisterPipelineChains
        // below returns early once the sentinel exists. A SECOND AddMediator(configure) therefore
        // registers components that no chain will be rebuilt around: they either never run, or a
        // Singleton chain from the first scan captures them and the container refuses to build under
        // ValidateScopes. Recording it here is observation, not inference: the sentinel proves the
        // chains were frozen, and only what this lambda appended is examined, so a second bare
        // PrecompilePipelines() (which registers nothing) and a lambda that adds no component are both
        // silent. ValidateMediatorHandlers() reports whatever is recorded.
        sb.AppendLine("            bool __alreadyScanned = MediatorRegistry.PipelinesAlreadyRegistered(services);");
        sb.AppendLine("            int __beforeConfigure = services.Count;");
        sb.AppendLine("            var builder = new global::DSoftStudio.Mediator.MediatorBuilder(services);");
        sb.AppendLine("            configure(builder);");
        sb.AppendLine("            if (__alreadyScanned)");
        sb.AppendLine("                global::DSoftStudio.Mediator.LateComponentRegistry.Record(services, __beforeConfigure);");

        // 4. Precompile pipelines (closes open generics, registers chains, freezes dispatch).
        sb.AppendLine("            global::DSoftStudio.Mediator.HandlerLifetimeOptimizer.Apply(services);");
        sb.AppendLine("            MediatorRegistry.RegisterPipelineChains(services);");
        sb.AppendLine("            global::DSoftStudio.Mediator.RequestObjectDispatch.Freeze();");

        // 5. Precompile notifications and streams.
        //
        // Without these, this overload silently breaks Publish<T>: NotificationDispatch<T>.Handlers
        // stays null and NotificationCachedDispatcher.DispatchSequential returns Task.CompletedTask
        // without dispatching anything (NotificationCachedDispatcher.cs:31-32). Publish(object)
        // throws instead, so the two Publish overloads disagreed. CreateStream<,> was equally
        // affected. The docs advertise this overload as doing "everything" and tell users NOT to
        // mix it with the individual Precompile* calls, so the single-call path must be complete.
        //
        // Safe to emit unconditionally: NotificationRegistryExtensions/StreamRegistryExtensions are
        // emitted by their generators for EVERY compilation (even with zero notification/stream
        // types — GenerateCode runs with an empty plan list) and land in this same
        // DSoftStudio.Mediator.Generated.<assembly> namespace, so no using directive is needed.
        // Both registries are write-once (Interlocked.CompareExchange in NotificationDispatch.cs:35
        // and StreamDispatch.cs:53/61), so a user who also calls them explicitly is a no-op.
        sb.AppendLine("            services.PrecompileNotifications();");
        sb.AppendLine("            services.PrecompileStreams();");
        sb.AppendLine("            return services;");

        sb.AppendLine("        }");

        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine("} // namespace");

        return sb.ToString();
    }

    /// <summary>
    /// Emits the <c>CloseAllOpenGenericBehaviors</c> method into the generated source.
    /// Does a single O(S) forward pass over the service collection. For each matched
    /// open-generic behavior descriptor, SPLICES the closed-generic versions in at that
    /// descriptor's own position — no generic method instantiation, no per-handler scanning.
    /// <para>
    /// Splice, not append. The previous version snapshotted <c>services.Count</c> and
    /// <c>Add</c>ed the closed descriptors at the END of the collection, so a behavior
    /// registered as an open generic always ended up INNERMOST in the pipeline regardless of
    /// where the author registered it — contradicting the documented contract
    /// (docs/mediator/features/pipeline-behaviors.md: "The first registered behavior is the
    /// outermost wrapper"). The chain is built from the DI resolution order
    /// (PipelineChainHandler pre-links <c>_behaviors[0]</c> outermost), so descriptor position
    /// IS execution order. Replacing the open descriptor in place preserves what the author wrote.
    /// </para>
    /// <para>
    /// Removing in place also subsumes the old <c>RemoveOpenGenericBehaviorDescriptors</c> pass,
    /// which matched on exactly the same guard and ran immediately afterwards — one O(S) pass at
    /// startup instead of two.
    /// </para>
    /// </summary>
    private static void EmitCloseAllOpenGenericBehaviorsMethod(
        StringBuilder sb,
        List<BehaviorTypeInfo> behaviors,
        List<HandlerInfo> registrations)
    {
        sb.AppendLine("        private static void CloseAllOpenGenericBehaviors(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine("            // services.Count is read live: the loop mutates the collection in place.");
        sb.AppendLine("            for (int i = 0; i < services.Count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                var d = services[i];");
        sb.AppendLine("                if (d.ImplementationType is null || !d.ServiceType.IsGenericTypeDefinition)");
        sb.AppendLine("                    continue;");

        foreach (var b in behaviors)
        {
            var serviceOpen = GetOpenServiceTypeName(b.Kind);

            sb.AppendLine();
            sb.AppendLine($"                if (d.ServiceType == typeof({serviceOpen}) && d.ImplementationType == typeof({b.OpenTypeName}))");
            sb.AppendLine("                {");
            sb.AppendLine("                    services.RemoveAt(i);");

            var slot = 0;
            foreach (var handler in registrations)
            {
                var serviceClosed = GetClosedServiceType(b.Kind, handler.RequestType, handler.ResponseType);

                sb.AppendLine($"                    services.Insert(i + {slot}, new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(");
                sb.AppendLine($"                        typeof({serviceClosed}),");
                sb.AppendLine($"                        typeof({b.BaseTypeName}<{handler.RequestType}, {handler.ResponseType}>),");
                sb.AppendLine("                        d.Lifetime));");
                slot++;
            }

            // Advance past the spliced block. With the loop's own i++ this lands on the element
            // after it. When nothing was inserted (no handler pairs) the net effect is to re-examine
            // position i, which now holds whatever shifted down into it.
            var delta = registrations.Count - 1;
            if (delta != 0)
                sb.AppendLine($"                    i += {delta};");

            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Emits the fully specialized chain for one (request, response) pair: one <c>file</c>-local link
    /// type per POSITION, each storing its behavior AND the next link in concrete-typed fields, plus a
    /// factory that verifies the prediction against the resolved instances before building.
    /// <para>
    /// Both fields concrete is the whole point. A link can only devirtualize the <c>next.Handle</c> call
    /// inside the user's behavior body if the JIT sees the concrete type of <c>next</c> at the inlined
    /// call site. Measured per link on .NET 11 preview 7 with three distinct pass-through behaviors:
    /// interface+interface 3.08 ns, concrete+interface 1.24 ns, concrete+concrete 0.10 ns.
    /// </para>
    /// <para>
    /// The factory is fail-open by construction: length check, then an EXACT type check per position.
    /// The chain is PREDICTED from registration syntax, which cannot see conditional registration,
    /// factory lambdas, or registrations inside a referenced assembly's method body — so a wrong
    /// prediction must be detected and discarded, never trusted. On any mismatch it returns null and
    /// PipelineChainHandler falls back to per-link construction. Exact, never <c>is</c>: a decorator or
    /// a subclass hiding <c>Handle</c> with <c>new</c> must not be bound to the base implementation.
    /// </para>
    /// </summary>
    private static void EmitSpecializedChain(
        StringBuilder sb,
        string requestType,
        string responseType,
        List<string> chain,
        int candidateIndex)
    {
        var suffix = candidateIndex + "_" + HandlerDiscovery.SanitizeIdentifier(requestType)
                     + "_" + HandlerDiscovery.SanitizeIdentifier(responseType);

        var handlerIface =
            $"global::DSoftStudio.Mediator.Abstractions.IRequestHandler<{requestType}, {responseType}>";

        // Innermost first, so each link can name the concrete type of the one it wraps.
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var linkName = $"__ChainLink{i}_{suffix}";
            var nextType = i == chain.Count - 1 ? handlerIface : $"__ChainLink{i + 1}_{suffix}";

            sb.AppendLine();
            sb.AppendLine($"    file sealed class {linkName} : {handlerIface}, global::DSoftStudio.Mediator.Abstractions.IPipelineHandlerTypeAccessor");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {chain[i]} _b;");
            sb.AppendLine($"        private readonly {nextType} _next;");
            sb.AppendLine();
            sb.AppendLine($"        internal {linkName}({chain[i]} b, {nextType} next)");
            sb.AppendLine("        {");
            sb.AppendLine("            _b = b;");
            sb.AppendLine("            _next = next;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public global::System.Threading.Tasks.ValueTask<{responseType}> Handle({requestType} request, global::System.Threading.CancellationToken cancellationToken)");
            sb.AppendLine("            => _b.Handle(request, _next, cancellationToken);");
            sb.AppendLine();
            sb.AppendLine("        public global::System.Type HandlerType");
            sb.AppendLine("            => _next is global::DSoftStudio.Mediator.Abstractions.IPipelineHandlerTypeAccessor inner");
            sb.AppendLine("                ? inner.HandlerType");
            sb.AppendLine("                : _next.GetType();");
            sb.AppendLine("    }");
        }

        // Verifying factory.
        sb.AppendLine();
        sb.AppendLine($"    file static class __ChainFactory{suffix}");
        sb.AppendLine("    {");
        sb.AppendLine($"        internal static {handlerIface}? Build(");
        sb.AppendLine($"            global::DSoftStudio.Mediator.Abstractions.IPipelineBehavior<{requestType}, {responseType}>[] behaviors,");
        sb.AppendLine($"            {handlerIface} handler)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (behaviors.Length != {chain.Count})");
        sb.AppendLine("                return null;");
        sb.AppendLine();

        for (int i = 0; i < chain.Count; i++)
        {
            sb.AppendLine($"            if (behaviors[{i}].GetType() != typeof({chain[i]}))");
            sb.AppendLine("                return null;");
        }

        sb.AppendLine();
        sb.Append("            return ");

        for (int i = 0; i < chain.Count; i++)
            sb.Append($"new __ChainLink{i}_{suffix}(({chain[i]})behaviors[{i}], ");

        sb.Append("handler");
        sb.Append(new string(')', chain.Count));
        sb.AppendLine(";");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static string GetOpenServiceTypeName(PipelineInterfaceKind kind) => kind switch
    {
        PipelineInterfaceKind.Behavior => "global::DSoftStudio.Mediator.Abstractions.IPipelineBehavior<,>",
        PipelineInterfaceKind.PostProcessor => "global::DSoftStudio.Mediator.Abstractions.IRequestPostProcessor<,>",
        PipelineInterfaceKind.ExceptionHandler => "global::DSoftStudio.Mediator.Abstractions.IRequestExceptionHandler<,>",
        _ => ""
    };

    private static string GetClosedServiceType(PipelineInterfaceKind kind, string requestType, string responseType) => kind switch
    {
        PipelineInterfaceKind.Behavior => $"global::DSoftStudio.Mediator.Abstractions.IPipelineBehavior<{requestType}, {responseType}>",
        PipelineInterfaceKind.PostProcessor => $"global::DSoftStudio.Mediator.Abstractions.IRequestPostProcessor<{requestType}, {responseType}>",
        PipelineInterfaceKind.ExceptionHandler => $"global::DSoftStudio.Mediator.Abstractions.IRequestExceptionHandler<{requestType}, {responseType}>",
        _ => ""
    };

    /// <summary>
    /// Represents a handler registration pair.
    /// </summary>
    internal readonly struct HandlerInfo(string requestType, string responseType) : System.IEquatable<HandlerInfo>
    {
        public string RequestType { get; } = requestType;
        public string ResponseType { get; } = responseType;

        public bool Equals(HandlerInfo other) =>
            RequestType == other.RequestType &&
            ResponseType == other.ResponseType;

        public override bool Equals(object obj) =>
            obj is HandlerInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (RequestType.GetHashCode() * 397) ^ ResponseType.GetHashCode();
            }
        }
    }
}
