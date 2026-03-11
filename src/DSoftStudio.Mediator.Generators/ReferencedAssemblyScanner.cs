// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace DSoftStudio.Mediator.Generators
{
    /// <summary>
    /// Reads <c>[assembly: MediatorHandlerRegistration(...)]</c> attributes from
    /// referenced assemblies so that generators in downstream projects can discover
    /// handlers declared in upstream projects — without runtime reflection.
    /// </summary>
    internal static class ReferencedAssemblyScanner
    {
        private const string AttributeFullName =
            "DSoftStudio.Mediator.Abstractions.MediatorHandlerRegistrationAttribute";

        private const string RequestHandlerMetadataName = "IRequestHandler`2";
        private const string NotificationHandlerMetadataName = "INotificationHandler`1";
        private const string StreamHandlerMetadataName = "IStreamRequestHandler`2";

        private const string AbstractionsNamespace = "DSoftStudio.Mediator.Abstractions";

        /// <summary>
        /// Returns all (ServiceType, ImplementationType) pairs found in
        /// <c>[assembly: MediatorHandlerRegistration]</c> attributes on referenced assemblies.
        /// </summary>
        public static List<ExternalHandlerInfo> GetAllExternalHandlers(Compilation compilation)
        {
            var results = new List<ExternalHandlerInfo>();

            var attrType = compilation.GetTypeByMetadataName(AttributeFullName);
            if (attrType is null)
                return results;

            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                    continue;

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

            return results;
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
    }

    internal readonly struct ExternalHandlerInfo
    {
        public INamedTypeSymbol ServiceType { get; }
        public INamedTypeSymbol ImplementationType { get; }

        public ExternalHandlerInfo(INamedTypeSymbol serviceType, INamedTypeSymbol implementationType)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
        }
    }
}
