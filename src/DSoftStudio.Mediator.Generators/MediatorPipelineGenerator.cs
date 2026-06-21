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
                    .OrderBy(static h => h.RequestType)
                    .ThenBy(static h => h.ResponseType)
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
                    .OrderBy(static b => b.BaseTypeName)
                    .ToArray();
                return new EquatableArray<BehaviorTypeInfo>(array);
            });

        var assemblyName = context.CompilationProvider
            .Select(static (c, _) => c.AssemblyName ?? "Assembly");

        var combined = localCollected
            .Combine(hasHandlerInterface)
            .Combine(externalHandlers)
            .Combine(selfCollected)
            .Combine(allBehaviors)
            .Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var (((((localHandlers, interfaceExists), external), selfHandlers), behaviors), asmName) = pair;

            var hasSelfHandlers = !selfHandlers.IsDefaultOrEmpty && selfHandlers.Length > 0;

            if (!interfaceExists && external.Length == 0 && !hasSelfHandlers)
            {
                spc.AddSource(
                    "MediatorRegistry.g.cs",
                    SourceText.From(
                        GenerateRegistryCode([], asmName, behaviors),
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
                .OrderBy(static h => h.RequestType)
                .ThenBy(static h => h.ResponseType)
                .ToList();

            var code = GenerateRegistryCode(uniqueRegistrations, asmName, behaviors);

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

        if (HandlerDiscovery.IsFileLocal(typeDecl))
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

        if (HandlerDiscovery.IsFileLocal(classDeclaration))
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
    /// Walks the current compilation's namespace tree to discover local open-generic
    /// pipeline behavior types (classes that implement <c>IPipelineBehavior&lt;,&gt;</c>,
    /// <c>IRequestPostProcessor&lt;,&gt;</c>, <c>IRequestExceptionHandler&lt;,&gt;</c>,
    /// or <c>IStreamPipelineBehavior&lt;,&gt;</c>).
    /// </summary>
    private static void CollectLocalBehaviors(
        INamespaceSymbol ns,
        List<BehaviorTypeInfo> results)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (type.TypeKind == TypeKind.Class
                && !type.IsAbstract
                && type.IsGenericType
                && (type.DeclaredAccessibility == Accessibility.Public
                    || type.DeclaredAccessibility == Accessibility.Internal))
            {
                ReferencedAssemblyScanner.TryAddBehaviorInfoFrom(type, results);
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
            CollectLocalBehaviors(child, results);
    }

    private static string GenerateRegistryCode(
        List<HandlerInfo> registrations,
        string assemblyName,
        EquatableArray<BehaviorTypeInfo> behaviors)
    {
        var sanitizedAsm = HandlerDiscovery.SanitizeIdentifier(assemblyName);
        var sb = new StringBuilder();

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
            sb.AppendLine("            // Replaces open-generic ServiceDescriptors with per-handler-pair closed-generic");
            sb.AppendLine("            // descriptors so DI never calls MakeGenericType (which fails for value-type");
            sb.AppendLine("            // TResponse under Native AOT when RuntimeFeature.IsDynamicCodeSupported is false).");
            sb.AppendLine("            CloseAllOpenGenericBehaviors(services);");
            sb.AppendLine("            RemoveOpenGenericBehaviorDescriptors(services);");
            sb.AppendLine();
        }

        foreach (var handler in registrations)
        {
            sb.AppendLine(
                $"            RegisterPipeline<{handler.RequestType}, {handler.ResponseType}>(services);");
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // Emit AOT-safe open-generic closure methods when behaviors are discovered
        if (requestBehaviors.Count > 0 && registrations.Count > 0)
        {
            EmitCloseAllOpenGenericBehaviorsMethod(sb, requestBehaviors, registrations);
            sb.AppendLine();
            EmitRemoveOpenGenericBehaviorDescriptorsMethod(sb, requestBehaviors);
            sb.AppendLine();
        }

        // Generic helper that inspects service collection and sets optimal dispatch
        sb.AppendLine("        private static void RegisterPipeline<TRequest, TResponse>(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("            where TRequest : global::DSoftStudio.Mediator.Abstractions.IRequest<TResponse>");
        sb.AppendLine("        {");
        sb.AppendLine("            bool needsChain = false;");
        sb.AppendLine("            bool allSingleton = true;");
        sb.AppendLine("            bool hasTransientPipelineComponent = false;");
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
        sb.AppendLine("                        hasTransientPipelineComponent = true;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (needsChain)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (allSingleton)");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::DSoftStudio.Mediator.PipelineChainHandler<TRequest, TResponse>>(services);");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (hasTransientPipelineComponent)");
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
        sb.AppendLine("                if (!hasTransientPipelineComponent)");
        sb.AppendLine("                    global::DSoftStudio.Mediator.RequestDispatch<TRequest, TResponse>.MarkPipelineChainCacheable();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // AOT-safe Send(object) dispatch — register a runtime-typed delegate for this request type.");
        sb.AppendLine("            // Uses same static flags + ThreadStatic caches as the generic Send<T,R> path.");
        sb.AppendLine("            // Sync fast path avoids async state machine allocation when handler completes synchronously.");
        sb.AppendLine("            global::DSoftStudio.Mediator.RequestObjectDispatch.Register<TRequest, TResponse>(");
        sb.AppendLine("                static (request, sp, ct) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    var typed = (TRequest)request;");
        sb.AppendLine("                    global::System.Threading.Tasks.ValueTask<TResponse> result;");
        sb.AppendLine();
        sb.AppendLine("                    if (global::DSoftStudio.Mediator.RequestDispatch<TRequest, TResponse>.HasPipelineChain)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var chain = global::DSoftStudio.Mediator.RequestDispatch<TRequest, TResponse>.IsPipelineChainCacheable");
        sb.AppendLine("                            ? global::DSoftStudio.Mediator.PipelineChainCache<TRequest, TResponse>.Resolve(sp)");
        sb.AppendLine("                            : global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
        sb.AppendLine("                                .GetService<global::DSoftStudio.Mediator.PipelineChainHandler<TRequest, TResponse>>(sp);");
        sb.AppendLine("                        if (chain is not null)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            result = chain.Handle(typed, ct);");
        sb.AppendLine("                            return result.IsCompletedSuccessfully");
        sb.AppendLine("                                ? new global::System.Threading.Tasks.ValueTask<object?>(result.Result)");
        sb.AppendLine("                                : AwaitAndBox(result);");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine();
        sb.AppendLine("                    result = global::DSoftStudio.Mediator.HandlerCache<TRequest, TResponse>");
        sb.AppendLine("                        .Resolve(sp).Handle(typed, ct);");
        sb.AppendLine("                    return result.IsCompletedSuccessfully");
        sb.AppendLine("                        ? new global::System.Threading.Tasks.ValueTask<object?>(result.Result)");
        sb.AppendLine("                        : AwaitAndBox(result);");
        sb.AppendLine("                });");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Async fallback: awaits the result and boxes it. Only allocated when the handler is truly async.</summary>");
        sb.AppendLine("        private static async global::System.Threading.Tasks.ValueTask<object?> AwaitAndBox<T>(");
        sb.AppendLine("            global::System.Threading.Tasks.ValueTask<T> task) => await task;");

        sb.AppendLine("    }");

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

        sb.AppendLine("            MediatorRegistry.RegisterPipelineChains(services);");
        sb.AppendLine("            global::DSoftStudio.Mediator.RequestObjectDispatch.Freeze();");
        sb.AppendLine("            return services;");

        sb.AppendLine("        }");

        sb.AppendLine();

        // ── AddMediator(Action<MediatorBuilder>) — single entry point (Option B: automatic) ──
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all mediator services, discovered handlers, and precompiled pipelines");
        sb.AppendLine("        /// in a single call. The optional <paramref name=\"configure\"/> lambda allows");
        sb.AppendLine("        /// registering open-generic behaviors, custom notification publishers, and more.");
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
        sb.AppendLine("            var builder = new global::DSoftStudio.Mediator.MediatorBuilder(services);");
        sb.AppendLine("            configure(builder);");

        // 4. Precompile pipelines (closes open generics, registers chains, freezes dispatch).
        sb.AppendLine("            MediatorRegistry.RegisterPipelineChains(services);");
        sb.AppendLine("            global::DSoftStudio.Mediator.RequestObjectDispatch.Freeze();");
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
    /// open-generic behavior descriptor, emits closed-generic versions for ALL known
    /// handler pairs inline — no generic method instantiation, no per-handler scanning.
    /// </summary>
    private static void EmitCloseAllOpenGenericBehaviorsMethod(
        StringBuilder sb,
        List<BehaviorTypeInfo> behaviors,
        List<HandlerInfo> registrations)
    {
        sb.AppendLine("        private static void CloseAllOpenGenericBehaviors(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine("            var count = services.Count;");
        sb.AppendLine("            for (int i = 0; i < count; i++)");
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

            foreach (var handler in registrations)
            {
                var serviceClosed = GetClosedServiceType(b.Kind, handler.RequestType, handler.ResponseType);

                sb.AppendLine("                    services.Add(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(");
                sb.AppendLine($"                        typeof({serviceClosed}),");
                sb.AppendLine($"                        typeof({b.BaseTypeName}<{handler.RequestType}, {handler.ResponseType}>),");
                sb.AppendLine("                        d.Lifetime));");
            }

            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Emits the <c>RemoveOpenGenericBehaviorDescriptors</c> method into the generated source.
    /// After all handler pairs have had their closed-generic descriptors added, this method
    /// removes the original open-generic descriptors so the DI container never attempts
    /// <c>MakeGenericType</c>.
    /// </summary>
    private static void EmitRemoveOpenGenericBehaviorDescriptorsMethod(
        StringBuilder sb,
        List<BehaviorTypeInfo> behaviors)
    {
        sb.AppendLine("        private static void RemoveOpenGenericBehaviorDescriptors(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine("            for (int i = services.Count - 1; i >= 0; i--)");
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
            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
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
