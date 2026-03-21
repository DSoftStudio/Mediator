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

[Generator]
public sealed class StreamGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var handlerInfos = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => GetHandlerInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var localCollected = handlerInfos.Collect();

        // Scan referenced assemblies for IStreamRequestHandler registrations
        var externalHandlers = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var external = ReferencedAssemblyScanner.GetExternalStreamHandlers(compilation);
                var array = external
                    .Select(e => new StreamHandlerInfo(e.RequestType, e.ResponseType, e.HandlerType))
                    .OrderBy(static h => h.RequestType)
                    .ThenBy(static h => h.ResponseType)
                    .ToArray();
                return new EquatableArray<StreamHandlerInfo>(array);
            });

        // Discover open-generic stream pipeline behavior types (local + external)
        // for AOT-safe closed-generic DI registration.
        var allStreamBehaviors = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var results = ReferencedAssemblyScanner.GetExternalOpenGenericBehaviors(compilation);
                CollectLocalBehaviors(compilation.Assembly.GlobalNamespace, results);

                var array = results
                    .Where(static b => b.Kind == PipelineInterfaceKind.StreamBehavior)
                    .Distinct()
                    .OrderBy(static b => b.BaseTypeName)
                    .ToArray();
                return new EquatableArray<BehaviorTypeInfo>(array);
            });

        var assemblyName = context.CompilationProvider
            .Select(static (c, _) => c.AssemblyName ?? "Assembly");

        var combined = localCollected.Combine(externalHandlers).Combine(allStreamBehaviors).Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var (((localHandlers, external), behaviors), asmName) = pair;

            // Merge local + external, deduplicate
            var localList = localHandlers.IsDefaultOrEmpty
                ? Enumerable.Empty<StreamHandlerInfo>()
                : localHandlers.Distinct();

            var registrations = localList
                .Concat(external)
                .Distinct()
                .OrderBy(static h => h.RequestType)
                .ThenBy(static h => h.ResponseType)
                .ToList();

            var code = GenerateCode(registrations, asmName, behaviors);

            spc.AddSource(
                "StreamRegistry.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
    }

    private static StreamHandlerInfo? GetHandlerInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct)
            is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract ||
            symbol.TypeKind != TypeKind.Class ||
            symbol.TypeParameters.Length > 0)
            return null;

        if (HandlerDiscovery.IsFileLocal(classDecl))
            return null;

        if (!HandlerDiscovery.TryGetStreamHandler(
                symbol,
                ct,
                out var requestType,
                out var responseType,
                out var handlerType))
            return null;

        return new StreamHandlerInfo(requestType, responseType, handlerType);
    }

    /// <summary>
    /// Walks the current compilation's namespace tree to discover local open-generic
    /// stream pipeline behavior types.
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

    private static string GenerateCode(
        List<StreamHandlerInfo> registrations,
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

        sb.AppendLine("    file static class StreamRegistry");
        sb.AppendLine("    {");

        sb.AppendLine("        public static void Register(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");

        // Filter stream behaviors from the discovered list
        var streamBehaviors = new List<BehaviorTypeInfo>();
        foreach (var b in behaviors)
        {
            if (b.Kind == PipelineInterfaceKind.StreamBehavior)
                streamBehaviors.Add(b);
        }

        // AOT-safe: emit open-generic stream closure calls before RegisterStreamPipeline
        if (streamBehaviors.Count > 0 && registrations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("            // AOT-safe: close open-generic stream pipeline behavior registrations in a single O(S) pass.");
            sb.AppendLine("            // Replaces open-generic ServiceDescriptors with per-handler-pair closed-generic");
            sb.AppendLine("            // descriptors so DI never calls MakeGenericType (which fails for value-type");
            sb.AppendLine("            // TResponse under Native AOT when RuntimeFeature.IsDynamicCodeSupported is false).");
            sb.AppendLine("            CloseAllOpenGenericStreamBehaviors(services);");
            sb.AppendLine("            RemoveOpenGenericStreamBehaviorDescriptors(services);");
            sb.AppendLine();
        }

        foreach (var handler in registrations)
        {
            sb.AppendLine(
                $"            global::DSoftStudio.Mediator.StreamDispatch<{handler.RequestType}, {handler.ResponseType}>.TryInitializeHandler(");

            sb.AppendLine(
                $"                static sp => global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{handler.HandlerType}>(sp));");

            sb.AppendLine(
                $"            RegisterStreamPipeline<{handler.RequestType}, {handler.ResponseType}>(services);");

            sb.AppendLine();
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // Generic helper that inspects service collection and sets optimal stream dispatch
        sb.AppendLine("        private static void RegisterStreamPipeline<TRequest, TResponse>(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("            where TRequest : global::DSoftStudio.Mediator.Abstractions.IStreamRequest<TResponse>");
        sb.AppendLine("        {");
        sb.AppendLine("            bool hasBehaviors = false;");
        sb.AppendLine("            bool allSingleton = true;");
        sb.AppendLine("            bool hasTransientComponent = false;");
        sb.AppendLine("            foreach (var descriptor in services)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (descriptor.ServiceType == typeof(global::DSoftStudio.Mediator.Abstractions.IStreamPipelineBehavior<TRequest, TResponse>) ||");
        sb.AppendLine("                    (descriptor.ServiceType.IsGenericTypeDefinition && descriptor.ServiceType == typeof(global::DSoftStudio.Mediator.Abstractions.IStreamPipelineBehavior<,>)))");
        sb.AppendLine("                {");
        sb.AppendLine("                    hasBehaviors = true;");
        sb.AppendLine("                    if (descriptor.Lifetime != global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)");
        sb.AppendLine("                        allSingleton = false;");
        sb.AppendLine("                    if (descriptor.Lifetime == global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient)");
        sb.AppendLine("                        hasTransientComponent = true;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Only register StreamPipelineChainHandler when behaviors exist.");
        sb.AppendLine("            if (hasBehaviors)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (allSingleton)");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::DSoftStudio.Mediator.StreamPipelineChainHandler<TRequest, TResponse>>(services);");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (hasTransientComponent)");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddTransient<global::DSoftStudio.Mediator.StreamPipelineChainHandler<TRequest, TResponse>>(services);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<global::DSoftStudio.Mediator.StreamPipelineChainHandler<TRequest, TResponse>>(services);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                // Scoped and Singleton chains are safe to cache per thread.");
        sb.AppendLine("                if (!hasTransientComponent)");
        sb.AppendLine("                    global::DSoftStudio.Mediator.StreamDispatch<TRequest, TResponse>.MarkStreamChainCacheable();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Set the stream pipeline delegate — handles both with-behaviors and no-behaviors paths.");
        sb.AppendLine("            global::DSoftStudio.Mediator.StreamDispatch<TRequest, TResponse>.TryInitializePipeline(");
        sb.AppendLine("                static (request, sp, ct) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    // Resolve chain: uses ThreadStatic cache for Scoped/Singleton, GetService for Transient.");
        sb.AppendLine("                    // Returns null when no behaviors are registered (chain not in DI).");
        sb.AppendLine("                    var chain = global::DSoftStudio.Mediator.StreamDispatch<TRequest, TResponse>.IsStreamChainCacheable");
        sb.AppendLine("                        ? global::DSoftStudio.Mediator.StreamPipelineChainCache<TRequest, TResponse>.Resolve(sp)");
        sb.AppendLine("                        : global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
        sb.AppendLine("                            .GetService<global::DSoftStudio.Mediator.StreamPipelineChainHandler<TRequest, TResponse>>(sp);");
        sb.AppendLine("                    if (chain is not null)");
        sb.AppendLine("                        return chain.Handle(request, ct);");
        sb.AppendLine("                    // No-behaviors fast path: resolve handler directly, skip chain allocation.");
        sb.AppendLine("                    return global::DSoftStudio.Mediator.StreamDispatch<TRequest, TResponse>.Handler!(sp).Handle(request, ct);");
        sb.AppendLine("                });");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Emit AOT-safe open-generic stream closure methods when behaviors are discovered
        if (streamBehaviors.Count > 0 && registrations.Count > 0)
        {
            EmitCloseAllOpenGenericStreamBehaviorsMethod(sb, streamBehaviors, registrations);
            sb.AppendLine();
            EmitRemoveOpenGenericStreamBehaviorDescriptorsMethod(sb, streamBehaviors);
            sb.AppendLine();
        }

        sb.AppendLine("    }");

        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine($"namespace DSoftStudio.Mediator.Generated.{sanitizedAsm}");
        sb.AppendLine("{");
        sb.AppendLine();

        sb.AppendLine("    internal static class StreamRegistryExtensions");
        sb.AppendLine("    {");

        sb.AppendLine(
            "        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection PrecompileStreams(");

        sb.AppendLine(
            "            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");

        sb.AppendLine("        {");

        sb.AppendLine("            StreamRegistry.Register(services);");
        sb.AppendLine("            return services;");

        sb.AppendLine("        }");

        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine("} // namespace");

        return sb.ToString();
    }

    /// <summary>
    /// Emits the <c>CloseAllOpenGenericStreamBehaviors</c> method into the generated source.
    /// Does a single O(S) forward pass over the service collection for AOT-safe stream
    /// pipeline behavior closure.
    /// </summary>
    private static void EmitCloseAllOpenGenericStreamBehaviorsMethod(
        StringBuilder sb,
        List<BehaviorTypeInfo> behaviors,
        List<StreamHandlerInfo> registrations)
    {
        sb.AppendLine("        private static void CloseAllOpenGenericStreamBehaviors(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine("            var count = services.Count;");
        sb.AppendLine("            for (int i = 0; i < count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                var d = services[i];");
        sb.AppendLine("                if (d.ImplementationType is null || !d.ServiceType.IsGenericTypeDefinition)");
        sb.AppendLine("                    continue;");

        foreach (var b in behaviors)
        {
            sb.AppendLine();
            sb.AppendLine($"                if (d.ServiceType == typeof(global::DSoftStudio.Mediator.Abstractions.IStreamPipelineBehavior<,>) && d.ImplementationType == typeof({b.OpenTypeName}))");
            sb.AppendLine("                {");

            foreach (var handler in registrations)
            {
                sb.AppendLine("                    services.Add(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(");
                sb.AppendLine($"                        typeof(global::DSoftStudio.Mediator.Abstractions.IStreamPipelineBehavior<{handler.RequestType}, {handler.ResponseType}>),");
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
    /// Emits the <c>RemoveOpenGenericStreamBehaviorDescriptors</c> method for removing
    /// original open-generic stream behavior descriptors after closure.
    /// </summary>
    private static void EmitRemoveOpenGenericStreamBehaviorDescriptorsMethod(
        StringBuilder sb,
        List<BehaviorTypeInfo> behaviors)
    {
        sb.AppendLine("        private static void RemoveOpenGenericStreamBehaviorDescriptors(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine("            for (int i = services.Count - 1; i >= 0; i--)");
        sb.AppendLine("            {");
        sb.AppendLine("                var d = services[i];");
        sb.AppendLine("                if (d.ImplementationType is null || !d.ServiceType.IsGenericTypeDefinition)");
        sb.AppendLine("                    continue;");

        foreach (var b in behaviors)
        {
            sb.AppendLine();
            sb.AppendLine($"                if (d.ServiceType == typeof(global::DSoftStudio.Mediator.Abstractions.IStreamPipelineBehavior<,>) && d.ImplementationType == typeof({b.OpenTypeName}))");
            sb.AppendLine("                {");
            sb.AppendLine("                    services.RemoveAt(i);");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    internal readonly struct StreamHandlerInfo : System.IEquatable<StreamHandlerInfo>
    {
        public string RequestType { get; }
        public string ResponseType { get; }
        public string HandlerType { get; }

        public StreamHandlerInfo(string requestType, string responseType, string handlerType)
        {
            RequestType = requestType;
            ResponseType = responseType;
            HandlerType = handlerType;
        }

        public bool Equals(StreamHandlerInfo other) =>
            RequestType == other.RequestType &&
            ResponseType == other.ResponseType &&
            HandlerType == other.HandlerType;

        public override bool Equals(object obj) =>
            obj is StreamHandlerInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RequestType.GetHashCode();
                hash = (hash * 397) ^ ResponseType.GetHashCode();
                hash = (hash * 397) ^ HandlerType.GetHashCode();
                return hash;
            }
        }
    }
}
