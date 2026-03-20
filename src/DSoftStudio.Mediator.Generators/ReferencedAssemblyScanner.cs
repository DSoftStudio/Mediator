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
        /// Handlers whose implementation type is not accessible from the consuming project
        /// (e.g. internal without <c>[InternalsVisibleTo]</c>) are excluded from the
        /// returned list. When <paramref name="skippedInternalHandlers"/> is provided,
        /// skipped handler details are collected for diagnostic reporting.
        /// </summary>
        public static List<ExternalHandlerInfo> GetAllExternalHandlers(
            Compilation compilation,
            List<SkippedHandlerInfo> skippedInternalHandlers = null)
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

            // ── Post-filter: remove handlers inaccessible from this compilation ──
            // Both Phase 1 and Phase 2 may discover internal handlers from external
            // assemblies. If the handler's assembly does not grant [InternalsVisibleTo]
            // to the current compilation, the generated DI code would fail with CS0122.
            for (int i = results.Count - 1; i >= 0; i--)
            {
                if (IsAccessibleFromCompilation(results[i].ImplementationType, compilation))
                    continue;

                skippedInternalHandlers?.Add(new SkippedHandlerInfo(
                    results[i].ImplementationType
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    results[i].ImplementationType.ContainingAssembly?.Name ?? "unknown"));

                results.RemoveAt(i);
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
        /// with public or internal accessibility. Internal types are included here
        /// so they can be discovered; actual accessibility from the consuming
        /// compilation is verified later in <see cref="GetAllExternalHandlers"/>.
        /// </summary>
        private static bool IsConcreteHandler(INamedTypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Class)
                return false;

            if (type.IsAbstract || type.IsGenericType)
                return false;

            // Include public and internal types. Internal types may be accessible
            // via [InternalsVisibleTo]; the post-filter in GetAllExternalHandlers
            // will remove those that are truly inaccessible.
            if (type.DeclaredAccessibility != Accessibility.Public
                && type.DeclaredAccessibility != Accessibility.Internal)
                return false;

            return true;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="type"/> is accessible from the
        /// assembly being compiled. Public types are always accessible. Internal types
        /// are accessible only when their containing assembly has
        /// <c>[InternalsVisibleTo]</c> pointing to the current compilation's assembly.
        /// </summary>
        private static bool IsAccessibleFromCompilation(
            INamedTypeSymbol type,
            Compilation compilation)
        {
            if (type.DeclaredAccessibility == Accessibility.Public)
                return true;

            if (type.DeclaredAccessibility != Accessibility.Internal
                && type.DeclaredAccessibility != Accessibility.ProtectedOrInternal)
                return false;

            // Check if the type's assembly grants [InternalsVisibleTo] to our assembly.
            var ourAssemblyName = compilation.AssemblyName;
            if (ourAssemblyName is null)
                return false;

            foreach (var attr in type.ContainingAssembly.GetAttributes())
            {
                if (attr.AttributeClass?.Name != "InternalsVisibleToAttribute"
                    && attr.AttributeClass?.Name != "InternalsVisibleTo")
                    continue;

                if (attr.ConstructorArguments.Length < 1
                    || attr.ConstructorArguments[0].Value is not string friendName)
                    continue;

                // The attribute value may contain a public key suffix:
                // "AssemblyName, PublicKey=00240000..."
                var comma = friendName.IndexOf(',');
                var name = comma >= 0
                    ? friendName.Substring(0, comma).Trim()
                    : friendName.Trim();

                if (string.Equals(name, ourAssemblyName, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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
        /// from referenced assemblies, along with information about internal handlers
        /// that were skipped because they are inaccessible from the consuming project.
        /// </summary>
        public static (List<(string ServiceType, string ImplementationType, bool IsStateless)> Handlers,
                        List<SkippedHandlerInfo> SkippedInternalHandlers) GetExternalDIHandlers(
            Compilation compilation)
        {
            var results = new List<(string, string, bool)>();
            var skippedInternals = new List<SkippedHandlerInfo>();

            foreach (var handler in GetAllExternalHandlers(compilation, skippedInternals))
            {
                var serviceType = handler.ServiceType
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var implType = handler.ImplementationType
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                bool isStateless = handler.ImplementationType.InstanceConstructors.Length > 0
                    && handler.ImplementationType.InstanceConstructors.All(static c => c.Parameters.IsEmpty);

                results.Add((serviceType, implType, isStateless));
            }

            return (results, skippedInternals);
        }

        internal readonly struct ExternalHandlerInfo(INamedTypeSymbol serviceType, INamedTypeSymbol implementationType)
        {
            public INamedTypeSymbol ServiceType { get; } = serviceType;
            public INamedTypeSymbol ImplementationType { get; } = implementationType;
        }

        /// <summary>
        /// Information about an internal handler that was discovered in an external
        /// assembly but skipped because it is not accessible from the consuming project.
        /// </summary>
        internal readonly struct SkippedHandlerInfo(
            string handlerType,
            string assemblyName) : System.IEquatable<SkippedHandlerInfo>
        {
            public string HandlerType { get; } = handlerType;
            public string AssemblyName { get; } = assemblyName;

            public bool Equals(SkippedHandlerInfo other) =>
                HandlerType == other.HandlerType && AssemblyName == other.AssemblyName;

            public override bool Equals(object obj) =>
                obj is SkippedHandlerInfo other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((HandlerType?.GetHashCode() ?? 0) * 397)
                         ^ (AssemblyName?.GetHashCode() ?? 0);
                }
            }
        }
    }
}
