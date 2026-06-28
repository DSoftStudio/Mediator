// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
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
                var (external, skippedInternals) = ReferencedAssemblyScanner.GetExternalDIHandlers(compilation);
                var array = external
                    .Select(e => new HandlerInfo(e.ServiceType, e.ImplementationType, e.IsStateless))
                    .OrderBy(static h => h.InterfaceType)
                    .ThenBy(static h => h.HandlerType)
                    .ToArray();
                var skippedArray = skippedInternals.ToArray();
                return (
                    Handlers: new EquatableArray<HandlerInfo>(array),
                    Skipped: new EquatableArray<ReferencedAssemblyScanner.SkippedHandlerInfo>(skippedArray));
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

        // Discover local request types (IRequest<T>, ICommand<T>, IQuery<T> implementations)
        var localRequestTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null }
                    || node is RecordDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => GetRequestTypeInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var localRequestTypesCollected = localRequestTypes.Collect();

        // Discover request types from referenced assemblies
        var externalRequestTypes = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var types = ReferencedAssemblyScanner.GetExternalRequestTypes(compilation);
                var array = types
                    .Select(static t => new RequestTypeEntry(t.RequestType, t.ResponseType))
                    .ToArray();
                return new EquatableArray<RequestTypeEntry>(array);
            });

        var allRequestTypes = localRequestTypesCollected.Combine(externalRequestTypes);

        var assemblyName = context.CompilationProvider
            .Select(static (c, _) => c.AssemblyName ?? "Assembly");

        var combined = localCollected
            .Combine(externalHandlers)
            .Combine(selfCollected)
            .Combine(allRequestTypes)
            .Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var ((((localHandlers, (external, skippedInternals)), selfHandlers), (localReqTypes, externalReqTypes)), asmName) = pair;

            // Report diagnostics for internal handlers that were skipped
            foreach (var skipped in skippedInternals)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InternalHandlerSkipped,
                    Location.None,
                    skipped.HandlerType,
                    skipped.AssemblyName));
            }

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

            // Local self-handlers only - external self-handlers are now discovered
            // as regular handlers via [assembly: MediatorHandlerRegistration] attributes.
            var localSelfHandlers = selfHandlers.IsDefaultOrEmpty
                ? []
                : selfHandlers.Distinct()
                    .OrderBy(static h => h.RequestType)
                    .ToArray();

            // Detect duplicate request/stream handlers (silent "last wins" bug)
            ReportDuplicateHandlers(spc, allRegistrations);

            // Detect request types with no handler implementation (DSOFT001)
            var allRequestTypeEntries = localReqTypes
                .Concat(externalReqTypes)
                .Distinct()
                .ToArray();

            ReportMissingHandlers(spc, allRegistrations, localSelfHandlers, allRequestTypeEntries);

            var code = GenerateCode(localRegistrations, allRegistrations, localSelfHandlers, asmName);

            spc.AddSource(
                "MediatorServiceRegistry.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
    }

    /// <summary>
    /// Reports compile-time diagnostics for request/stream handler types that have
    /// multiple implementations. With Microsoft.Extensions.DI, <c>GetRequiredService&lt;T&gt;</c>
    /// returns the last registration - earlier handlers are silently ignored.
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
            // Notification handlers: multiple implementations per type is expected - no diagnostic
        }
    }

    /// <summary>
    /// Reports compile-time diagnostics for request types that have no corresponding
    /// <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> implementation registered.
    /// Self-handling requests (static Execute) are also considered handled.
    /// </summary>
    private static void ReportMissingHandlers(
        SourceProductionContext spc,
        HandlerInfo[] allHandlers,
        SelfHandlerDetail[] selfHandlers,
        RequestTypeEntry[] allRequestTypes)
    {
        if (allRequestTypes.Length == 0)
            return;

        // Build a set of handler interface types for fast lookup
        var handlerInterfaces = new HashSet<string>();
        foreach (var h in allHandlers)
            handlerInterfaces.Add(h.InterfaceType);

        // Self-handler request types
        var selfHandledRequests = new HashSet<string>();
        foreach (var s in selfHandlers)
            selfHandledRequests.Add(s.RequestType);

        foreach (var entry in allRequestTypes)
        {
            // Check if a normal handler is registered.
            // Local handlers use "IRequestHandler<A,B>" (no space) while external handlers
            // use ToDisplayString which produces "IRequestHandler<A, B>" (with space).
            var expectedNoSpace =
                $"global::DSoftStudio.Mediator.Abstractions.IRequestHandler<{entry.RequestType},{entry.ResponseType}>";
            var expectedWithSpace =
                $"global::DSoftStudio.Mediator.Abstractions.IRequestHandler<{entry.RequestType}, {entry.ResponseType}>";

            if (handlerInterfaces.Contains(expectedNoSpace) || handlerInterfaces.Contains(expectedWithSpace))
                continue;

            // Check if it's a self-handling request
            if (selfHandledRequests.Contains(entry.RequestType))
                continue;

            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoHandlerForRequest,
                Location.None,
                entry.RequestType,
                entry.ResponseType));
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

        // Handlers with no constructor parameters are stateless - safe to register as Singleton.
        bool isStateless = symbol.InstanceConstructors.Length > 0
            && symbol.InstanceConstructors.All(static c => c.Parameters.IsEmpty);

        // Capture the dependency types of the constructor DI will use (the greediest public ctor) so the
        // runtime optimizer can raise this handler's lifetime from its dependency lifetimes (AOT-safe: the
        // types are emitted as typeof, never reflected). Empty for stateless handlers.
        string depTypes = "";
        if (!isStateless)
        {
            var ctor = symbol.InstanceConstructors
                .Where(static c => c.DeclaredAccessibility == Accessibility.Public)
                .OrderByDescending(static c => c.Parameters.Length)
                .FirstOrDefault();
            if (ctor is not null && !ctor.Parameters.IsEmpty)
            {
                var depsBuilder = new System.Text.StringBuilder();
                for (int p = 0; p < ctor.Parameters.Length; p++)
                {
                    if (p > 0) depsBuilder.Append('|'); // '|' never appears in a type name (generics use < , >)
                    depsBuilder.Append(ctor.Parameters[p].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
                depTypes = depsBuilder.ToString();
            }
        }

        // An explicit [HandlerLifetime(...)] pins the lifetime: it is emitted directly and skips the optimizer.
        string? explicitLifetime = null;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == "DSoftStudio.Mediator.Abstractions.HandlerLifetimeAttribute"
                && attr.ConstructorArguments.Length == 1
                && attr.ConstructorArguments[0].Value is int lifetimeValue)
            {
                explicitLifetime = lifetimeValue switch { 1 => "Scoped", 2 => "Singleton", _ => "Transient" };
                break;
            }
        }

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
                            .ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat);

                        var response = iface.TypeArguments[1]
                            .ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat);

                        return new HandlerInfo(
                            $"global::DSoftStudio.Mediator.Abstractions.IRequestHandler<{request},{response}>",
                            symbol.ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat),
                            isStateless, depTypes, explicitLifetime);
                    }

                case "INotificationHandler`1":
                    {
                        var notification = iface.TypeArguments[0]
                            .ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat);

                        return new HandlerInfo(
                            $"global::DSoftStudio.Mediator.Abstractions.INotificationHandler<{notification}>",
                            symbol.ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat),
                            isStateless, depTypes, explicitLifetime);
                    }

                case "IStreamRequestHandler`2":
                    {
                        var request = iface.TypeArguments[0]
                            .ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat);

                        var response = iface.TypeArguments[1]
                            .ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat);

                        return new HandlerInfo(
                            $"global::DSoftStudio.Mediator.Abstractions.IStreamRequestHandler<{request},{response}>",
                            symbol.ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat),
                            isStateless, depTypes, explicitLifetime);
                    }
            }
        }

        return null;
    }

    private static RequestTypeEntry? GetRequestTypeInfo(
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

        if (!HandlerDiscovery.TryGetRequestType(symbol, ct, out var requestType, out var responseType))
            return null;

        return new RequestTypeEntry(requestType, responseType);
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
    /// <param name="assemblyName">The consuming assembly name - used to generate a unique namespace for extension classes.</param>
    private static string GenerateCode(HandlerInfo[] localHandlers, HandlerInfo[] allHandlers, SelfHandlerDetail[] selfHandlers, string assemblyName)
    {
        var sanitizedAsm = HandlerDiscovery.SanitizeIdentifier(assemblyName);
        var sb = new StringBuilder(2048);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"global using DSoftStudio.Mediator.Generated.{sanitizedAsm};");
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

        // Emit assembly-level attributes for LOCAL self-handler adapters.
        // Downstream projects discover these as regular IRequestHandler registrations.
        foreach (var handler in selfHandlers)
        {
            var adapterFqn = $"global::DSoftStudio.Mediator.Generated.{sanitizedAsm}.__SelfHandler_"
                + HandlerDiscovery.SanitizeIdentifier(handler.RequestType);
            var ifaceType = $"global::DSoftStudio.Mediator.Abstractions.IRequestHandler<{handler.RequestType},{handler.ResponseType}>";

            sb.Append("[assembly: global::DSoftStudio.Mediator.Abstractions.MediatorHandlerRegistration(typeof(");
            sb.Append(ifaceType);
            sb.Append("), typeof(");
            sb.Append(adapterFqn);
            sb.AppendLine("))]");
        }

        if (localHandlers.Length > 0 || selfHandlers.Length > 0)
            sb.AppendLine();

        sb.AppendLine("namespace DSoftStudio.Mediator");
        sb.AppendLine("{");
        sb.AppendLine();

        sb.AppendLine("file static class MediatorServiceRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    private sealed class __Sentinel { }");
        sb.AppendLine();

        sb.AppendLine("    public static void Register(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var d in services)");
        sb.AppendLine("            if (d.ServiceType == typeof(__Sentinel))");
        sb.AppendLine("                return;");
        sb.AppendLine("        global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<__Sentinel>(services);");
        sb.AppendLine();

        // Register ALL handlers (local + external) in DI
        var registeredConcreteTypes = new System.Collections.Generic.HashSet<string>();
        // Request handlers eligible for the deferred lifetime upgrade: they resolve by interface, carry
        // dependencies, and have no explicit [HandlerLifetime]. Each is added through an explicit descriptor
        // captured in a local so the finalization pass can verify ours is still the live registration.
        var optimizableHandlers = new System.Collections.Generic.List<(int Index, HandlerInfo Handler)>();

        foreach (var handler in allHandlers)
        {
            var isOptimizable = handler.ExplicitLifetime is null
                && !handler.IsStateless
                && handler.DepTypes.Length > 0
                && handler.InterfaceType.Contains("IRequestHandler<");

            if (isOptimizable)
            {
                // Same effect as AddTransient, but the captured descriptor reference lets
                // HandlerLifetimeOptimizer.Apply confirm ours is still the winning registration before
                // upgrading. The startup optimizer may then raise it to Singleton (all-singleton deps) or
                // Scoped (any scoped dep) once every registration is visible.
                var optIndex = optimizableHandlers.Count;
                sb.Append("        var __mh");
                sb.Append(optIndex);
                sb.Append(" = global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient(typeof(");
                sb.Append(handler.InterfaceType);
                sb.Append("), typeof(");
                sb.Append(handler.HandlerType);
                sb.AppendLine("));");
                sb.Append("        services.Add(__mh");
                sb.Append(optIndex);
                sb.AppendLine(");");
                optimizableHandlers.Add((optIndex, handler));
            }
            else
            {
                // Lifetime: an explicit [HandlerLifetime] wins; otherwise stateless handlers are Singleton
                // (zero allocation per call) and handlers-with-deps default to Transient.
                var addMethod = handler.ExplicitLifetime switch
                {
                    "Singleton" => "AddSingleton<",
                    "Scoped" => "AddScoped<",
                    "Transient" => "AddTransient<",
                    _ => handler.IsStateless ? "AddSingleton<" : "AddTransient<",
                };
                sb.Append("        global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.");
                sb.Append(addMethod);
                sb.Append(handler.InterfaceType);
                sb.Append(", ");
                sb.Append(handler.HandlerType);
                sb.AppendLine(">(services);");
            }

            // Notification and stream dispatch tables resolve by CONCRETE type,
            // so we must also register the implementation type directly (matching lifetime).
            if (!handler.InterfaceType.Contains("IRequestHandler<") && registeredConcreteTypes.Add(handler.HandlerType))
            {
                var tryAddMethod = handler.ExplicitLifetime switch
                {
                    "Singleton" => "TryAddSingleton",
                    "Scoped" => "TryAddScoped",
                    "Transient" => "TryAddTransient",
                    _ => handler.IsStateless ? "TryAddSingleton" : "TryAddTransient",
                };
                sb.Append("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.");
                sb.Append(tryAddMethod);
                sb.Append("(services, typeof(");
                sb.Append(handler.HandlerType);
                sb.AppendLine("));");
            }
        }

        // Deferred lifetime optimization: stage each eligible request handler so the finalization step
        // (PrecompilePipelines / the single-call AddMediator) can raise it from the Transient default to the
        // longest SAFE lifetime its dependencies allow - once ALL registrations are visible, regardless of
        // whether a dependency was registered before or after this call. AOT-safe (dependency types emitted
        // as typeof; only registered descriptor lifetimes are read).
        if (optimizableHandlers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("        global::DSoftStudio.Mediator.HandlerLifetimeOptimizer.Stage(services, new (global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor, global::System.Type[])[]");
            sb.AppendLine("        {");
            foreach (var (index, handler) in optimizableHandlers)
            {
                sb.Append("            (__mh");
                sb.Append(index);
                sb.Append(", new global::System.Type[] { ");
                var deps = handler.DepTypes.Split('|');
                for (int d = 0; d < deps.Length; d++)
                {
                    if (d > 0) sb.Append(", ");
                    sb.Append("typeof(");
                    sb.Append(deps[d]);
                    sb.Append(')');
                }
                sb.AppendLine(" }),");
            }
            sb.AppendLine("        });");
        }

        // Register local self-handler adapters in DI
        foreach (var handler in selfHandlers)
        {
            var adapterFqn = $"global::DSoftStudio.Mediator.Generated.{sanitizedAsm}.__SelfHandler_"
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

        // Emit validator worker class (file-scoped) inside DSoftStudio.Mediator namespace
        GenerateHandlerValidatorWorker(sb, allHandlers, selfHandlers);

        // Close DSoftStudio.Mediator namespace
        sb.AppendLine("} // namespace DSoftStudio.Mediator");
        sb.AppendLine();

        // Open per-assembly unique namespace for extension classes (avoids CS0121 with InternalsVisibleTo)
        sb.AppendLine($"namespace DSoftStudio.Mediator.Generated.{sanitizedAsm}");
        sb.AppendLine("{");
        sb.AppendLine();

        // Generate public adapter classes for self-handling request types.
        // These must be public so downstream projects can reference them via
        // [assembly: MediatorHandlerRegistration] attributes.
        GenerateSelfHandlerAdapters(sb, selfHandlers);

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

        GenerateHandlerValidatorExtension(sb);

        // Close per-assembly namespace
        sb.AppendLine();
        sb.AppendLine("} // namespace");

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
            sb.Append($"public sealed class {adapterName} : ");
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

    private static void GenerateHandlerValidatorWorker(StringBuilder sb, HandlerInfo[] allHandlers, SelfHandlerDetail[] selfHandlers)
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
        sb.AppendLine("file static class MediatorHandlerValidator");
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
                // for the same notification type - GetServices validates all at once).
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
    }

    private static void GenerateHandlerValidatorExtension(StringBuilder sb)
    {
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

    internal readonly struct RequestTypeEntry(string requestType, string responseType) : IEquatable<RequestTypeEntry>
    {
        public string RequestType { get; } = requestType;
        public string ResponseType { get; } = responseType;

        public bool Equals(RequestTypeEntry other) =>
            RequestType == other.RequestType &&
            ResponseType == other.ResponseType;

        public override bool Equals(object? obj) =>
            obj is RequestTypeEntry other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (RequestType.GetHashCode() * 397) ^ ResponseType.GetHashCode();
            }
        }
    }

    internal readonly struct HandlerInfo(string iface, string handler, bool isStateless = false, string depTypes = "", string? explicitLifetime = null) : IEquatable<HandlerInfo>
    {
        public string InterfaceType { get; } = iface;
        public string HandlerType { get; } = handler;
        public bool IsStateless { get; } = isStateless;

        // Comma-joined fully-qualified constructor dependency types (greediest public ctor), captured at
        // compile time so the runtime optimizer can pick the lifetime from their registered lifetimes
        // without reflection. A string (not an array) keeps the incremental-generator model cached by value.
        public string DepTypes { get; } = depTypes;

        // "Singleton"/"Scoped"/"Transient" when the handler carries an explicit [HandlerLifetime]; null otherwise.
        public string? ExplicitLifetime { get; } = explicitLifetime;

        public bool Equals(HandlerInfo other) =>
            InterfaceType == other.InterfaceType &&
            HandlerType == other.HandlerType &&
            IsStateless == other.IsStateless &&
            DepTypes == other.DepTypes &&
            ExplicitLifetime == other.ExplicitLifetime;

        public override bool Equals(object? obj) =>
            obj is HandlerInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (InterfaceType.GetHashCode() * 397) ^ HandlerType.GetHashCode();
                return (hash * 397) ^ DepTypes.GetHashCode();
            }
        }
    }
}
