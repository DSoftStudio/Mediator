// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// ADR-0065 SAFE fast path: when a (request, response) pair maps to exactly ONE nameable
/// concrete handler, the Send emitters (interceptors + typed extensions) emit a file-local
/// concrete-typed dispatch cache — devirtualizing the final <c>Handle</c> call — with an
/// EXACT-type (<c>GetType() == typeof(T)</c>, never <c>is</c>) miss path that degrades to
/// interface dispatch for overridden handlers. Exactness is load-bearing: an <c>is</c> test would
/// match a subclass that hides <c>Handle</c> with <c>new</c> and silently misdispatch it.
/// These tests gate the emission rules; runtime behavior is covered by
/// <c>Integration.SendFastPathIntegrationTests</c>.
/// </summary>
public class SendFastPathTests
{
    private const string UniqueHandler = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record Ping(int N) : IRequest<string>;

        public sealed class PingHandler : IRequestHandler<Ping, string>
        {
            public ValueTask<string> Handle(Ping request, CancellationToken ct) => new("pong");
        }

        public static class Consumer
        {
            public static async Task<string> Run(ISender sender) => await sender.Send<Ping, string>(new Ping(1));
        }
        """;

    [Fact]
    public void Interceptor_Emits_Concrete_Cache_For_Unique_Handler()
    {
        var (_, output) = GeneratorTestHarness.RunChain<MediatorExtensionsGenerator, SendInterceptorGenerator>(
            UniqueHandler, interceptors: true, release: true);

        // Both generators' output: the cache class now lives in MediatorExtensions.g.cs and the
        // interceptor references it, so assertions about the cache BODY must look at the whole
        // compilation rather than only the second generator's result.
        var code = string.Concat(output.SyntaxTrees.Select(t => t.ToString()));
        code.ShouldContain("__SendConcreteCache_",
            customMessage: "a unique nameable handler must produce the concrete-typed cache");
        code.ShouldContain("svc.GetType() == typeof(global::TestApp.PingHandler)",
            customMessage: "the miss path must EXACT-type-test (never hard-cast, never `is` — subclasses could hide Handle) so user overrides degrade gracefully");
        code.ShouldContain("global::DSoftStudio.Mediator.HandlerCache<global::TestApp.Ping, string>.Resolve(sp)",
            customMessage: "the miss path must keep the interface-typed HandlerCache as the L2 tier");
        string.Join(" | ", output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())).ShouldBe("");
    }

    /// <summary>
    /// REGRESSION: both Send call forms must share ONE concrete-cache class per (request, response).
    /// <para>
    /// The name used to be index-derived and the two emitters index differently —
    /// <c>SendInterceptorGenerator</c> by call-site group, <c>MediatorExtensionsGenerator</c> by
    /// request — and each emitted its own <c>file</c>-local class, in different namespaces. So a
    /// pair had TWO rival holders while <c>AggressiveDispatch&lt;,&gt;.TryArm</c> is one-shot
    /// (AggressiveDispatch.cs:279): whichever call form dispatched first consumed the single arm
    /// attempt and the other holder stayed null forever. "Armed" was a property of which call FORM
    /// ran first, not of the request type.
    /// </para>
    /// <para>
    /// Now MediatorExtensionsGenerator OWNS the class (its pair set is a superset of the
    /// intercepted call sites) and emits it <c>internal</c>; the interceptor references it
    /// fully-qualified. Same shape ADR-0066 uses on the Publish side.
    /// </para>
    /// </summary>
    [Fact]
    public void Both_Send_Emitters_Share_One_Concrete_Cache_Per_Pair()
    {
        var (extResult, _) = GeneratorTestHarness.Run<MediatorExtensionsGenerator>(UniqueHandler);
        var (intResult, _) = GeneratorTestHarness.RunChain<MediatorExtensionsGenerator, SendInterceptorGenerator>(
            UniqueHandler, interceptors: true, release: true);

        var extensions = extResult.AllSource();
        var interceptors = intResult.AllSource();

        // Exactly one declaration, and it is the extensions generator's.
        extensions.ShouldContain("internal static class __SendConcreteCache_",
            customMessage: "MediatorExtensionsGenerator owns the cache class and must emit it internal, not file-local");
        interceptors.ShouldNotContain("static class __SendConcreteCache_",
            customMessage: "SendInterceptorGenerator must reference the shared cache, never declare a rival one");

        // Both name the SAME type for the same pair.
        const string expected = "__SendConcreteCache_TestApp_Ping_string";
        extensions.ShouldContain(expected);
        interceptors.ShouldContain(
            "global::DSoftStudio.Mediator.Generated.TestAssembly." + expected,
            customMessage: "the interceptor must reference the shared cache by its fully-qualified name");
    }

    [Fact]
    public void Typed_Extension_Emits_And_Uses_Concrete_Cache()
    {
        var (result, output) = GeneratorTestHarness.Run<MediatorExtensionsGenerator>(UniqueHandler);

        var code = result.AllSource();
        code.ShouldContain(".Dispatch(sp, request, cancellationToken)",
            customMessage: "the typed Send extension tail must dispatch through the concrete cache");
        // __r, not __r0: the body moved out of the case into its own method (see
        // SendObjectOutliningTests), so it no longer carries the case's positional variable name.
        code.ShouldContain(".Dispatch(__sp, __r, cancellationToken)",
            customMessage: "the Send(object) path must share the SAME cache (one TLS pair per request type)");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void No_Concrete_Cache_When_Two_Handlers_Exist_For_Same_Request()
    {
        // Two implementations of the same closed IRequestHandler: MSDI's winner is a registration-order
        // question the generator cannot answer at compile time — the pair must keep the interface tail.
        const string ambiguous = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ping(int N) : IRequest<string>;

            public sealed class PingHandler : IRequestHandler<Ping, string>
            {
                public ValueTask<string> Handle(Ping request, CancellationToken ct) => new("pong");
            }

            public sealed class OtherPingHandler : IRequestHandler<Ping, string>
            {
                public ValueTask<string> Handle(Ping request, CancellationToken ct) => new("other");
            }

            public static class Consumer
            {
                public static async Task<string> Run(ISender sender) => await sender.Send<Ping, string>(new Ping(1));
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<SendInterceptorGenerator>(
            ambiguous, interceptors: true, release: true);

        var code = result.AllSource();
        code.ShouldNotContain("__SendConcreteCache",
            customMessage: "an ambiguous handler mapping must fail open to the interface-typed HandlerCache tail");
        code.ShouldContain("global::DSoftStudio.Mediator.HandlerCache<global::TestApp.Ping, string>.Resolve(sp)");
    }

    [Fact]
    public void No_Concrete_Cache_For_Open_Generic_Handler_Implementation()
    {
        // A generic class implementing the closed handler interface has no single closed concrete
        // type the cache field could be typed to.
        const string genericHandler = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ping(int N) : IRequest<string>;

            public class GenericHandler<TMarker> : IRequestHandler<Ping, string>
            {
                public ValueTask<string> Handle(Ping request, CancellationToken ct) => new("pong");
            }

            public static class Consumer
            {
                public static async Task<string> Run(ISender sender) => await sender.Send<Ping, string>(new Ping(1));
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<SendInterceptorGenerator>(
            genericHandler, interceptors: true, release: true);

        result.AllSource().ShouldNotContain("__SendConcreteCache",
            customMessage: "an open-generic handler implementation must not produce a concrete cache");
    }

    [Fact]
    public void No_Concrete_Cache_When_No_Handler_In_Compilation()
    {
        // Call site with no visible handler (handler lives in an assembly this compilation cannot
        // name): the interceptor must keep today's interface tail.
        const string noHandler = """
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ping(int N) : IRequest<string>;

