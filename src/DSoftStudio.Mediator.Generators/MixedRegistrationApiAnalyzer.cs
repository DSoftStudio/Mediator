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

    /// <summary>
    /// Dot-terminated, for matches that must not also accept a namespace merely STARTING with the
    /// prefix — "DSoftStudio.MediatorPlus" is somebody else's code.
    /// </summary>
    private const string MediatorNamespaceDotted = "DSoftStudio.Mediator.";

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

    /// <summary>
    /// Which scan governs a component. A pipeline component is late only against the scan that would
    /// have built ITS chain: a stream behavior registered after <c>PrecompileNotifications()</c> but
    /// before <c>PrecompileStreams()</c> is correctly ordered, and reporting it would be a false
    /// positive in a rule users build with <c>TreatWarningsAsErrors</c>. Nothing in
    /// <see cref="ComponentMetadataNames"/> is a notification component, so
    /// <c>PrecompileNotifications()</c> governs none and pairs with none.
    /// </summary>
    [Flags]
    private enum PipelineKind
    {
        None = 0,
        Request = 1,
        Stream = 2,
    }

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

    /// <summary>
    /// First-party companion packages that register a pipeline component from INSIDE their own
    /// extension method. Nothing in the call's argument list names a component interface — the
    /// descriptor is added in another assembly — so the only way to see these is to know the methods
    /// by name. Without them, the order these packages document ("before PrecompilePipelines()") is
    /// enforced by nothing, and getting it wrong is a silent no-op: no chain is built, the behavior
    /// never runs, and the application starts and serves traffic unvalidated or uncached.
    /// </summary>
    private static readonly (string ContainingType, string Method, PipelineKind Kind)[] FirstPartyComponentMethods =
    [
        ("FluentValidationServiceCollectionExtensions", "AddMediatorFluentValidation", PipelineKind.Request),
        ("HybridCacheServiceCollectionExtensions", "AddMediatorHybridCache", PipelineKind.Request),
        // Instrumentation registers a request behavior, a stream behavior AND a dispatch observer, so it
        // is late against either scan.
        ("OpenTelemetryServiceCollectionExtensions", "AddMediatorInstrumentation",
            PipelineKind.Request | PipelineKind.Stream),
    ];

    /// <summary><c>MediatorBuilder</c> methods that register a pipeline component.</summary>
    private static readonly (string Method, PipelineKind Kind)[] BuilderComponentMethods =
    [
        ("AddBehavior", PipelineKind.Request),
        ("AddOpenBehavior", PipelineKind.Request),
        ("AddStreamBehavior", PipelineKind.Stream),
        ("AddOpenStreamBehavior", PipelineKind.Stream),
        ("AddRequestPreProcessor", PipelineKind.Request),
        ("AddRequestPostProcessor", PipelineKind.Request),
        ("AddRequestExceptionHandler", PipelineKind.Request),
        // An observer lives inside the request chain, so the request scan is the one it can miss.
        ("AddDispatchObserver", PipelineKind.Request),
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
                var scanPoints = new List<(int End, string Method, ISymbol? Receiver, PipelineKind Kind)>();
                var componentAdds =
                    new List<(Location Location, int Start, string Method, ISymbol? Receiver, PipelineKind Kind)>();

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
                            scanPoints.Add((statement.Span.End, method.Name + "()",
                                            ReceiverSymbol(invocation), ScanKind(method)));
                    }
                    else if (ComponentRegistrationKind(invocation, method, componentTypes) is var componentKind
                             && componentKind != PipelineKind.None)
                    {
                        lock (gate)
                            componentAdds.Add((invocation.Syntax.GetLocation(), invocation.Syntax.SpanStart,
                                               method.Name + "()", ReceiverSymbol(invocation), componentKind));
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

                            // The scan has to be the one that would have built THIS component's chain.
                            if ((add.Kind & scan.Kind) == PipelineKind.None)
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
    /// Which chains a scan freezes. <c>AddMediator(configure)</c> ends by precompiling everything, so it
    /// governs both. <c>PrecompileNotifications()</c> governs neither: no pipeline component belongs to a
    /// notification, so a component that follows it is not late for anything.
    /// </summary>
    private static PipelineKind ScanKind(IMethodSymbol method)
        => method.Name switch
        {
            "PrecompilePipelines" => PipelineKind.Request,
            "PrecompileStreams" => PipelineKind.Stream,
            "AddMediator" => PipelineKind.Request | PipelineKind.Stream,
            _ => PipelineKind.None,
        };

    /// <summary>
    /// True when the invocation registers a pipeline component. Three shapes are recognised, and
    /// anything else is left alone — DSOFT010 is a Warning, and users build with
    /// <c>TreatWarningsAsErrors</c>, so a miss is far cheaper than a false positive.
    /// </summary>
    private static PipelineKind ComponentRegistrationKind(
        IInvocationOperation invocation, IMethodSymbol method, List<INamedTypeSymbol> componentTypes)
    {
        // (a) A MediatorBuilder method that registers a component.
        if (method.ContainingType is { Name: "MediatorBuilder" })
        {
            foreach (var (name, kind) in BuilderComponentMethods)
                if (method.Name == name)
                    return kind;
        }

        // (a2) A first-party companion extension method. Gated on the dot-terminated namespace so a
        //      same-named method in anybody else's code cannot trip a rule that users build as an error.
        if (method.ContainingNamespace?.ToDisplayString() is { } ns
            && ns.StartsWith(MediatorNamespaceDotted, StringComparison.Ordinal))
        {
            var containing = method.ContainingType?.Name;
            foreach (var (type, name, kind) in FirstPartyComponentMethods)
                if (containing == type && method.Name == name)
                    return kind;
        }

        // (b) A generic Add*/TryAdd* whose type arguments name a component interface, e.g.
        //     services.AddTransient<IPipelineBehavior<Ping, int>, LoggingBehavior>().
        if (method.Name.StartsWith("Add", StringComparison.Ordinal)
            || method.Name.StartsWith("TryAdd", StringComparison.Ordinal))
        {
            foreach (var argument in method.TypeArguments)
                if (ComponentKind(argument, componentTypes) is var kind && kind != PipelineKind.None)
                    return kind;
        }

        // (c) Any typeof(...) argument naming a component interface — covers the open-generic form
        //     services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Logging<,>)) and
        //     services.Add(new ServiceDescriptor(typeof(IPipelineBehavior<,>), ...)).
        var found = PipelineKind.None;
        foreach (var argument in invocation.Arguments)
            found |= ContainsComponentKind(argument.Value, componentTypes);

        return found;
    }

    private static PipelineKind ContainsComponentKind(IOperation? operation, List<INamedTypeSymbol> componentTypes)
    {
        switch (operation)
        {
            case null:
                return PipelineKind.None;
            case ITypeOfOperation typeOf:
                return ComponentKind(typeOf.TypeOperand, componentTypes);
            // ServiceDescriptor.Singleton(typeof(IPipelineBehavior<,>), typeof(X<,>)) and its generic
            // form — the shape TryAddEnumerable takes, and the one the first-party packages themselves
            // now use. Deliberately restricted to ServiceDescriptor's own factories: recursing into ANY
            // invocation would start matching helper calls and lambdas that merely MENTION the
            // interface, and a false positive here breaks a build.
            case IInvocationOperation descriptorFactory
                when descriptorFactory.TargetMethod.ContainingType?.Name == "ServiceDescriptor":
                var fromFactory = PipelineKind.None;
                foreach (var typeArgument in descriptorFactory.TargetMethod.TypeArguments)
                    fromFactory |= ComponentKind(typeArgument, componentTypes);

                foreach (var argument in descriptorFactory.Arguments)
                    fromFactory |= ContainsComponentKind(argument.Value, componentTypes);

                return fromFactory;

            // new ServiceDescriptor(typeof(IPipelineBehavior<,>), ...) reaches us as the argument.
            case IObjectCreationOperation creation:
                var fromCreation = PipelineKind.None;
                foreach (var argument in creation.Arguments)
                    fromCreation |= ContainsComponentKind(argument.Value, componentTypes);
                return fromCreation;
            case IConversionOperation conversion:
                return ContainsComponentKind(conversion.Operand, componentTypes);
            default:
                return PipelineKind.None;
        }
    }

    /// <summary>
    /// The kind of pipeline a component interface belongs to, or <see cref="PipelineKind.None"/> when the
    /// type is not one of ours. The name test runs only AFTER symbol equality has confirmed the type is
    /// from <c>ComponentMetadataNames</c>, so it can never match somebody else's same-named interface.
    /// </summary>
    private static PipelineKind ComponentKind(ITypeSymbol? type, List<INamedTypeSymbol> componentTypes)
    {
        if (type is not INamedTypeSymbol named)
            return PipelineKind.None;

        var definition = named.OriginalDefinition;
        foreach (var component in componentTypes)
        {
            if (!SymbolEqualityComparer.Default.Equals(definition, component))
                continue;

            return definition.Name == "IStreamPipelineBehavior" ? PipelineKind.Stream : PipelineKind.Request;
        }

        return PipelineKind.None;
    }

    /// <summary>
    /// The <c>IServiceCollection</c> an invocation acts on, so DSOFT010 only pairs a registration with
    /// a scan of the SAME collection. Returns <c>null</c> when it cannot be resolved to a symbol, and
    /// the rule then stays silent rather than guessing.
    /// <para>
    /// Two receivers are walked back to that collection first, because without it the rule was blind to
    /// whole registration styles rather than merely quiet about them:
    /// </para>
    /// <list type="bullet">
    /// <item><description>A FLUENT CHAIN. <c>AddMediator</c>, <c>RegisterMediatorHandlers</c> and the
    /// <c>Precompile*</c> methods are extensions that return the collection they were handed, so in
    /// <c>services.AddMediator().PrecompilePipelines()</c> the scan's receiver is the PREVIOUS
    /// INVOCATION. That resolved to null, the scan was never recorded as a pairing candidate, and
    /// DSOFT010 could not fire anywhere in a file written that way.</description></item>
    /// <item><description>A <c>MediatorBuilder</c>. Its component methods are the whole point of branch
    /// (a) of <see cref="IsComponentRegistration"/>, but their receiver is the builder, never the
    /// collection, so branch (a) could never pair with a scan. <c>new MediatorBuilder(services)</c>
    /// names the collection in its first argument.</description></item>
    /// </list>
    /// <para>
    /// Unwrapping only ever makes the pairing MORE precise: a chain over a different collection still
    /// resolves to that other collection and still does not match. A builder held in a local is left at
    /// null on purpose — which collection it wraps is a dataflow question, and this rule is a Warning
    /// that users build with <c>TreatWarningsAsErrors</c>, so a miss is far cheaper than a guess.
    /// </para>
    /// </summary>
    private static ISymbol? ReceiverSymbol(IInvocationOperation invocation)
        => CollectionSymbol(invocation.Instance
                            ?? (invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null));

    private static ISymbol? CollectionSymbol(IOperation? receiver)
    {
        while (receiver is IConversionOperation conversion)
            receiver = conversion.Operand;

        switch (receiver)
        {
            // A fluent extension hands back the very collection it was given, so the chain's earlier
            // link is the receiver that matters. Gated on the return type matching the receiver type,
            // which is what "returns what it was handed" looks like to the compiler.
            case IInvocationOperation call when ReturnsItsOwnReceiver(call):
                return CollectionSymbol(
                    call.Instance ?? (call.Arguments.Length > 0 ? call.Arguments[0].Value : null));

            // new MediatorBuilder(services) -- the collection is argument 0.
            case IObjectCreationOperation creation
                when IsMediatorBuilder(creation.Type) && creation.Arguments.Length > 0:
                return CollectionSymbol(creation.Arguments[0].Value);
        }

        // The builder a configure lambda is handed stands for the collection the enclosing
        // AddMediator(configure) was called on. This is the shape a late module uses --
        // services.AddMediator(b => b.AddOpenBehavior(...)) called after another module already
        // scanned -- and the components inside it are as late as any other, so the rule has to see
        // through the parameter to say so. Its OWN AddMediator is not a false positive: the scan
        // boundary is the containing statement's end, which encloses the lambda body.
        if (receiver is IParameterReferenceOperation { Parameter.Type: { } parameterType }
            && IsMediatorBuilder(parameterType))
        {
            for (IOperation? node = receiver; node is not null; node = node.Parent)
            {
                if (node is IAnonymousFunctionOperation
                    && node.Parent is IDelegateCreationOperation
                    {
                        Parent: IArgumentOperation { Parent: IInvocationOperation owner },
                    })
                {
                    return CollectionSymbol(
                        owner.Instance ?? (owner.Arguments.Length > 0 ? owner.Arguments[0].Value : null));
                }
            }
        }

        // A builder reached through any other symbol stands for a collection this method cannot name.
        if (IsMediatorBuilder(receiver?.Type))
            return null;

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
    /// True for the fluent shape <c>X Foo(this X self)</c>: the receiver's type and the return type are
    /// the same, so the value flowing on is the value that came in. An extension that returns something
    /// else is left alone rather than assumed.
    /// </summary>
    private static bool ReturnsItsOwnReceiver(IInvocationOperation call)
    {
        var self = call.Instance?.Type
                   ?? (call.Arguments.Length > 0 ? call.Arguments[0].Value.Type : null);

        return self is not null
               && call.Type is not null
               && SymbolEqualityComparer.Default.Equals(self, call.Type);
    }

    /// <summary>The builder, by name and by its own namespace — never somebody else's same-named type.</summary>
    private static bool IsMediatorBuilder(ITypeSymbol? type)
        => type is { Name: "MediatorBuilder" }
           && type.ContainingNamespace?.ToDisplayString() is { } ns
           && ns.StartsWith(MediatorNamespacePrefix, StringComparison.Ordinal);

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
