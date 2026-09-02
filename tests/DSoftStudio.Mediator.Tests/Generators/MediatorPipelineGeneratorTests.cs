// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Drives the real <see cref="MediatorPipelineGenerator"/> in-memory and asserts the generated
/// <c>MediatorRegistry.g.cs</c> — the single registration entry point (<c>RegisterMediatorHandlers</c> /
/// <c>RegisterPipelineChains</c> / <c>PrecompilePipelines</c>). It had zero coverage before this.
/// </summary>
public class MediatorPipelineGeneratorTests
{
    private const string RequestHandler = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record GetUser(int Id) : IRequest<string>;

        public sealed class GetUserHandler : IRequestHandler<GetUser, string>
        {
            public ValueTask<string> Handle(GetUser request, CancellationToken ct) => new("user");
        }
        """;

    [Fact]
    public void Generates_MediatorRegistry_For_RequestHandler()
    {
        var (result, _) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(RequestHandler);
        var code = result.AllSource();

        code.ShouldContain("MediatorRegistry");
        code.ShouldContain("RegisterMediatorHandlers");
        code.ShouldContain("RegisterPipelineChains");
        code.ShouldContain("PrecompilePipelines");
        code.ShouldContain("GetUser");
    }

    private const string NullableResponseChain = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestApp;

        public sealed class UserDto { public int Id; }

        public record FindUser(int Id) : IRequest<UserDto?>;

        public sealed class FindUserHandler : IRequestHandler<FindUser, UserDto?>
        {
            public ValueTask<UserDto?> Handle(FindUser request, CancellationToken ct)
                => new((UserDto?)null);
        }

        public sealed class TraceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
            where TRequest : IRequest<TResponse>
        {
            public ValueTask<TResponse> Handle(
                TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                => next.Handle(request, ct);
        }

        public static class Root
        {
            public static void Configure(IServiceCollection services)
            {
                // CLOSED registration on purpose: only this path runs the request/response types
                // through BehaviorRegistrationScanner's own display format. An OPEN registration
                // closes over HandlerInfo's strings instead and would not exercise the bug at all.
                services.AddScoped(
                    typeof(IPipelineBehavior<FindUser, UserDto?>),
                    typeof(TraceBehavior<FindUser, UserDto?>));
            }
        }
        """;

