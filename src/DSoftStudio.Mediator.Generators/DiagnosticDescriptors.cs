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

        public static readonly DiagnosticDescriptor InaccessibleHandlerSkipped = new(
            id: "DSOFT009",
            title: "Handler skipped: generated code cannot name it",
            messageFormat: "The {1} '{0}' was skipped because generated registration code cannot name it. "
                          + "To fix: make it (and every type enclosing it) at least internal, and do not nest it "
                          + "inside a generic type.",
            category: "DSoftStudio.Mediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Registration is generated into a separate file in the same assembly, so it can reach a "
                       + "public or internal type but not a private or protected nested one, and typeof(Outer<>.Inner) "
                       + "is not legal C# for a type nested in a generic. Such handlers are skipped rather than "
                       + "emitted, which would produce CS0122 errors in generated files. A skipped handler is not "
                       + "registered, so dispatching its request throws at runtime.");

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

        /// <summary>
        /// DSOFT010 — a pipeline component registered after the scan that decides whether a chain
        /// exists for it. Reported per registration block, on the same service collection.
        /// </summary>
        public static readonly DiagnosticDescriptor ComponentRegisteredAfterPrecompile = new(
            id: "DSOFT010",
            title: "Pipeline component registered after the mediator pipeline scan",
            messageFormat: "'{0}' registers a pipeline component after '{1}' has already scanned the "
                         + "service collection. The scan decides, per request type, whether a pipeline chain "
                         + "is built AT ALL, and freezes its lifetime; a component added afterwards may never "
                         + "run — silently. Move it before '{1}'.",
            category: "DSoftStudio.Mediator.Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "PrecompilePipelines() / PrecompileStreams() / AddMediator(builder => { }) inspect "
                        + "the IServiceCollection at the point they are called. If a request/response pair has "
                        + "no behavior, processor or exception handler registered by then, no pipeline chain is "
                        + "built for it and anything registered afterwards never runs — with no exception and no "
                        + "diagnostic at runtime. Where a chain does exist, the scan has already fixed its "
                        + "lifetime, so a Transient component registered later is constructed once and shared. "
                        + "Calling the scan a second time does not repair either case. This rule sees only "
                        + "registrations in the same method on the same collection; ValidateMediatorHandlers() "
                        + "catches the rest, including registrations in other methods and assemblies.");

        public static readonly DiagnosticDescriptor MissingHandlerRegistration = new(
            id: "DSOFT008",
            title: "AddMediator() registers core services but no handlers",
            messageFormat: "'AddMediator()' registers only the core services and leaves handlers unregistered. "
                         + "Use 'AddMediator(builder => { })' (recommended) or chain '.RegisterMediatorHandlers()'. "
                         + "Otherwise handler resolution throws at runtime (\"No service for type IRequestHandler<...>\").",
            category: "DSoftStudio.Mediator.Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The parameterless AddMediator() overload registers only the core mediator services "
                        + "(IMediator / ISender / IPublisher). It does not register request, notification, or "
                        + "stream handlers. When handlers exist in the compilation (locally or in referenced "
                        + "assemblies) but neither AddMediator(Action<MediatorBuilder>) nor "
                        + "RegisterMediatorHandlers() is called, those handlers are never added to DI and the "
                        + "first dispatch fails at runtime. Use the builder overload (single entry point) or "
                        + "call RegisterMediatorHandlers() explicitly.",
            // Registration can live in a different method than AddMediator(), so the analyzer can only decide
            // this once the WHOLE compilation has been seen — it reports from a CompilationEndAction. The tag
            // tells the host to schedule it as a full-compilation diagnostic (not a live per-keystroke one).
            customTags: WellKnownDiagnosticTags.CompilationEnd);
    }
}
