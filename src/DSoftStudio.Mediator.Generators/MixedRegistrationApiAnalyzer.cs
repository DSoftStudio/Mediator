// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Validates how the mediator registration APIs are used. DSOFT007 (mixing APIs) is checked per
/// registration scope (method body); DSOFT008 (handlers never registered) is checked across the whole
/// compilation, since the registration that backs <c>AddMediator()</c> often lives in another method.
/// <para>
/// <c>AddMediator(Action&lt;MediatorBuilder&gt;)</c> is a single entry point that registers core
/// services, handlers, and precompiled pipelines in one call. Calling
/// <c>RegisterMediatorHandlers()</c> or <c>PrecompilePipelines()</c> alongside it is redundant.
/// The parameterless <c>AddMediator()</c> registers only the core services; handlers must then be
/// registered with <c>RegisterMediatorHandlers()</c> (or manually) or the build will fail at
/// runtime on the first dispatch.
/// </para>
/// <para>
/// Emits:
/// <list type="bullet">
///   <item><c>DSOFT007</c> — the builder overload is used together with the individual registration
///   methods in the same scope (redundant / double registration).</item>
///   <item><c>DSOFT008</c> — the parameterless <c>AddMediator()</c> is used while NOTHING in the whole
///   compilation registers handlers (no builder overload, no <c>RegisterMediatorHandlers()</c>, and no
///   manual <c>AddTransient&lt;IRequestHandler&lt;,&gt;,…&gt;()</c>), yet handlers exist — they are left
///   unregistered. Compilation-wide so a split across methods is not a false positive.</item>
/// </list>
/// </para>
/// <para>
/// This is a <see cref="DiagnosticAnalyzer"/> (not a source generator) on purpose: the builder
/// overload, <c>RegisterMediatorHandlers()</c>, and <c>PrecompilePipelines()</c> are emitted by
/// sibling source generators. A generator cannot see another generator's output, so it could not
/// resolve those calls. An analyzer runs after all generators, on the final compilation, so the
/// semantic model resolves the generated members correctly.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MixedRegistrationApiAnalyzer : DiagnosticAnalyzer
{
    private const string MediatorNamespacePrefix = "DSoftStudio.Mediator";

    private const string MediatorHandlerRegistrationAttributeFullName =
        "DSoftStudio.Mediator.Abstractions.MediatorHandlerRegistrationAttribute";

    private const string RequestHandlerMetadataName =
        "DSoftStudio.Mediator.Abstractions.IRequestHandler`2";
    private const string NotificationHandlerMetadataName =
        "DSoftStudio.Mediator.Abstractions.INotificationHandler`1";
    private const string StreamHandlerMetadataName =
        "DSoftStudio.Mediator.Abstractions.IStreamRequestHandler`2";
    private const string RequestMetadataName =
        "DSoftStudio.Mediator.Abstractions.IRequest`1";

    /// <summary>The pipeline components whose registration order decides whether a chain exists.</summary>
    private static readonly string[] ComponentMetadataNames =
    [
        "DSoftStudio.Mediator.Abstractions.IPipelineBehavior`2",
        "DSoftStudio.Mediator.Abstractions.IRequestPreProcessor`1",
        "DSoftStudio.Mediator.Abstractions.IRequestPostProcessor`2",
        "DSoftStudio.Mediator.Abstractions.IRequestExceptionHandler`2",
        "DSoftStudio.Mediator.Abstractions.IStreamPipelineBehavior`2",
        "DSoftStudio.Mediator.Abstractions.IMediatorDispatchObserver",
    ];

    /// <summary><c>MediatorBuilder</c> methods that register a pipeline component.</summary>
    private static readonly string[] BuilderComponentMethods =
    [
        "AddBehavior", "AddOpenBehavior", "AddStreamBehavior", "AddOpenStreamBehavior",
        "AddRequestPreProcessor", "AddRequestPostProcessor", "AddRequestExceptionHandler",
        "AddDispatchObserver",
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DiagnosticDescriptors.MixedRegistrationApi,
            DiagnosticDescriptors.MissingHandlerRegistration,
            DiagnosticDescriptors.ComponentRegisteredAfterPrecompile);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var compilation = compilationStart.Compilation;

            var requestHandler = compilation.GetTypeByMetadataName(RequestHandlerMetadataName);
            var notificationHandler = compilation.GetTypeByMetadataName(NotificationHandlerMetadataName);
            var streamHandler = compilation.GetTypeByMetadataName(StreamHandlerMetadataName);

            // Mediator abstractions not referenced → nothing this analyzer can flag.
            if (requestHandler is null && notificationHandler is null && streamHandler is null)
                return;

            // Computed once per compilation: are there handlers that need registering?
            // DSOFT008 only matters when there are.
            bool hasHandlers = CompilationHasHandlers(
                compilation, requestHandler, notificationHandler, streamHandler,
                compilationStart.CancellationToken);

            // ── DSOFT008 is a COMPILATION-WIDE property ───────────────────────────────────────────────
            // "Are the handlers registered anywhere in startup?" can only be answered for the whole
            // compilation: AddMediator() and the registration that backs it (RegisterMediatorHandlers(),
            // the builder overload, or manual AddTransient<IRequestHandler<,>>) routinely live in different
            // methods/files. A per-scope check false-positives that split — and DSOFT008 is a Warning, so a
            // false positive breaks builds under TreatWarningsAsErrors. We therefore accumulate the parameterless
            // AddMediator() sites and a single "registers handlers somewhere" flag across the whole compilation
            // (block actions run concurrently → thread-safe state), then decide in RegisterCompilationEndAction
            // once every method has been seen. DSOFT007 (mixing APIs) stays per-scope: mixing is by definition
            // within one registration block.
            var unregisteredAddMediatorSites = new ConcurrentBag<Location>();
            int registersHandlersSomewhere = 0; // set-once via Interlocked from concurrent block actions

            var componentTypes = new List<INamedTypeSymbol>(ComponentMetadataNames.Length);
            foreach (var metadataName in ComponentMetadataNames)
            {
                var symbol = compilation.GetTypeByMetadataName(metadataName);
                if (symbol is not null)
                    componentTypes.Add(symbol);
            }

            compilationStart.RegisterOperationBlockStartAction(blockStart =>
            {
                // Per-scope (method body) state — DSOFT007 only. Each block gets its own closure instance.
                var gate = new object();
                var redundantCalls = new List<(Location Location, string Method, string Action)>();
                bool hasBuilderOverload = false;

                // DSOFT010 (per-scope): where the pipeline scans happen, and where components get
                // registered, so the end action can pair them up by position on the same collection.
                var scanPoints = new List<(int End, string Method, ISymbol? Receiver)>();
                var componentAdds = new List<(Location Location, int Start, string Method, ISymbol? Receiver)>();

                blockStart.RegisterOperationAction(opContext =>
                {
                    var invocation = (IInvocationOperation)opContext.Operation;
                    var method = invocation.TargetMethod;
                    if (method is null)
                        return;

                    // Manual handler registration — e.g. services.AddTransient<IRequestHandler<X,Y>, H>()
                    // or services.AddSingleton<INotificationHandler<N>>(instance) — means handlers ARE
                    // registered (somewhere in the compilation), so DSOFT008 must not fire.
                    if (IsManualHandlerRegistration(method, requestHandler, notificationHandler, streamHandler))
                    {
                        Interlocked.Exchange(ref registersHandlersSomewhere, 1);
                        return;
                    }

                    // ── DSOFT010 probes ────────────────────────────────────────────────────────────
                    // These sit ABOVE the mediator-namespace gate below, like the manual-handler probe
                    // and for the same reason: the registrations this rule exists to catch —
                    // services.AddTransient(typeof(IPipelineBehavior<,>), typeof(X<,>)) and friends — are
                    // Microsoft.Extensions.DependencyInjection methods and would never reach the switch.
                    if (IsPipelineScan(method))
                    {
                        // The CONTAINING STATEMENT is the boundary, not the invocation: that keeps a fluent
                        // chain (.RegisterMediatorHandlers().PrecompilePipelines()) and the
                        // AddMediator(configure) lambda body on the "before" side, where they belong.
                        var statement = invocation.Syntax.FirstAncestorOrSelf<StatementSyntax>() ?? invocation.Syntax;
                        lock (gate)
                            scanPoints.Add((statement.Span.End, method.Name + "()", ReceiverSymbol(invocation)));
                    }
                    else if (IsComponentRegistration(invocation, method, componentTypes))
                    {
                        lock (gate)
                            componentAdds.Add((invocation.Syntax.GetLocation(), invocation.Syntax.SpanStart,
                                               method.Name + "()", ReceiverSymbol(invocation)));
                    }

                    // Only the mediator's own registration methods (avoids matching an unrelated
                    // method that happens to share a name).
                    var ns = method.ContainingNamespace?.ToDisplayString();
                    if (ns is null || !ns.StartsWith(MediatorNamespacePrefix, StringComparison.Ordinal))
                        return;

                    var location = invocation.Syntax.GetLocation();

                    switch (method.Name)
                    {
                        case "AddMediator":
                            if (HasMediatorBuilderParameter(method))
                            {
                                lock (gate) { hasBuilderOverload = true; }            // DSOFT007 (per-scope)
                                Interlocked.Exchange(ref registersHandlersSomewhere, 1); // registers handlers
                            }
                            else
                            {
                                unregisteredAddMediatorSites.Add(location);          // DSOFT008 (compilation-wide)
                            }
                            break;

                        case "RegisterMediatorHandlers":
                            Interlocked.Exchange(ref registersHandlersSomewhere, 1);  // registers handlers
                            lock (gate)
                                redundantCalls.Add((location, "RegisterMediatorHandlers()", "registers handlers"));
                            break;

                        case "PrecompilePipelines":
                            lock (gate)
                                redundantCalls.Add((location, "PrecompilePipelines()", "precompiles pipelines"));
                            break;
                    }
                }, OperationKind.Invocation);

                // ── DSOFT007: redundant individual call alongside the builder overload (same scope) ──
                blockStart.RegisterOperationBlockEndAction(blockEnd =>
                {
                    if (hasBuilderOverload)
                    {
                        foreach (var (location, method, action) in redundantCalls)
                            blockEnd.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.MixedRegistrationApi, location, method, action));
                    }

                    // ── DSOFT010: a component registered after a scan on the SAME collection ──
                    // A later scan is deliberately NOT treated as a repair: a second PrecompilePipelines()
                    // returns early on its sentinel and changes nothing, so the registration is still late.
                    foreach (var add in componentAdds)
                    {
                        if (add.Receiver is null)
                            continue;

                        foreach (var scan in scanPoints)
                        {
                            if (scan.Receiver is null || add.Start <= scan.End)
                                continue;

                            if (!SymbolEqualityComparer.Default.Equals(add.Receiver, scan.Receiver))
                                continue;

                            blockEnd.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.ComponentRegisteredAfterPrecompile,
                                add.Location, add.Method, scan.Method));
                            break;
                        }
                    }
                });
            });

            // ── DSOFT008: decided once the whole compilation has been analyzed ──
            // Fire only when handlers exist AND nothing anywhere registers them — every parameterless
            // AddMediator() site is then genuinely leaving handlers unregistered.
            compilationStart.RegisterCompilationEndAction(compilationEnd =>
            {
                if (!hasHandlers || Volatile.Read(ref registersHandlersSomewhere) != 0)
                    return;

                foreach (var location in unregisteredAddMediatorSites)
                    compilationEnd.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MissingHandlerRegistration, location));
            });
        });
    }

    /// <summary>
    /// True for the calls that snapshot the <c>IServiceCollection</c>: the three <c>Precompile*</c>
    /// methods and the <c>AddMediator(Action&lt;MediatorBuilder&gt;)</c> overload, which ends by
    /// precompiling.
    /// </summary>
    private static bool IsPipelineScan(IMethodSymbol method)
        => method.Name is "PrecompilePipelines" or "PrecompileStreams" or "PrecompileNotifications"
           || (method.Name == "AddMediator" && HasMediatorBuilderParameter(method));

    /// <summary>
    /// True when the invocation registers a pipeline component. Three shapes are recognised, and
    /// anything else is left alone — DSOFT010 is a Warning, and users build with
    /// <c>TreatWarningsAsErrors</c>, so a miss is far cheaper than a false positive.
    /// </summary>
    private static bool IsComponentRegistration(
        IInvocationOperation invocation, IMethodSymbol method, List<INamedTypeSymbol> componentTypes)
    {
        // (a) A MediatorBuilder method that registers a component.
        if (method.ContainingType is { Name: "MediatorBuilder" })
        {
            foreach (var name in BuilderComponentMethods)
                if (method.Name == name)
                    return true;
        }

        // (b) A generic Add*/TryAdd* whose type arguments name a component interface, e.g.
        //     services.AddTransient<IPipelineBehavior<Ping, int>, LoggingBehavior>().
        if (method.Name.StartsWith("Add", StringComparison.Ordinal)
            || method.Name.StartsWith("TryAdd", StringComparison.Ordinal))
        {
            foreach (var argument in method.TypeArguments)
                if (MatchesComponent(argument, componentTypes))
                    return true;
        }

        // (c) Any typeof(...) argument naming a component interface — covers the open-generic form
        //     services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Logging<,>)) and
        //     services.Add(new ServiceDescriptor(typeof(IPipelineBehavior<,>), ...)).
        foreach (var argument in invocation.Arguments)
            if (ContainsComponentTypeOf(argument.Value, componentTypes))
                return true;

        return false;
    }

    private static bool ContainsComponentTypeOf(IOperation? operation, List<INamedTypeSymbol> componentTypes)
    {
        switch (operation)
        {
            case null:
                return false;
            case ITypeOfOperation typeOf:
                return MatchesComponent(typeOf.TypeOperand, componentTypes);
            // new ServiceDescriptor(typeof(IPipelineBehavior<,>), ...) reaches us as the argument.
            case IObjectCreationOperation creation:
                foreach (var argument in creation.Arguments)
                    if (ContainsComponentTypeOf(argument.Value, componentTypes))
                        return true;
                return false;
            case IConversionOperation conversion:
                return ContainsComponentTypeOf(conversion.Operand, componentTypes);
            default:
                return false;
        }
    }

    private static bool MatchesComponent(ITypeSymbol? type, List<INamedTypeSymbol> componentTypes)
    {
        if (type is not INamedTypeSymbol named)
            return false;

        var definition = named.OriginalDefinition;
        foreach (var component in componentTypes)
            if (SymbolEqualityComparer.Default.Equals(definition, component))
                return true;

        return false;
    }

    /// <summary>
    /// The <c>IServiceCollection</c> an invocation acts on, so DSOFT010 only pairs a registration with
    /// a scan of the SAME collection. Returns <c>null</c> when it cannot be resolved to a symbol, and
    /// the rule then stays silent rather than guessing.
    /// </summary>
    private static ISymbol? ReceiverSymbol(IInvocationOperation invocation)
    {
        var receiver = invocation.Instance
                       ?? (invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null);

        while (receiver is IConversionOperation conversion)
            receiver = conversion.Operand;

        return receiver switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IPropertyReferenceOperation property => property.Property, // builder.Services
            IFieldReferenceOperation field => field.Field,
            _ => null,
        };
    }

    /// <summary>
    /// Checks whether the method has an <c>Action&lt;MediatorBuilder&gt;</c> parameter,
    /// identifying the builder overload of <c>AddMediator</c>. Works for both the reduced
    /// extension-method form and the static invocation form.
    /// </summary>
    private static bool HasMediatorBuilderParameter(IMethodSymbol method)
    {
        foreach (var param in method.Parameters)
        {
            if (param.Type is INamedTypeSymbol { Name: "Action", TypeArguments.Length: 1 } actionType
                && actionType.TypeArguments[0].Name == "MediatorBuilder")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects a manual DI registration of a mediator handler, e.g.
    /// <c>services.AddTransient&lt;IRequestHandler&lt;X,Y&gt;, H&gt;()</c> or
    /// <c>services.AddSingleton&lt;INotificationHandler&lt;N&gt;&gt;(instance)</c>. Such a call
    /// registers handlers without <c>RegisterMediatorHandlers()</c>, so DSOFT008 must not fire.
    /// </summary>
    private static bool IsManualHandlerRegistration(
        IMethodSymbol method,
        INamedTypeSymbol? requestHandler,
        INamedTypeSymbol? notificationHandler,
        INamedTypeSymbol? streamHandler)
    {
        switch (method.Name)
        {
            case "AddSingleton":
            case "AddTransient":
            case "AddScoped":
            case "TryAddSingleton":
            case "TryAddTransient":
            case "TryAddScoped":
                break;
            default:
                return false;
        }

        foreach (var typeArg in method.TypeArguments)
        {
            if (typeArg is INamedTypeSymbol named && IsHandlerInterface(
                    named.OriginalDefinition, requestHandler, notificationHandler, streamHandler))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHandlerInterface(
        ITypeSymbol definition,
        INamedTypeSymbol? requestHandler,
        INamedTypeSymbol? notificationHandler,
        INamedTypeSymbol? streamHandler)
        => (requestHandler is not null && SymbolEqualityComparer.Default.Equals(definition, requestHandler))
        || (notificationHandler is not null && SymbolEqualityComparer.Default.Equals(definition, notificationHandler))
        || (streamHandler is not null && SymbolEqualityComparer.Default.Equals(definition, streamHandler));

    // ── Handler-existence detection (compilation-wide, for DSOFT008) ──────────────

    private static bool CompilationHasHandlers(
        Compilation compilation,
        INamedTypeSymbol? requestHandler,
        INamedTypeSymbol? notificationHandler,
        INamedTypeSymbol? streamHandler,
        CancellationToken ct)
    {
        // Fast path: the DI generator emits [assembly: MediatorHandlerRegistration] for every
        // local handler / self-handler. Present in any real build where handlers exist.
        var attribute = compilation.GetTypeByMetadataName(MediatorHandlerRegistrationAttributeFullName);
        if (attribute is not null)
        {
            foreach (var attr in compilation.Assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attribute))
                    return true;
            }
        }

        // Handlers contributed by referenced assemblies (clean-architecture / modular setups).
        if (ReferencedAssemblyScanner.GetExternalDIHandlers(compilation).Handlers.Count > 0)
            return true;

        // Fallback: scan source types directly — covers the case where the DI generator did not
        // run (e.g. analyzer-only unit tests, or the generator suppressed).
        var request = compilation.GetTypeByMetadataName(RequestMetadataName);
        return ContainsHandler(
            compilation.Assembly.GlobalNamespace,
            requestHandler, notificationHandler, streamHandler, request, ct);
    }

    private static bool ContainsHandler(
        INamespaceSymbol ns,
        INamedTypeSymbol? requestHandler,
        INamedTypeSymbol? notificationHandler,
        INamedTypeSymbol? streamHandler,
        INamedTypeSymbol? request,
        CancellationToken ct)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (TypeOrNestedIsHandler(type, requestHandler, notificationHandler, streamHandler, request, ct))
                return true;
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            if (ContainsHandler(child, requestHandler, notificationHandler, streamHandler, request, ct))
                return true;
        }

        return false;
    }

    private static bool TypeOrNestedIsHandler(
        INamedTypeSymbol type,
        INamedTypeSymbol? requestHandler,
        INamedTypeSymbol? notificationHandler,
        INamedTypeSymbol? streamHandler,
        INamedTypeSymbol? request,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (IsConcreteHandlerType(type, requestHandler, notificationHandler, streamHandler, request, ct))
            return true;

        foreach (var nested in type.GetTypeMembers())
        {
            if (TypeOrNestedIsHandler(nested, requestHandler, notificationHandler, streamHandler, request, ct))
                return true;
        }

        return false;
    }

    private static bool IsConcreteHandlerType(
        INamedTypeSymbol type,
        INamedTypeSymbol? requestHandler,
        INamedTypeSymbol? notificationHandler,
        INamedTypeSymbol? streamHandler,
        INamedTypeSymbol? request,
        CancellationToken ct)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract)
            return false;

        foreach (var iface in type.AllInterfaces)
        {
            if (IsHandlerInterface(iface.OriginalDefinition, requestHandler, notificationHandler, streamHandler))
                return true;
        }

        // Self-handling request type (IRequest<T> + static Execute), registered as an adapter.
        if (request is not null && HandlerDiscovery.TryGetSelfHandlingRequest(type, ct, out _))
            return true;

        return false;
    }
}
