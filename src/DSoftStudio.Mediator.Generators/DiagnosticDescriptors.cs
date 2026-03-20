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

        public static readonly DiagnosticDescriptor DuplicateRequestHandler = new(
            id: "DSOFT002",
            title: "Duplicate request handler registration",
            messageFormat: "Multiple handlers found for '{0}': {1}. Only the last registered handler will execute; the others will be silently ignored.",
            category: "DSoftStudio.Mediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Each request type should have exactly one IRequestHandler<TRequest, TResponse> implementation. When multiple handlers are registered for the same request type, Microsoft.Extensions.DI resolves only the last registration via GetRequiredService<T>(), silently ignoring the others.");

        public static readonly DiagnosticDescriptor DuplicateStreamHandler = new(
            id: "DSOFT003",
            title: "Duplicate stream handler registration",
            messageFormat: "Multiple handlers found for '{0}': {1}. Only the last registered handler will execute; the others will be silently ignored.",
            category: "DSoftStudio.Mediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Each stream request type should have exactly one IStreamRequestHandler<TRequest, TResponse> implementation. When multiple handlers are registered for the same stream request type, Microsoft.Extensions.DI resolves only the last registration via GetRequiredService<T>(), silently ignoring the others.");

        public static readonly DiagnosticDescriptor MockingWithInterceptorsInRelease = new(
            id: "DSOFT004",
            title: "Mocking library detected with interceptors enabled",
            messageFormat: "This project references mocking library '{0}' and has interceptors enabled. "
                         + "In Release builds, interceptors use a branchless cast that throws InvalidCastException "
                         + "on mock objects. Either reference only DSoftStudio.Mediator.Abstractions in test projects, "
                         + "or set <DSoftMediatorSuppressInterceptors>true</DSoftMediatorSuppressInterceptors> in this project.",
            category: "DSoftStudio.Mediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Test projects that mock ISender/IPublisher/IMediator should not have interceptors enabled. "
                       + "In Release builds, the generated interceptors cast to IServiceProviderAccessor without a type check, "
                       + "causing InvalidCastException when the sender is a mock object. "
                       + "Reference DSoftStudio.Mediator.Abstractions instead of DSoftStudio.Mediator in test projects, "
                       + "or suppress interceptors with the DSoftMediatorSuppressInterceptors MSBuild property.");
    }
}
