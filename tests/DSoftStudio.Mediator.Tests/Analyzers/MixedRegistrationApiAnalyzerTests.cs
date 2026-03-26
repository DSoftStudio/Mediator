// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Analyzers;

/// <summary>
/// Verifies that <see cref="MixedRegistrationApiAnalyzer"/> emits the correct diagnostics:
/// <list type="bullet">
///   <item>DSOFT007 — mixed registration API usage (AddMediator(configure) + RegisterMediatorHandlers/PrecompilePipelines)</item>
/// </list>
/// </summary>
public class MixedRegistrationApiAnalyzerTests
{
    /// <summary>
    /// Minimal stub types so the semantic model resolves mediator registration methods.
    /// </summary>
    private const string StubSource = """
        namespace DSoftStudio.Mediator.Abstractions
        {
            public interface IRequest<out TResponse> { }
            public interface IRequestHandler<in TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                System.Threading.Tasks.ValueTask<TResponse> Handle(
                    TRequest request, System.Threading.CancellationToken ct);
            }
        }

        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection : System.Collections.Generic.IList<ServiceDescriptor> { }
            public class ServiceDescriptor { }
        }

        namespace DSoftStudio.Mediator
        {
            public sealed class MediatorBuilder
            {
                public MediatorBuilder(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
            }

            public static class ServiceCollectionExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediator(
                    Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
            }
        }

        namespace DSoftStudio.Mediator.Generated.TestAssembly
        {
            public static class MediatorServiceRegistryExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection RegisterMediatorHandlers(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
            }

            public static class MediatorRegistryExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection PrecompilePipelines(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;

                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediator(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
                    System.Action<DSoftStudio.Mediator.MediatorBuilder> configure) => services;
            }
        }
        """;

    private static GeneratorRunResult RunAnalyzer(string userSource)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(StubSource, path: "Stubs.cs"),
            CSharpSyntaxTree.ParseText(userSource, path: "UserCode.cs"),
        };

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new MixedRegistrationApiAnalyzer();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new IIncrementalGenerator[] { analyzer }
                .Select(GeneratorExtensions.AsSourceGenerator));

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Results.Single();
    }

    // ── DSOFT007: Mixed registration API ──────────────────────────

    [Fact]
    public void Emits_DSOFT007_When_AddMediator_Builder_And_RegisterMediatorHandlers()
    {
        const string source = """
            using DSoftStudio.Mediator.Generated.TestAssembly;

            public class Startup
            {
                public void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddMediator(builder => { });
                    services.RegisterMediatorHandlers();
                }
            }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT007");
        var diag = result.Diagnostics.First(d => d.Id == "DSOFT007");
        diag.GetMessage().ShouldContain("RegisterMediatorHandlers()");
        diag.GetMessage().ShouldContain("registers handlers");
    }

    [Fact]
    public void Emits_DSOFT007_When_AddMediator_Builder_And_PrecompilePipelines()
    {
        const string source = """
            using DSoftStudio.Mediator.Generated.TestAssembly;

            public class Startup
            {
                public void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddMediator(builder => { });
                    services.PrecompilePipelines();
                }
            }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT007");
        var diag = result.Diagnostics.First(d => d.Id == "DSOFT007");
        diag.GetMessage().ShouldContain("PrecompilePipelines()");
        diag.GetMessage().ShouldContain("precompiles pipelines");
    }

    [Fact]
    public void Emits_DSOFT007_On_Both_When_AddMediator_Builder_With_RegisterHandlers_And_Precompile()
    {
        const string source = """
            using DSoftStudio.Mediator.Generated.TestAssembly;

            public class Startup
            {
                public void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddMediator(builder => { });
                    services.RegisterMediatorHandlers();
                    services.PrecompilePipelines();
                }
            }
            """;

        var result = RunAnalyzer(source);

        var dsoft007 = result.Diagnostics.Where(d => d.Id == "DSOFT007").ToList();
        dsoft007.Count.ShouldBe(2);
        dsoft007.ShouldContain(d => d.GetMessage().Contains("RegisterMediatorHandlers()"));
        dsoft007.ShouldContain(d => d.GetMessage().Contains("PrecompilePipelines()"));
    }

    [Fact]
    public void Does_Not_Emit_DSOFT007_When_Only_Builder_Overload_Used()
    {
        const string source = """
            using DSoftStudio.Mediator.Generated.TestAssembly;

            public class Startup
            {
                public void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddMediator(builder => { });
                }
            }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT007");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT007_When_Only_Individual_Calls_Used()
    {
        const string source = """
            using DSoftStudio.Mediator.Generated.TestAssembly;

            public class Startup
            {
                public void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services);
                    services.RegisterMediatorHandlers();
                    services.PrecompilePipelines();
                }
            }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT007");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT007_When_No_Registration_Calls()
    {
        const string source = """
            public class NoMediator
            {
                public void DoStuff() { }
            }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT007");
    }
}
