// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

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
        // closure emit (CloseAllOpenGenericBehaviors / RemoveOpenGenericBehaviorDescriptors) for each kind —
        // the largest previously-uncovered block of the generator.
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
        code.ShouldContain("RemoveOpenGenericBehaviorDescriptors");
        code.ShouldContain("LoggingBehavior");
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
