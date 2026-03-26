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

        public static readonly DiagnosticDescriptor InternalHandlerSkipped = new(
            id: "DSOFT005",
            title: "Internal handler in external assembly skipped",
            messageFormat: "Handler '{0}' in assembly '{1}' is internal and cannot be registered from this project. "
                          + "To fix: make the handler public, add [InternalsVisibleTo] to the handler's project, "
                          + "or add the source generator to the handler's project so it self-registers.",
            category: "DSoftStudio.Mediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The source generator discovered a handler in a referenced assembly, but the handler class "
                        + "is internal and not visible to this project. The generated DI registration code cannot "
                        + "reference internal types across assembly boundaries (CS0122). The handler will be silently "
                        + "skipped. To register it, either make the handler public, add [InternalsVisibleTo] from the "
                        + "handler's project to this project, or ensure the handler's project also references the "
                        + "source generator so it emits its own registration code.");

        public static readonly DiagnosticDescriptor PreferCqrsInterface = new(
            id: "DSOFT006",
            title: "Consider using ICommand<T> or IQuery<T> instead of IRequest<T>",
            messageFormat: "Type '{0}' implements IRequest<{1}> directly. Consider using ICommand<{1}> (write) or IQuery<{1}> (read) for CQRS semantic clarity — zero runtime cost.",
            category: "DSoftStudio.Mediator.Usage",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "ICommand<T> and IQuery<T> extend IRequest<T> with zero overhead. "
                        + "Using them gives pipeline behaviors a runtime type check "
                        + "(if request is ICommand / is IQuery) for cross-cutting concerns "
                        + "and makes intent explicit at the type level.");

        public static readonly DiagnosticDescriptor MixedRegistrationApi = new(
            id: "DSOFT007",
            title: "Redundant mediator registration call",
            messageFormat: "'{0}' should not be called when using 'AddMediator(Action<MediatorBuilder>)'. "
                         + "The builder overload {1} automatically.",
            category: "DSoftStudio.Mediator.Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "AddMediator(Action<MediatorBuilder>) is a single entry point that registers "
                        + "core services, handlers, and precompiled pipelines in one call. "
                        + "Calling RegisterMediatorHandlers() or PrecompilePipelines() separately "
                        + "when using the builder overload causes double registration. "
                        + "Use either the builder overload (recommended) or the individual methods, "
                        + "but not both.");
    }
}