            public static class Consumer
            {
                public static async Task<string> Run(ISender sender) => await sender.Send<Ping, string>(new Ping(1));
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<SendInterceptorGenerator>(
            noHandler, interceptors: true, release: true);

        var code = result.AllSource();
        code.ShouldNotContain("__SendConcreteCache");
        code.ShouldContain("global::DSoftStudio.Mediator.HandlerCache<global::TestApp.Ping, string>.Resolve(sp)");
    }

    [Fact]
    public void No_Concrete_Cache_For_Explicit_Interface_Implementation()
    {
        // An explicitly-implemented Handle is not a public member of the concrete type: the
        // emitted `concrete.Handle(...)` would fail with CS1061. Such handlers must keep the
        // interface-typed tail. (Found by the phase-1 adversarial review.)
        const string explicitImpl = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ping(int N) : IRequest<string>;

            public sealed class ExplicitPingHandler : IRequestHandler<Ping, string>
            {
                ValueTask<string> IRequestHandler<Ping, string>.Handle(Ping request, CancellationToken ct)
                    => new("pong");
            }

            public static class Consumer
            {
                public static async Task<string> Run(ISender sender) => await sender.Send<Ping, string>(new Ping(1));
            }
            """;

        var (result, output) = GeneratorTestHarness.Run<SendInterceptorGenerator>(
            explicitImpl, interceptors: true, release: true);

        result.AllSource().ShouldNotContain("__SendConcreteCache",
            customMessage: "an explicit-impl handler must not produce a concrete-typed cache");
        string.Join(" | ", output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())).ShouldBe("");
    }

    [Fact]
    public void No_Concrete_Cache_For_Default_Interface_Method_Handle()
    {
        // C# 8 DIM: the Handle implementation lives on the INTERFACE, not the class — the
        // emitted `concrete.Handle(...)` would be CS1061. (Found by the phase-1 review.)
        const string dim = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ping(int N) : IRequest<string>;

            public interface IPingHandlerBase : IRequestHandler<Ping, string>
            {
                ValueTask<string> IRequestHandler<Ping, string>.Handle(Ping request, CancellationToken ct)
                    => new("dim-pong");
            }

            public sealed class PingHandler : IPingHandlerBase
            {
            }

            public static class Consumer
            {
                public static async Task<string> Run(ISender sender) => await sender.Send<Ping, string>(new Ping(1));
            }
            """;

        var (result, output) = GeneratorTestHarness.Run<SendInterceptorGenerator>(
            dim, interceptors: true, release: true);

        result.AllSource().ShouldNotContain("__SendConcreteCache",
            customMessage: "a DIM-implemented handler must not produce a concrete-typed cache");
        string.Join(" | ", output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())).ShouldBe("");
    }

    [Fact]
    public void Concrete_Cache_Emitted_For_Nullable_Annotated_Response()
    {
        const string nullableResponse = """
            #nullable enable
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public sealed record UserDto(string Name);

            public record FindUser(int Id) : IRequest<UserDto?>;

            public sealed class FindUserHandler : IRequestHandler<FindUser, UserDto?>
            {
                public ValueTask<UserDto?> Handle(FindUser request, CancellationToken ct)
                    => new((UserDto?)null);
            }

            public static class Consumer
            {
                public static async Task<UserDto?> Run(ISender sender) => await sender.Send<FindUser, UserDto?>(new FindUser(1));
            }
            """;

        var (result, output) = GeneratorTestHarness.RunChain<MediatorExtensionsGenerator, SendInterceptorGenerator>(
            nullableResponse, interceptors: true, release: true);

        result.AllSource().ShouldContain("__SendConcreteCache_",
            customMessage: "a nullable-annotated response type must still get the concrete cache");
        string.Join(" | ", output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())).ShouldBe("");
    }

    [Fact]
    public void Aggressive_Armed_Gate_Is_Emitted_By_Default()
    {
        var (_, output) = GeneratorTestHarness.RunChain<MediatorExtensionsGenerator, SendInterceptorGenerator>(
            UniqueHandler, interceptors: true, release: true);

        // Both generators' output: the cache class (Extensions) plus the interceptor that uses it.
        var code = string.Concat(output.SyntaxTrees.Select(t => t.ToString()));
        code.ShouldContain(".Armed;",
            customMessage: "the AGGRESSIVE tier is default-ON: the armed-holder gate must be emitted");
        code.ShouldContain("AggressiveDispatch<global::TestApp.Ping, string>",
            customMessage: "arming must go through the runtime eligibility/latch infrastructure");
    }

    [Fact]
    public void DisableAggressive_Knob_Suppresses_Armed_Gate_But_Keeps_Safe_Tier()
    {
        var (result, output) = GeneratorTestHarness.RunChain<MediatorExtensionsGenerator, SendInterceptorGenerator>(
            UniqueHandler, interceptors: true, release: true,
            buildProperties: new() { ["DSoftMediatorDisableAggressive"] = "true" });

        var code = result.AllSource();
        code.ShouldNotContain(".Armed",
            customMessage: "DSoftMediatorDisableAggressive=true must remove the armed-holder gate entirely");
        code.ShouldNotContain("AggressiveDispatch<");
        code.ShouldContain("__SendConcreteCache_",
            customMessage: "the SAFE concrete cache must remain — the knob only disables the AGGRESSIVE tier");
        string.Join(" | ", output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())).ShouldBe("");
    }

    [Fact]
    public void DisableAggressive_Knob_Applies_To_Typed_Extensions_Too()
    {
        var (result, _) = GeneratorTestHarness.Run<MediatorExtensionsGenerator>(
            UniqueHandler,
            buildProperties: new() { ["DSoftMediatorDisableAggressive"] = "true" });

        var code = result.AllSource();
        code.ShouldNotContain(".Armed");
        code.ShouldContain(".Dispatch(sp, request, cancellationToken)");
    }

    [Fact]
    public void Generated_Cache_Compiles_With_Internal_Handler()
    {
        // Same-compilation internal handlers are nameable from generated code — the cache must be
        // emitted and the whole compilation must stay error-free.
        const string internalHandler = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ping(int N) : IRequest<string>;

            internal sealed class PingHandler : IRequestHandler<Ping, string>
            {
                public ValueTask<string> Handle(Ping request, CancellationToken ct) => new("pong");
            }

            public static class Consumer
            {
                public static async Task<string> Run(ISender sender) => await sender.Send<Ping, string>(new Ping(1));
            }
            """;

        var (result, output) = GeneratorTestHarness.RunChain<MediatorExtensionsGenerator, SendInterceptorGenerator>(
            internalHandler, interceptors: true, release: true);

        result.AllSource().ShouldContain("__SendConcreteCache_");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty(
            "generated cache referencing a same-compilation internal handler must compile");
    }
}
