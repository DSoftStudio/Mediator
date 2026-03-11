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

        var combined = localCollected.Combine(externalHandlers);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var (localHandlers, external) = pair;

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

            var code = GenerateCode(localRegistrations, allRegistrations);

            spc.AddSource(
                "MediatorServiceRegistry.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
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

    /// <param name="localHandlers">Handlers discovered in the current project (emit assembly attributes for these).</param>
    /// <param name="allHandlers">Local + external handlers (register all in DI).</param>
    private static string GenerateCode(HandlerInfo[] localHandlers, HandlerInfo[] allHandlers)
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

        return sb.ToString();
    }

    internal readonly struct HandlerInfo : IEquatable<HandlerInfo>
    {
        public string InterfaceType { get; }
        public string HandlerType { get; }
        public bool IsStateless { get; }

        public HandlerInfo(string iface, string handler, bool isStateless = false)
        {
            InterfaceType = iface;
            HandlerType = handler;
            IsStateless = isStateless;
        }

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