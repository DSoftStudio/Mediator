// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

namespace DSoftStudio.Mediator.Generators
{
    internal static class HandlerDiscovery
    {
        private const string RequestHandlerMetadataName =
            "DSoftStudio.Mediator.Abstractions.IRequestHandler`2";

        private const string NotificationHandlerMetadataName =
            "DSoftStudio.Mediator.Abstractions.INotificationHandler`1";

        private const string StreamHandlerMetadataName =
            "DSoftStudio.Mediator.Abstractions.IStreamRequestHandler`2";


        public static bool TryGetRequestHandler(
            INamedTypeSymbol symbol,
            CancellationToken ct,
            out string requestType,
            out string responseType)
        {
            requestType = string.Empty;
            responseType = string.Empty;    
            foreach (var iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();

                if (!IsTargetInterface(iface, RequestHandlerMetadataName))
                    continue;

                requestType = iface.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                responseType = iface.TypeArguments[1]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                return true;
            }

            return false;
        }


        public static bool TryGetNotificationHandler(
            INamedTypeSymbol symbol,
            CancellationToken ct,
            out string notificationType,
            out string handlerType)
        {
            notificationType = string.Empty;
            handlerType = string.Empty;

            foreach (var iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();

                if (!IsTargetInterface(iface, NotificationHandlerMetadataName))
                    continue;

                notificationType = iface.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                handlerType = symbol
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                return true;
            }

            return false;
        }


        public static bool TryGetStreamHandler(
            INamedTypeSymbol symbol,
            CancellationToken ct,
            out string requestType,
            out string responseType,
            out string handlerType)
        {
            requestType = string.Empty;
            responseType = string.Empty;
            handlerType = string.Empty;

            foreach (var iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();

                if (!IsTargetInterface(iface, StreamHandlerMetadataName))
                    continue;

                requestType = iface.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                responseType = iface.TypeArguments[1]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                handlerType = symbol
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                return true;
            }

            return false;
        }


        /// <summary>
        /// Returns <c>true</c> when the class has the <c>file</c> modifier (C# 11+).
        /// File-local types cannot be referenced from generated code and must be skipped.
        /// </summary>
        public static bool IsFileLocal(ClassDeclarationSyntax classDecl)
        {
            foreach (var modifier in classDecl.Modifiers)
            {
                if (modifier.Text == "file")
                    return true;
            }
            return false;
        }

        private static bool IsTargetInterface(
            INamedTypeSymbol iface,
            string metadataName)
        {
            var original = iface.OriginalDefinition;
            var qualifiedName = original.ContainingNamespace.ToDisplayString()
                + "." + original.MetadataName;
            return qualifiedName == metadataName;
        }
    }
}