    /// <summary>
    /// REGRESSION: the chain predictor must render types with the SAME display format the rest of
    /// the generator uses — <c>HandlerDiscovery.NullableFullyQualifiedFormat</c>, which carries
    /// <c>IncludeNullableReferenceTypeModifier</c>.
    /// <para>
    /// BehaviorRegistrationScanner declared its own format without that option. That was wrong twice:
    /// the emitted chain link fields would render a nullable response as <c>global::Ns.UserDto</c>
    /// instead of <c>global::Ns.UserDto?</c>, producing CS8631 nullability mismatches in the
    /// consumer's build — the exact failure HandlerDiscovery's own comment says all generators must
    /// avoid — and closed-registration matching compared those strings against HandlerInfo's, which
    /// DO carry the annotation, so a nullable pair would silently never match its own registration.
    /// </para>
    /// </summary>
    [Fact]
    public void PredictedChain_RendersNullableResponse_WithAnnotation()
    {
        var (result, output) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(NullableResponseChain);
        var code = result.AllSource();

        code.ShouldContain("__ChainLink0_",
            customMessage: "the registration is readable, so this pair should get a predicted chain");

        code.ShouldContain("global::TestApp.UserDto?",
            customMessage: "the nullable annotation must survive into the emitted chain");

        output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error
                        && (d.Id == "CS8631" || d.Id == "CS0029" || d.Id == "CS1503"))
            .ShouldBeEmpty("a nullability mismatch in the emitted chain breaks the consumer's build");
    }

    private const string NestedBehaviors = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record GetUser(int Id) : IRequest<string>;

        public sealed class GetUserHandler : IRequestHandler<GetUser, string>
        {
            public ValueTask<string> Handle(GetUser request, CancellationToken ct) => new("u");
        }

        // Nested inside a NON-generic holder: perfectly registerable, must be discovered.
        public static class Behaviors
        {
            public sealed class Logging<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                public ValueTask<TResponse> Handle(
                    TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                    => next.Handle(request, ct);
            }

            // Not nameable from the generated registry.
            private sealed class Hidden<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                public ValueTask<TResponse> Handle(
                    TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                    => next.Handle(request, ct);
            }
        }

        // Enclosing type is GENERIC: typeof(GenericHolder<>.Inner<,>) is not legal C#.
        public static class GenericHolder<TMarker>
        {
            public sealed class Inner<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                public ValueTask<TResponse> Handle(
                    TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                    => next.Handle(request, ct);
            }
        }
        """;

    /// <summary>
    /// REGRESSION: open-generic behaviors declared as NESTED types must be discovered and closed.
    /// <para>
    /// Both discovery paths walked <c>INamespaceSymbol.GetTypeMembers()</c> and recursed through
    /// NAMESPACES only — never into nested types. A behavior inside a holder class was therefore
    /// invisible, its open-generic descriptor was never closed, and MSDI fell back to
    /// <c>MakeGenericType</c>: the Native AOT failure this whole closure machinery exists to
    /// prevent, reached silently and with no diagnostic.
    /// </para>
    /// <para>
    /// Two exclusions are deliberate and asserted here: a <c>private</c> nested type is not
    /// nameable from the generated registry, and a type whose ENCLOSING type is generic cannot be
    /// written as an open generic at all — <c>typeof(Holder&lt;&gt;.Inner&lt;,&gt;)</c> is not legal C#,
    /// and BaseTypeNameFormat omits generic arguments, so emitting it would not compile.
    /// </para>
    /// </summary>
    [Fact]
    public void NestedOpenGenericBehaviors_AreDiscovered_ExceptWhenUnnameable()
    {
        var (result, _) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(NestedBehaviors);
        var code = result.AllSource();

        code.ShouldContain("global::TestApp.Behaviors.Logging<global::TestApp.GetUser, string>",
            customMessage: "a behavior nested in a non-generic holder must be discovered and closed");

        code.ShouldNotContain("Hidden",
            customMessage: "a private nested behavior is not nameable from the generated registry");
        code.ShouldNotContain("GenericHolder",
            customMessage: "typeof(GenericHolder<>.Inner<,>) is not legal C# — it must be skipped, not emitted");
    }

    private const string ConstrainedBehavior = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public interface IAuditable { }

        public record GetUser(int Id) : IRequest<string>;

        public sealed class GetUserHandler : IRequestHandler<GetUser, string>
        {
            public ValueTask<string> Handle(GetUser request, CancellationToken ct) => new("user");
        }

        // GetUser does NOT implement IAuditable, so closing this behavior over (GetUser, string)
        // is a compile error.
        public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
            where TRequest : IRequest<TResponse>, IAuditable
        {
            public ValueTask<TResponse> Handle(
                TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                => next.Handle(request, ct);
        }
        """;

    /// <summary>
    /// REGRESSION: a behavior whose type parameters carry constraints beyond the ones
    /// <c>IPipelineBehavior</c> declares must NOT be auto-closed over every handler pair.
    /// <para>
    /// Behavior discovery only checked namespace, interface metadata name and arity — never
    /// constraints — while <c>CloseAllOpenGenericBehaviors</c> emits
    /// <c>typeof(Behavior&lt;Request, Response&gt;)</c> for ALL known pairs. A behavior constrained
    /// to, say, <c>IAuditable</c> therefore produced <b>CS0311/CS0315 in the consumer's build</b>,
    /// in generated code the consumer never wrote and cannot edit.
    /// </para>
    /// <para>
    /// The assertion is on the OUTPUT COMPILATION rather than on emitted text: the symptom users
    /// hit is a failing build, so that is what the test should reproduce.
    /// </para>
    /// </summary>
    [Fact]
    public void ConstrainedBehavior_IsNotClosedOverIncompatibleHandlerPairs()
    {
        var (result, output) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(ConstrainedBehavior);

        // Only constraint violations are in scope. The harness drives ONE generator, so the emitted
        // registry legitimately cannot resolve its sibling generators' extension methods
        // (RegisterMediatorHandlers / PrecompileNotifications / PrecompileStreams -> CS1061);
        // that is a property of the isolated harness, not of the generated code in a real build.
        var constraintErrors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error
                        && (d.Id == "CS0311" || d.Id == "CS0315" || d.Id == "CS0453"))
            .ToList();

        constraintErrors.ShouldBeEmpty(
            "a behavior constrained beyond IPipelineBehavior must not be closed over incompatible "
            + "handler pairs — those closed generics break the consumer's build:\n"
            + string.Join("\n", constraintErrors.Select(e => e.ToString())));

        // And specifically: no closed AuditBehavior over the incompatible pair.
        result.AllSource().ShouldNotContain("AuditBehavior<global::TestApp.GetUser");
    }

    /// <summary>
    /// REGRESSION: the single-call <c>AddMediator(configure)</c> overload must precompile
    /// notifications and streams, not only pipelines.
    /// <para>
    /// The emitted body used to call only <c>HandlerLifetimeOptimizer.Apply</c> +
    /// <c>RegisterPipelineChains</c> + <c>RequestObjectDispatch.Freeze</c>. With no custom
    /// publisher, <c>Mediator.Publish&lt;T&gt;</c> goes through
    /// <c>NotificationCachedDispatcher.DispatchSequential</c>, which returns
    /// <c>Task.CompletedTask</c> while <c>NotificationDispatch&lt;T&gt;.Handlers</c> is null — so
    /// <c>Publish</c> silently dispatched nothing, while <c>Publish(object)</c> threw. The docs
    /// advertise this overload as doing "everything" and tell users NOT to mix it with the
    /// individual <c>Precompile*</c> calls, so the single-call path has to be complete.
    /// </para>
    /// <para>
    /// This is asserted on the EMITTED TEXT rather than by publishing through a built provider:
    /// <c>NotificationRegistry.Register</c> initializes the process-global, write-once
    /// <c>NotificationDispatch&lt;T&gt;</c> for every notification type in the assembly, so any other
    /// test that calls <c>PrecompileNotifications()</c> primes that static and a runtime test
    /// passes whether or not the emitter is correct.
    /// </para>
    /// </summary>
    [Fact]
    public void AddMediatorConfigure_AlsoPrecompilesNotificationsAndStreams()
    {
        var (result, _) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(RequestHandler);
        var code = result.AllSource();

        var start = code.IndexOf("IServiceCollection AddMediator(", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(-1, "the AddMediator(configure) overload should be emitted");
        var body = code[start..];

        body.ShouldContain("services.PrecompileNotifications();");
        body.ShouldContain("services.PrecompileStreams();");

        // Order matters: both must run AFTER configure(builder), so a publisher or stream behavior
        // registered through the builder is visible to the registries.
        body.IndexOf("configure(builder);", StringComparison.Ordinal)
            .ShouldBeLessThan(body.IndexOf("services.PrecompileNotifications();", StringComparison.Ordinal));
    }

    [Fact]
    public void RegisterPipeline_FoldsHandlerLifetimeIntoChainLifetime()
    {
        // ADR-0001: the chain lifetime is the lowest of everything it wraps - including the HANDLER, not just
        // the pipeline components. The chain's constructor consumes the handler, so a Singleton chain wrapping
        // a Transient/Scoped handler would capture it (and its scoped deps, e.g. an injected IMediator) for the
        // whole app lifetime: the captive-dependency crash (cannot consume scoped service from singleton).
        // RegisterPipeline must therefore fold the IRequestHandler<TRequest, TResponse> descriptor's lifetime
        // into allSingleton, not only the behaviors/processors.
        var (result, _) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(RequestHandler);
        var code = result.AllSource();

        code.ShouldContain("st == typeof(global::DSoftStudio.Mediator.Abstractions.IRequestHandler<TRequest, TResponse>)");
    }

    [Fact]
    public void Emits_Aot_Behavior_Closure_For_OpenGeneric_Behavior_And_Processor()
    {
        // A handler PLUS open-generic pipeline components (behavior + pre-processor) triggers the AOT-safe
        // closure emit (CloseAllOpenGenericBehaviors) for each kind — the largest previously-uncovered
        // block of the generator.
        const string rich = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record GetUser(int Id) : IRequest<string>;

            public sealed class GetUserHandler : IRequestHandler<GetUser, string>
            {
                public ValueTask<string> Handle(GetUser request, CancellationToken ct) => new("u");
            }

            public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                public ValueTask<TResponse> Handle(
                    TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
                    => next.Handle(request, ct);
            }

            public sealed class ValidationPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
            {
                public ValueTask Process(TRequest request, CancellationToken ct) => default;
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(rich);
        var code = result.AllSource();

        code.ShouldContain("CloseAllOpenGenericBehaviors");
        code.ShouldContain("LoggingBehavior");

        // The closure SPLICES in place — RemoveAt at the open descriptor's index, then Insert of the
        // closed expansions at that same index — so registration order survives. It must NOT append
        // (services.Add), which is what pushed open-generic behaviors to the innermost position.
        code.ShouldContain("services.RemoveAt(i);");
        code.ShouldContain("services.Insert(i + 0, new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(");

        // The separate second pass is gone: removal now happens in place, under the identical guard.
        code.ShouldNotContain("RemoveOpenGenericBehaviorDescriptors");
    }

    [Fact]
    public void Registers_Self_Handling_Request()
    {
        // A self-handling request — implements IRequest<T>, has a static Execute, and NO separate
        // IRequestHandler — is registered through the self-handler discovery path (previously uncovered).
        const string selfHandler = """
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record GetTime(int Tz) : IRequest<string>
            {
                public static string Execute(GetTime request) => "now";
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(selfHandler);
        var code = result.AllSource();

        code.ShouldContain("MediatorRegistry");
        code.ShouldContain("GetTime");
    }

    [Fact]
    public void Generates_Registry_Skeleton_When_No_Handlers()
    {
        // No request handler at all → the registry entry points are still emitted (so consumer startup code
        // that calls them compiles), just with no per-handler registration. Covers the empty path.
        const string none = """
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record GetUser(int Id) : IRequest<string>;
            """;

        var (result, _) = GeneratorTestHarness.Run<MediatorPipelineGenerator>(none);
        var code = result.AllSource();

        code.ShouldContain("MediatorRegistry");
        code.ShouldContain("PrecompilePipelines");
    }
}
