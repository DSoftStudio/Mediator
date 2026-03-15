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

        var combined = localCollected
            .Combine(hasHandlerInterface)
            .Combine(externalHandlers)
            .Combine(selfCollected);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var (((localHandlers, interfaceExists), external), selfHandlers) = pair;

            var hasSelfHandlers = !selfHandlers.IsDefaultOrEmpty && selfHandlers.Length > 0;

            if (!interfaceExists && external.Length == 0 && !hasSelfHandlers)
            {
                spc.AddSource(
                    "MediatorRegistry.g.cs",
                    SourceText.From(
                        GenerateRegistryCode([]),
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

            var code = GenerateRegistryCode(uniqueRegistrations);

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

    private static string GenerateRegistryCode(List<HandlerInfo> registrations)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace DSoftStudio.Mediator");
        sb.AppendLine("{");

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Auto-generated mediator pipeline registry.");
        sb.AppendLine("    /// Inspects the service collection at startup to determine the optimal dispatch");
        sb.AppendLine("    /// strategy (direct handler vs full pipeline) for each request type.");
        sb.AppendLine("    /// </summary>");

        sb.AppendLine("    internal static class MediatorRegistry");
        sb.AppendLine("    {");

        sb.AppendLine("        public static void RegisterPipelineChains(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");

        foreach (var handler in registrations)
        {
            sb.AppendLine(
                $"            RegisterPipeline<{handler.RequestType}, {handler.ResponseType}>(services);");
        }

        sb.AppendLine("        }");
        sb.AppendLine();

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
        sb.AppendLine();
        sb.AppendLine("                // Pipeline with behaviors: resolve PipelineChainHandler directly — single DI lookup.");
        sb.AppendLine("                global::DSoftStudio.Mediator.RequestDispatch<TRequest, TResponse>.TryInitialize(");
        sb.AppendLine("                    static (request, sp, ct) =>");
        sb.AppendLine("                        global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
        sb.AppendLine("                            .GetRequiredService<global::DSoftStudio.Mediator.PipelineChainHandler<TRequest, TResponse>>(sp)");
        sb.AppendLine("                            .Handle(request, ct));");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                // No pipeline features — resolve handler directly. Single DI lookup, zero overhead.");
        sb.AppendLine("                global::DSoftStudio.Mediator.RequestDispatch<TRequest, TResponse>.TryInitialize(");
        sb.AppendLine("                    static (request, sp, ct) =>");
        sb.AppendLine("                        global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
        sb.AppendLine("                            .GetRequiredService<global::DSoftStudio.Mediator.Abstractions.IRequestHandler<TRequest, TResponse>>(sp)");
        sb.AppendLine("                            .Handle(request, ct));");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // AOT-safe Send(object) dispatch — register a runtime-typed delegate for this request type.");
        sb.AppendLine("            // Resolves directly from the passed-in IServiceProvider instead of the static Pipeline");
        sb.AppendLine("            // delegate, because Pipeline is write-once and may have been initialized by a different");
        sb.AppendLine("            // DI configuration (e.g. parallel test classes with distinct service providers).");
        sb.AppendLine("            global::DSoftStudio.Mediator.RequestObjectDispatch.Register<TRequest, TResponse>(");
        sb.AppendLine("                static async (request, sp, ct) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    var typed = (TRequest)request;");
        sb.AppendLine("                    var chain = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
        sb.AppendLine("                        .GetService<global::DSoftStudio.Mediator.PipelineChainHandler<TRequest, TResponse>>(sp);");
        sb.AppendLine("                    if (chain is not null)");
        sb.AppendLine("                        return await chain.Handle(typed, ct);");
        sb.AppendLine("                    return await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
        sb.AppendLine("                        .GetRequiredService<global::DSoftStudio.Mediator.Abstractions.IRequestHandler<TRequest, TResponse>>(sp)");
        sb.AppendLine("                        .Handle(typed, ct);");
        sb.AppendLine("                });");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
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

        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

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
