// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace DSoftStudio.Mediator.Generators
{
    /// <summary>
    /// Discovers handler registrations from referenced assemblies using a two-phase strategy:
    /// <list type="number">
    ///   <item>
    ///     <b>Phase 1 — Attribute-based (fast path):</b>
    ///     Reads <c>[assembly: MediatorHandlerRegistration(...)]</c> attributes emitted by
    ///     assemblies that ran the DSoftStudio.Mediator source generator.
    ///   </item>
    ///   <item>
    ///     <b>Phase 2 — Type-based (fallback):</b>
    ///     For assemblies that reference <c>DSoftStudio.Mediator.Abstractions</c> but have
    ///     <em>no</em> <c>[MediatorHandlerRegistration]</c> attributes (typically
    ///     domain/application-layer projects that only reference the Abstractions package),
    ///     walks all exported types and discovers handler interface implementations directly.
    ///   </item>
    /// </list>
    /// This ensures downstream generators (DI registration, typed extensions, interceptors)
    /// discover handlers regardless of whether the upstream project ran the generator.
    /// </summary>
    internal static class ReferencedAssemblyScanner
    {
        private const string AttributeFullName =
            "DSoftStudio.Mediator.Abstractions.MediatorHandlerRegistrationAttribute";

        private const string RequestHandlerMetadataName = "IRequestHandler`2";
        private const string NotificationHandlerMetadataName = "INotificationHandler`1";
        private const string StreamHandlerMetadataName = "IStreamRequestHandler`2";

        private const string AbstractionsNamespace = "DSoftStudio.Mediator.Abstractions";
        private const string AbstractionsAssemblyName = "DSoftStudio.Mediator.Abstractions";

        /// <summary>
        /// Returns all (ServiceType, ImplementationType) pairs discovered from referenced
        /// assemblies using both attribute-based and type-based scanning.
        /// </summary>
        public static List<ExternalHandlerInfo> GetAllExternalHandlers(Compilation compilation)
        {
            var results = new List<ExternalHandlerInfo>();

            var attrType = compilation.GetTypeByMetadataName(AttributeFullName);

            // Track assemblies that had [MediatorHandlerRegistration] attributes
            // so Phase 2 only scans assemblies WITHOUT them.
            var assembliesWithAttributes = new HashSet<string>(System.StringComparer.Ordinal);

            // ── Phase 1: Attribute-based discovery (fast path) ───────────
            if (attrType is not null)
            {
                foreach (var reference in compilation.References)
                {
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                        continue;

                    int countBefore = results.Count;
                    CollectHandlersFromAttributes(assembly, attrType, results);

                    if (results.Count > countBefore)
                        assembliesWithAttributes.Add(assembly.Identity.Name);
                }
            }

            // ── Phase 2: Type-based discovery (fallback) ─────────────────
            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                    continue;

                // Skip assemblies already covered by Phase 1
                if (assembliesWithAttributes.Contains(assembly.Identity.Name))
                    continue;

                // Only scan assemblies that actually reference Abstractions
                if (!ReferencesAbstractions(assembly))
                    continue;

                CollectHandlersFromTypes(assembly, results);
            }

            return results;
        }

        // ── Phase 1 helpers ──────────────────────────────────────────────

        private static void CollectHandlersFromAttributes(
            IAssemblySymbol assembly,
            INamedTypeSymbol attrType,
            List<ExternalHandlerInfo> results)
        {
            foreach (var attr in assembly.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType))
                    continue;

                if (attr.ConstructorArguments.Length < 2)
                    continue;

                if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol serviceType)
                    continue;

                if (attr.ConstructorArguments[1].Value is not INamedTypeSymbol implType)
                    continue;

                results.Add(new ExternalHandlerInfo(serviceType, implType));
            }
        }

        // ── Phase 2 helpers ──────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> when the assembly directly references
        /// <c>DSoftStudio.Mediator.Abstractions</c>.
        /// </summary>
        private static bool ReferencesAbstractions(IAssemblySymbol assembly)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var referenced in module.ReferencedAssemblySymbols)
                {
                    if (referenced.Name == AbstractionsAssemblyName)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Walks all exported (public, non-abstract, non-generic) types in the assembly
        /// and collects handler interface implementations.
        /// </summary>
        private static void CollectHandlersFromTypes(
            IAssemblySymbol assembly,
            List<ExternalHandlerInfo> results)
        {
            var types = new List<INamedTypeSymbol>();
            CollectConcreteTypes(assembly.GlobalNamespace, types);

            foreach (var type in types)
            {
                foreach (var iface in type.AllInterfaces)
                {
                    var original = iface.OriginalDefinition;

                    var ns = original.ContainingNamespace?.ToDisplayString();
                    if (ns != AbstractionsNamespace)
                        continue;

                    var metaName = original.MetadataName;
                    if (metaName == RequestHandlerMetadataName
                        || metaName == NotificationHandlerMetadataName
                        || metaName == StreamHandlerMetadataName)
                    {
                        results.Add(new ExternalHandlerInfo(iface, type));
                    }
                }
            }
        }

        /// <summary>
        /// Recursively collects all concrete (non-abstract, non-generic) class types
        /// from a namespace tree. Only collects types accessible from downstream projects.
        /// </summary>
        private static void CollectConcreteTypes(
            INamespaceSymbol ns,
            List<INamedTypeSymbol> types)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (IsConcreteHandler(type))
                    types.Add(type);

                CollectNestedConcreteTypes(type, types);
            }

            foreach (var child in ns.GetNamespaceMembers())
                CollectConcreteTypes(child, types);
        }

        private static void CollectNestedConcreteTypes(
            INamedTypeSymbol parent,
            List<INamedTypeSymbol> types)
        {
            foreach (var nested in parent.GetTypeMembers())
            {
                if (IsConcreteHandler(nested))
                    types.Add(nested);

                CollectNestedConcreteTypes(nested, types);
            }
        }

        /// <summary>
        /// A type is a candidate handler if it's a non-abstract, non-generic class
        /// with public or internal accessibility.
        /// </summary>
        private static bool IsConcreteHandler(INamedTypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Class)
                return false;

            if (type.IsAbstract || type.IsGenericType)
                return false;

            // Only accessible types (public or internal) can be registered
            if (type.DeclaredAccessibility != Accessibility.Public
                && type.DeclaredAccessibility != Accessibility.Internal)
                return false;

            return true;
        }

        // ── Filtered helpers for each generator ──────────────────────────

        /// <summary>
        /// Returns (requestType, responseType) pairs for <c>IRequestHandler&lt;,&gt;</c>
        /// registrations found in referenced assemblies.
        /// </summary>
        public static List<(string RequestType, string ResponseType)> GetExternalPipelineHandlers(
            Compilation compilation)
        {
            var results = new List<(string, string)>();

            foreach (var handler in GetAllExternalHandlers(compilation))
            {
                var original = handler.ServiceType.OriginalDefinition;

                if (original.MetadataName != RequestHandlerMetadataName)
                    continue;

                var ns = original.ContainingNamespace?.ToDisplayString();
                if (ns != AbstractionsNamespace)
                    continue;

                if (handler.ServiceType.TypeArguments.Length < 2)
                    continue;

                var requestType = handler.ServiceType.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var responseType = handler.ServiceType.TypeArguments[1]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                results.Add((requestType, responseType));
            }

            return results;
        }

        /// <summary>
        /// Returns (notificationType, handlerType) pairs for <c>INotificationHandler&lt;&gt;</c>
        /// registrations found in referenced assemblies.
        /// </summary>
        public static List<(string NotificationType, string HandlerType)> GetExternalNotificationHandlers(
            Compilation compilation)
        {
            var results = new List<(string, string)>();

            foreach (var handler in GetAllExternalHandlers(compilation))
            {
                var original = handler.ServiceType.OriginalDefinition;

                if (original.MetadataName != NotificationHandlerMetadataName)
                    continue;

                var ns = original.ContainingNamespace?.ToDisplayString();
                if (ns != AbstractionsNamespace)
                    continue;

                if (handler.ServiceType.TypeArguments.Length < 1)
                    continue;

                var notificationType = handler.ServiceType.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var handlerType = handler.ImplementationType
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                results.Add((notificationType, handlerType));
            }

            return results;
        }

        /// <summary>
        /// Returns (requestType, responseType, handlerType) tuples for
        /// <c>IStreamRequestHandler&lt;,&gt;</c> registrations found in referenced assemblies.
        /// </summary>
        public static List<(string RequestType, string ResponseType, string HandlerType)> GetExternalStreamHandlers(
            Compilation compilation)
        {
            var results = new List<(string, string, string)>();

            foreach (var handler in GetAllExternalHandlers(compilation))
            {
                var original = handler.ServiceType.OriginalDefinition;

                if (original.MetadataName != StreamHandlerMetadataName)
                    continue;

                var ns = original.ContainingNamespace?.ToDisplayString();
                if (ns != AbstractionsNamespace)
                    continue;

                if (handler.ServiceType.TypeArguments.Length < 2)
                    continue;

                var requestType = handler.ServiceType.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var responseType = handler.ServiceType.TypeArguments[1]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var handlerType = handler.ImplementationType
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                results.Add((requestType, responseType, handlerType));
            }

            return results;
        }

        /// <summary>
        /// Returns (serviceType, implementationType) string pairs for DI registration
        /// from referenced assemblies.
        /// </summary>
        public static List<(string ServiceType, string ImplementationType, bool IsStateless)> GetExternalDIHandlers(
            Compilation compilation)
        {
            var results = new List<(string, string, bool)>();

            foreach (var handler in GetAllExternalHandlers(compilation))
            {
                var serviceType = handler.ServiceType
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var implType = handler.ImplementationType
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                bool isStateless = handler.ImplementationType.InstanceConstructors.Length > 0
                    && handler.ImplementationType.InstanceConstructors.All(static c => c.Parameters.IsEmpty);

                results.Add((serviceType, implType, isStateless));
            }

            return results;
        }

        internal readonly struct ExternalHandlerInfo(INamedTypeSymbol serviceType, INamedTypeSymbol implementationType)
        {
            public INamedTypeSymbol ServiceType { get; } = serviceType;
            public INamedTypeSymbol ImplementationType { get; } = implementationType;
        }
    }
}
