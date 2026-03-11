// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;

namespace DSoftStudio.Mediator.Generators
{
    internal static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor NoHandlerForRequest = new(
            id: "DSOFT001",
            title: "No handler found for request type",
            messageFormat: "No IRequestHandler<{0}, {1}> implementation found for request type '{0}'",
            category: "DSoftStudio.Mediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Every request type implementing IRequest<TResponse> should have a corresponding IRequestHandler<TRequest, TResponse> implementation.");
    }
}
