// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DSoftStudio.Mediator.Tests.Analyzers;

/// <summary>
/// High-fidelity tests for <see cref="MixedRegistrationApiAnalyzer"/> that run the <b>real</b>
/// <see cref="DependencyInjectionGenerator"/> first, then the analyzer on the resulting (generated)
/// compilation — exactly as the compiler does in a real build.
/// <para>
/// This is the scenario the previous stub-only tests could not exercise: in a real build,
/// <c>RegisterMediatorHandlers()</c> (and the builder overload) are <i>generated</i>, not source.
/// A source generator cannot see another generator's output, so the analyzer used to miss those
/// calls entirely (DSOFT007 silently dead; DSOFT008 false positives). As a
/// <see cref="DiagnosticAnalyzer"/>, it runs after generators and resolves them correctly.
/// </para>
/// </summary>
public class MixedRegistrationApiIntegrationTests
{
    private const string AbstractionsSource = """
        namespace DSoftStudio.Mediator.Abstractions
        {
            public interface IRequest<out TResponse> { }
            public interface ICommand { }
            public interface ICommand<out TResponse> : IRequest<TResponse>, ICommand { }
            public interface IQuery { }
            public interface IQuery<out TResponse> : IRequest<TResponse>, IQuery { }
            public interface IStreamRequest<out TResponse> { }
            public interface INotification { }

            public interface IRequestHandler<in TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                System.Threading.Tasks.ValueTask<TResponse> Handle(
                    TRequest request, System.Threading.CancellationToken ct);
            }

            public interface IStreamRequestHandler<in TRequest, out TResponse>
                where TRequest : IStreamRequest<TResponse>
            {
                System.Collections.Generic.IAsyncEnumerable<TResponse> Handle(
                    TRequest request, System.Threading.CancellationToken ct);
            }

            public interface INotificationHandler<in TNotification>
                where TNotification : INotification
            {
                System.Threading.Tasks.Task Handle(
                    TNotification notification, System.Threading.CancellationToken ct);
            }

            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class MediatorHandlerRegistrationAttribute : System.Attribute
            {
                public MediatorHandlerRegistrationAttribute(System.Type serviceType, System.Type implementationType) { }
            }
        }
        """;

    private const string DependencyInjectionStubSource = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection : System.Collections.Generic.IList<ServiceDescriptor> { }
            public class ServiceDescriptor { }
            public static class ServiceCollectionServiceExtensions
            {
                public static IServiceCollection AddSingleton<TService>(IServiceCollection s) where TService : class => s;
                public static IServiceCollection AddTransient<TService, TImpl>(IServiceCollection s) where TImpl : class, TService => s;
                public static IServiceCollection AddSingleton<TService, TImpl>(IServiceCollection s) where TImpl : class, TService => s;
            }
        }
        namespace Microsoft.Extensions.DependencyInjection.Extensions
        {
            public static class ServiceCollectionDescriptorExtensions
            {
                public static void TryAddTransient(IServiceCollection s, System.Type t) { }
                public static void TryAddSingleton(IServiceCollection s, System.Type t) { }
            }
        }
        """;

    /// <summary>
    /// The hand-written parts of the registration surface: the parameterless <c>AddMediator()</c>
    /// (which lives in the runtime) and the builder overload (which the pipeline generator emits in
    /// the <c>...Generated.TestAssembly</c> namespace — stubbed here as source so the builder case is
    /// resolvable). <c>RegisterMediatorHandlers()</c> is deliberately NOT here: the real DI generator
    /// emits it, so the test proves the analyzer sees the generated member.
    /// </summary>
    private const string RegistrationApiStubSource = """
        namespace DSoftStudio.Mediator
        {
            public sealed class MediatorBuilder
            {
                public MediatorBuilder(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
            }

            public static class ServiceCollectionExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediator(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
            }
        }
        namespace DSoftStudio.Mediator.Generated.TestAssembly
        {
            public static class MediatorRegistryExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediator(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
                    System.Action<DSoftStudio.Mediator.MediatorBuilder> configure) => services;
            }
        }
        """;

    private const string HandlerSource = """
        using DSoftStudio.Mediator.Abstractions;

        public sealed class Ping : IRequest<string> { }

        public sealed class PingHandler : IRequestHandler<Ping, string>
        {
            public System.Threading.Tasks.ValueTask<string> Handle(
                Ping request, System.Threading.CancellationToken ct) => default;
        }
        """;

    /// <summary>
    /// Builds a compilation, runs the real <see cref="DependencyInjectionGenerator"/> (so
    /// <c>RegisterMediatorHandlers()</c> and the <c>[assembly: MediatorHandlerRegistration]</c>
    /// attributes are emitted), then runs the analyzer on the final compilation.
    /// </summary>
    private static ImmutableArray<Diagnostic> Analyze(string startupSource)
    {
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(AbstractionsSource, path: "Abstractions.cs"),
            CSharpSyntaxTree.ParseText(DependencyInjectionStubSource, path: "DI.cs"),
            CSharpSyntaxTree.ParseText(RegistrationApiStubSource, path: "RegistrationApi.cs"),
            CSharpSyntaxTree.ParseText(HandlerSource, path: "Handlers.cs"),
            CSharpSyntaxTree.ParseText(startupSource, path: "Startup.cs"),
        };

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // 1. Run the REAL DI generator → emits RegisterMediatorHandlers() + assembly attributes.
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new IIncrementalGenerator[] { new DependencyInjectionGenerator() }
                .Select(GeneratorExtensions.AsSourceGenerator));

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var generatedCompilation, out _);

        // 2. Run the analyzer on the generated (final) compilation, like the real compiler.
        var withAnalyzers = generatedCompilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new MixedRegistrationApiAnalyzer()));

        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void Does_Not_Emit_DSOFT008_When_Generated_RegisterMediatorHandlers_Is_Called()
    {
        // The regression: RegisterMediatorHandlers() is GENERATED. The old generator-based analyzer
        // could not see it and produced a false DSOFT008 here. The analyzer must now see it.
        const string startup = """
            using DSoftStudio.Mediator;
            using Microsoft.Extensions.DependencyInjection;

            public class Startup
            {
                public void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.RegisterMediatorHandlers();
                }
            }
            """;

        var diagnostics = Analyze(startup);

        diagnostics.ShouldNotContain(d => d.Id == "DSOFT008");
        diagnostics.ShouldNotContain(d => d.Id == "DSOFT007");
    }

    [Fact]
    public void Emits_DSOFT008_When_Parameterless_AddMediator_Without_Registration()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using Microsoft.Extensions.DependencyInjection;

            public class Startup
            {
                public void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                }
            }
            """;

        var diagnostics = Analyze(startup);

        diagnostics.ShouldContain(d => d.Id == "DSOFT008");
    }

    [Fact]
    public void Emits_DSOFT007_When_Builder_Overload_Mixed_With_Generated_RegisterMediatorHandlers()
    {
        // The builder overload + the GENERATED RegisterMediatorHandlers() in the same scope.
        // This is the DSOFT007 case that was silently dead with the old generator-based analyzer.
        const string startup = """
            using Microsoft.Extensions.DependencyInjection;

            public class Startup
            {
                public void Configure(IServiceCollection services)
                {
                    services.AddMediator(builder => { });
                    services.RegisterMediatorHandlers();
                }
            }
            """;

        var diagnostics = Analyze(startup);

        diagnostics.ShouldContain(d => d.Id == "DSOFT007");
        var diag = diagnostics.First(d => d.Id == "DSOFT007");
        diag.GetMessage().ShouldContain("RegisterMediatorHandlers()");
    }
}
