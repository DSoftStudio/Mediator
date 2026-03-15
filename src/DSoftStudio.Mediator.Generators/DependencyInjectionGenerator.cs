// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Text;
using System.Threading;

namespace DSoftStudio.Mediator.Generators;

[Generator]
public sealed class DependencyInjectionGenerator : IIncrementalGenerator
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

        // Scan referenced assemblies for [MediatorHandlerRegistration] attributes
        var externalHandlers = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var external = ReferencedAssemblyScanner.GetExternalDIHandlers(compilation);
                var array = external
                    .Select(e => new HandlerInfo(e.ServiceType, e.ImplementationType, e.IsStateless))
                    .OrderBy(static h => h.InterfaceType)
                    .ThenBy(static h => h.HandlerType)
                    .ToArray();
                return new EquatableArray<HandlerInfo>(array);
            });

        // Discover self-handling request classes (IRequest<T> + static Execute)
        var selfHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null }
                    || node is RecordDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => GetSelfHandlerInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var selfCollected = selfHandlers.Collect();

        var combined = localCollected.Combine(externalHandlers).Combine(selfCollected);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var ((localHandlers, external), selfHandlers) = pair;

            var localRegistrations = localHandlers
                .Distinct()
                .OrderBy(static h => h.InterfaceType)
                .ThenBy(static h => h.HandlerType)
                .ToArray();

            // Merge local + external, deduplicate
            var allRegistrations = localRegistrations
                .Concat(external)
                .Distinct()
                .OrderBy(static h => h.InterfaceType)
                .ThenBy(static h => h.HandlerType)
                .ToArray();

            var selfHandlerList = selfHandlers.IsDefaultOrEmpty
                ? System.Array.Empty<SelfHandlerDetail>()
                : selfHandlers.Distinct()
                    .OrderBy(static h => h.RequestType)
                    .ToArray();

            // Detect duplicate request/stream handlers (silent "last wins" bug)
            ReportDuplicateHandlers(spc, allRegistrations);

            var code = GenerateCode(localRegistrations, allRegistrations, selfHandlerList);

            spc.AddSource(
                "MediatorServiceRegistry.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
    }

    /// <summary>
    /// Reports compile-time diagnostics for request/stream handler types that have
    /// multiple implementations. With Microsoft.Extensions.DI, <c>GetRequiredService&lt;T&gt;</c>
    /// returns the last registration — earlier handlers are silently ignored.
    /// Notification handlers are excluded (multiple handlers per notification is by design).
    /// </summary>
    private static void ReportDuplicateHandlers(SourceProductionContext spc, HandlerInfo[] allHandlers)
    {
        const string RequestPrefix =
            "global::DSoftStudio.Mediator.Abstractions.IRequestHandler<";
        const string StreamPrefix =
            "global::DSoftStudio.Mediator.Abstractions.IStreamRequestHandler<";

        var groups = allHandlers
            .GroupBy(static h => h.InterfaceType)
            .Where(static g => g.Count() > 1);

        foreach (var group in groups)
        {
            var interfaceType = group.Key;
            var handlerNames = string.Join(", ", group.Select(static h => h.HandlerType));

            if (interfaceType.StartsWith(RequestPrefix))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateRequestHandler,
                    Location.None,
                    interfaceType,
                    handlerNames));
            }
            else if (interfaceType.StartsWith(StreamPrefix))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateStreamHandler,
                    Location.None,
                    interfaceType,
                    handlerNames));
            }
            // Notification handlers: multiple implementations per type is expected — no diagnostic
        }
    }

    private static HandlerInfo? GetHandlerInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract || symbol.TypeKind != TypeKind.Class)
            return null;

        // File-scoped types (C# 11+) cannot be referenced from generated code.
        if (HandlerDiscovery.IsFileLocal(classDecl))
            return null;

        // Handlers with no constructor parameters are stateless — safe to register as Singleton.
        bool isStateless = symbol.InstanceConstructors.Length > 0
            && symbol.InstanceConstructors.All(static c => c.Parameters.IsEmpty);

        foreach (var iface in symbol.AllInterfaces)
        {
            var ns = iface.ContainingNamespace.ToDisplayString();

            if (ns != "DSoftStudio.Mediator.Abstractions")
                continue;

            switch (iface.MetadataName)
            {
                case "IRequestHandler`2":
                    {
                        var request = iface.TypeArguments[0]
                            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        var response = iface.TypeArguments[1]
                            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        return new HandlerInfo(
                            $"global::DSoftStudio.Mediator.Abstractions.IRequestHandler<{request},{response}>",
                            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            isStateless);
                    }

                case "INotificationHandler`1":
                    {
                        var notification = iface.TypeArguments[0]
                            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        return new HandlerInfo(
                            $"global::DSoftStudio.Mediator.Abstractions.INotificationHandler<{notification}>",
                            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            isStateless);
                    }

                case "IStreamRequestHandler`2":
                    {
                        var request = iface.TypeArguments[0]
                            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        var response = iface.TypeArguments[1]
                            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        return new HandlerInfo(
                            $"global::DSoftStudio.Mediator.Abstractions.IStreamRequestHandler<{request},{response}>",
                            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            isStateless);
                    }
            }
        }

        return null;
    }

    private static SelfHandlerDetail? GetSelfHandlerInfo(
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

    /// <param name="localHandlers">Handlers discovered in the current project (emit assembly attributes for these).</param>
    /// <param name="allHandlers">Local + external handlers (register all in DI).</param>
    /// <param name="selfHandlers">Self-handling request classes (IRequest&lt;T&gt; + static Execute).</param>
    private static string GenerateCode(HandlerInfo[] localHandlers, HandlerInfo[] allHandlers, SelfHandlerDetail[] selfHandlers)
    {
        var sb = new StringBuilder(2048);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Emit assembly-level attributes for LOCAL handlers only.
        // Downstream (referencing) projects read these to discover our handlers.
        foreach (var handler in localHandlers)
        {
            sb.Append("[assembly: global::DSoftStudio.Mediator.Abstractions.MediatorHandlerRegistration(typeof(");
            sb.Append(handler.InterfaceType);
            sb.Append("), typeof(");
            sb.Append(handler.HandlerType);
            sb.AppendLine("))]");
        }

        if (localHandlers.Length > 0)
            sb.AppendLine();

        sb.AppendLine("namespace DSoftStudio.Mediator;");
        sb.AppendLine();

        // Generate adapter classes for self-handling request types
        GenerateSelfHandlerAdapters(sb, selfHandlers);

        sb.AppendLine("internal static class MediatorServiceRegistry");
        sb.AppendLine("{");

        sb.AppendLine("    public static void Register(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");

        // Register ALL handlers (local + external) in DI
        var registeredConcreteTypes = new System.Collections.Generic.HashSet<string>();

        foreach (var handler in allHandlers)
        {
            // Stateless handlers (no constructor parameters) → Singleton (zero allocation per call).
            // Handlers with DI dependencies → Transient (safe default).
            sb.Append(handler.IsStateless
                ? "        global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<"
                : "        global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddTransient<");
            sb.Append(handler.InterfaceType);
            sb.Append(", ");
            sb.Append(handler.HandlerType);
            sb.AppendLine(">(services);");

            // Notification and stream dispatch tables resolve by CONCRETE type,
            // so we must also register the implementation type directly.
            if (!handler.InterfaceType.Contains("IRequestHandler<") && registeredConcreteTypes.Add(handler.HandlerType))
            {
                sb.Append(handler.IsStateless
                    ? "        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, typeof("
                    : "        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddTransient(services, typeof(");
                sb.Append(handler.HandlerType);
                sb.AppendLine("));");
            }
        }

        // Register self-handler adapters in DI
        foreach (var handler in selfHandlers)
        {
            var adapterFqn = "global::DSoftStudio.Mediator.__SelfHandler_"
                + HandlerDiscovery.SanitizeIdentifier(handler.RequestType);

            bool isStateless = true;
            foreach (var param in handler.Parameters)
            {
                if (param.Kind == SelfHandlerParam.KindService)
                {
                    isStateless = false;
                    break;
                }
            }

            sb.Append(isStateless
                ? "        global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<"
                : "        global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddTransient<");
            sb.Append($"global::DSoftStudio.Mediator.Abstractions.IRequestHandler<{handler.RequestType}, {handler.ResponseType}>, ");
            sb.Append(adapterFqn);
            sb.AppendLine(">(services);");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("internal static class MediatorServiceRegistryExtensions");
        sb.AppendLine("{");

        sb.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection RegisterMediatorHandlers(");
        sb.AppendLine("        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");

        sb.AppendLine("        MediatorServiceRegistry.Register(services);");
        sb.AppendLine("        return services;");

        sb.AppendLine("    }");

        sb.AppendLine("}");
        sb.AppendLine();

        GenerateHandlerValidator(sb, allHandlers, selfHandlers);

        return sb.ToString();
    }

    /// <summary>
    /// Generates adapter classes that bridge self-handling request classes
    /// (IRequest&lt;T&gt; + static Execute) to the IRequestHandler&lt;,&gt; contract.
    /// </summary>
    private static void GenerateSelfHandlerAdapters(
        StringBuilder sb,
        SelfHandlerDetail[] selfHandlers)
    {
        foreach (var handler in selfHandlers)
        {
            var adapterName = "__SelfHandler_"
                + HandlerDiscovery.SanitizeIdentifier(handler.RequestType);

            // Collect service parameters
            var services = new System.Collections.Generic.List<(string TypeName, int Index)>();
            int serviceIndex = 0;
            foreach (var param in handler.Parameters)
            {
                if (param.Kind == SelfHandlerParam.KindService)
                {
                    services.Add((param.TypeName, serviceIndex));
                    serviceIndex++;
                }
            }

            sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
            sb.Append($"internal sealed class {adapterName} : ");
            sb.AppendLine($"global::DSoftStudio.Mediator.Abstractions.IRequestHandler<{handler.RequestType}, {handler.ResponseType}>");
            sb.AppendLine("{");

            // Fields
            for (int i = 0; i < services.Count; i++)
                sb.AppendLine($"    private readonly {services[i].TypeName} _s{services[i].Index};");
            if (services.Count > 0)
                sb.AppendLine();

            // Constructor (only if there are DI services)
            if (services.Count > 0)
            {
                sb.Append($"    public {adapterName}(");
                for (int i = 0; i < services.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append($"{services[i].TypeName} s{services[i].Index}");
                }
                sb.AppendLine(")");
                sb.AppendLine("    {");
                for (int i = 0; i < services.Count; i++)
                    sb.AppendLine($"        _s{services[i].Index} = s{services[i].Index};");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            // Handle method
            bool needsAsync = handler.ReturnKind == SelfHandlerDetail.ReturnTask;
            sb.Append("    public ");
            if (needsAsync) sb.Append("async ");
            sb.Append($"global::System.Threading.Tasks.ValueTask<{handler.ResponseType}> Handle(");
            sb.Append($"{handler.RequestType} request, ");
            sb.AppendLine("global::System.Threading.CancellationToken cancellationToken)");
            sb.AppendLine("    {");

            // Build Execute call arguments (in declaration order)
            var args = new StringBuilder();
            int svcIdx = 0;
            bool first = true;
            foreach (var param in handler.Parameters)
            {
                if (!first) args.Append(", ");
                first = false;

                switch (param.Kind)
                {
                    case SelfHandlerParam.KindRequest:
                        args.Append("request");
                        break;
                    case SelfHandlerParam.KindCancellationToken:
                        args.Append("cancellationToken");
                        break;
                    case SelfHandlerParam.KindService:
                        args.Append($"_s{svcIdx}");
                        svcIdx++;
                        break;
                }
            }

            var callExpr = $"{handler.RequestType}.Execute({args})";

            switch (handler.ReturnKind)
            {
                case SelfHandlerDetail.ReturnSync:
                    sb.AppendLine($"        return new global::System.Threading.Tasks.ValueTask<{handler.ResponseType}>({callExpr});");
                    break;
                case SelfHandlerDetail.ReturnTaskOfT:
                    sb.AppendLine($"        return new global::System.Threading.Tasks.ValueTask<{handler.ResponseType}>({callExpr});");
                    break;
                case SelfHandlerDetail.ReturnValueTaskOfT:
                    sb.AppendLine($"        return {callExpr};");
                    break;
                case SelfHandlerDetail.ReturnVoid:
                    sb.AppendLine($"        {callExpr};");
                    sb.AppendLine($"        return new global::System.Threading.Tasks.ValueTask<{handler.ResponseType}>(global::DSoftStudio.Mediator.Abstractions.Unit.Value);");
                    break;
                case SelfHandlerDetail.ReturnTask:
                    sb.AppendLine($"        await {callExpr};");
                    sb.AppendLine($"        return global::DSoftStudio.Mediator.Abstractions.Unit.Value;");
                    break;
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }

    private static void GenerateHandlerValidator(StringBuilder sb, HandlerInfo[] allHandlers, SelfHandlerDetail[] selfHandlers)
    {
        const string RequestPrefix =
            "global::DSoftStudio.Mediator.Abstractions.IRequestHandler<";
        const string NotificationPrefix =
            "global::DSoftStudio.Mediator.Abstractions.INotificationHandler<";
        const string StreamPrefix =
            "global::DSoftStudio.Mediator.Abstractions.IStreamRequestHandler<";

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated fail-fast handler validator.");
        sb.AppendLine("/// Resolves every mediator handler from DI at startup to detect");
        sb.AppendLine("/// misconfiguration before the first request is processed.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class MediatorHandlerValidator");
        sb.AppendLine("{");
        sb.AppendLine("    public static void Validate(global::System.IServiceProvider serviceProvider)");
        sb.AppendLine("    {");

        if (allHandlers.Length > 0 || selfHandlers.Length > 0)
        {
            sb.AppendLine("        using var scope = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(serviceProvider);");
            sb.AppendLine("        var sp = scope.ServiceProvider;");
            sb.AppendLine("        var errors = new global::System.Collections.Generic.List<global::System.Exception>();");
            sb.AppendLine();

            var emittedInterfaces = new System.Collections.Generic.HashSet<string>();

            foreach (var handler in allHandlers)
            {
                // Skip duplicate interface types (e.g. multiple notification handlers
                // for the same notification type — GetServices validates all at once).
                if (!emittedInterfaces.Add(handler.InterfaceType))
                    continue;

                if (handler.InterfaceType.StartsWith(RequestPrefix))
                {
                    // Validate request handler
                    sb.AppendLine($"        try {{ global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{handler.InterfaceType}>(sp); }}");
                    sb.AppendLine("        catch (global::System.Exception ex) { errors.Add(ex); }");

                    // Validate pipeline chain if registered (behaviors, processors, exception handlers)
                    var chainType = handler.InterfaceType.Replace(RequestPrefix,
                        "global::DSoftStudio.Mediator.PipelineChainHandler<");
                    sb.AppendLine($"        try {{ global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<{chainType}>(sp); }}");
                    sb.AppendLine("        catch (global::System.Exception ex) { errors.Add(ex); }");
                }
                else if (handler.InterfaceType.StartsWith(NotificationPrefix))
                {
                    // Validate all notification handlers (GetServices materializes every implementation)
                    sb.AppendLine($"        try {{ foreach (var _ in global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices<{handler.InterfaceType}>(sp)) {{ }} }}");
                    sb.AppendLine("        catch (global::System.Exception ex) { errors.Add(ex); }");
                }
                else if (handler.InterfaceType.StartsWith(StreamPrefix))
                {
                    // Validate stream handler
                    sb.AppendLine($"        try {{ global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{handler.InterfaceType}>(sp); }}");
                    sb.AppendLine("        catch (global::System.Exception ex) { errors.Add(ex); }");

                    // Validate stream pipeline chain if registered
                    var chainType = handler.InterfaceType.Replace(StreamPrefix,
                        "global::DSoftStudio.Mediator.StreamPipelineChainHandler<");
                    sb.AppendLine($"        try {{ global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<{chainType}>(sp); }}");
                    sb.AppendLine("        catch (global::System.Exception ex) { errors.Add(ex); }");
                }
            }

            // Validate self-handler adapters
            foreach (var handler in selfHandlers)
            {
                var ifaceType = $"global::DSoftStudio.Mediator.Abstractions.IRequestHandler<{handler.RequestType},{handler.ResponseType}>";
                if (!emittedInterfaces.Add(ifaceType))
                    continue;

                sb.AppendLine($"        try {{ global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{ifaceType}>(sp); }}");
                sb.AppendLine("        catch (global::System.Exception ex) { errors.Add(ex); }");
            }

            sb.AppendLine();
            sb.AppendLine("        if (errors.Count > 0)");
            sb.AppendLine("            throw new global::System.AggregateException(");
            sb.AppendLine("                \"One or more mediator handlers failed validation. See inner exceptions for details.\",");
            sb.AppendLine("                errors);");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension method for fail-fast mediator handler validation.");
        sb.AppendLine("/// Call after <c>BuildServiceProvider()</c> / <c>builder.Build()</c> to");
        sb.AppendLine("/// detect misconfigured handlers before the first request.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class MediatorHandlerValidatorExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static void ValidateMediatorHandlers(");
        sb.AppendLine("        this global::System.IServiceProvider serviceProvider)");
        sb.AppendLine("    {");
        sb.AppendLine("        MediatorHandlerValidator.Validate(serviceProvider);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    internal readonly struct HandlerInfo(string iface, string handler, bool isStateless = false) : IEquatable<HandlerInfo>
    {
        public string InterfaceType { get; } = iface;
        public string HandlerType { get; } = handler;
        public bool IsStateless { get; } = isStateless;

        public bool Equals(HandlerInfo other) =>
            InterfaceType == other.InterfaceType &&
            HandlerType == other.HandlerType;

        public override bool Equals(object? obj) =>
            obj is HandlerInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (InterfaceType.GetHashCode() * 397) ^ HandlerType.GetHashCode();
            }
        }
    }
}
