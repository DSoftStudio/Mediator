// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// ADR-0065 SAFE fast path: when a (request, response) pair maps to exactly ONE nameable
/// concrete handler, the Send emitters (interceptors + typed extensions) emit a file-local
/// concrete-typed dispatch cache — devirtualizing the final <c>Handle</c> call — with an
/// <c>is</c>-typed miss path that degrades to interface dispatch for overridden handlers.
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
        var (result, output) = GeneratorTestHarness.Run<SendInterceptorGenerator>(
            UniqueHandler, interceptors: true, release: true);

        var code = result.AllSource();
        code.ShouldContain("__SendConcreteCache_0",
            customMessage: "a unique nameable handler must produce the concrete-typed cache");
        code.ShouldContain("svc.GetType() == typeof(global::TestApp.PingHandler)",
            customMessage: "the miss path must EXACT-type-test (never hard-cast, never `is` — subclasses could hide Handle) so user overrides degrade gracefully");
        code.ShouldContain("global::DSoftStudio.Mediator.HandlerCache<global::TestApp.Ping, string>.Resolve(sp)",
            customMessage: "the miss path must keep the interface-typed HandlerCache as the L2 tier");
        string.Join(" | ", output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())).ShouldBe("");
    }

    [Fact]
    public void Typed_Extension_Emits_And_Uses_Concrete_Cache()
    {
        var (result, output) = GeneratorTestHarness.Run<MediatorExtensionsGenerator>(UniqueHandler);

        var code = result.AllSource();
        code.ShouldContain("__SendConcreteCache_0.Dispatch(sp, request, cancellationToken)",
            customMessage: "the typed Send extension tail must dispatch through the concrete cache");
        code.ShouldContain("__SendConcreteCache_0.Dispatch(__sp, __r0, cancellationToken)",
            customMessage: "the Send(object) switch case must share the SAME cache (one TLS pair per request type)");
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

        var (result, output) = GeneratorTestHarness.Run<SendInterceptorGenerator>(
            nullableResponse, interceptors: true, release: true);

        result.AllSource().ShouldContain("__SendConcreteCache_0",
            customMessage: "a nullable-annotated response type must still get the concrete cache");
        string.Join(" | ", output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())).ShouldBe("");
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

        var (result, output) = GeneratorTestHarness.Run<SendInterceptorGenerator>(
            internalHandler, interceptors: true, release: true);

        result.AllSource().ShouldContain("__SendConcreteCache_0");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty(
            "generated cache referencing a same-compilation internal handler must compile");
    }
}
