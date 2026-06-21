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
