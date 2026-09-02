// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DSoftStudio.Mediator.Tests.Analyzers;

/// <summary>
/// <c>DSOFT010</c> — a pipeline component registered after the scan that decides whether a chain is
/// built for it. The registration silently never runs, so the compiler is the cheapest place to say so.
/// <para>
/// The rule is deliberately narrow: same method body, same service collection. That is a Program.cs
/// shape. Registrations that live in another method or another assembly are unreachable here — there
/// is no compilation-wide ordering to consult — and are caught at startup by
/// <c>ValidateMediatorHandlers()</c> instead.
/// </para>
/// </summary>
public class ComponentAfterPrecompileDiagnosticTests
{
    private const string AbstractionsSource = """
        namespace DSoftStudio.Mediator.Abstractions
        {
            public interface IRequest<out TResponse> { }
            public interface INotification { }
            public interface IStreamRequest<out TResponse> { }

            public interface IRequestHandler<in TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                System.Threading.Tasks.ValueTask<TResponse> Handle(
                    TRequest request, System.Threading.CancellationToken ct);
            }

            public interface IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                System.Threading.Tasks.ValueTask<TResponse> Handle(
                    TRequest request, IRequestHandler<TRequest, TResponse> next,
                    System.Threading.CancellationToken ct);
            }
        }
        """;

    private const string DependencyInjectionStubSource = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection { }

            public static class ServiceCollectionServiceExtensions
            {
                public static IServiceCollection AddTransient<TService, TImpl>(this IServiceCollection s)
                    where TImpl : class, TService => s;

                public static IServiceCollection AddTransient(this IServiceCollection s, System.Type service, System.Type impl) => s;
            }
        }
        """;

    private const string RegistrationApiStubSource = """
        namespace DSoftStudio.Mediator
        {
            public static class ServiceCollectionExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediator(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;

                public static Microsoft.Extensions.DependencyInjection.IServiceCollection PrecompilePipelines(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
            }
        }
        """;

    private const string HandlerSource = """
        using DSoftStudio.Mediator.Abstractions;

        public sealed class Ping : IRequest<int> { }

        public sealed class PingHandler : IRequestHandler<Ping, int>
        {
            public System.Threading.Tasks.ValueTask<int> Handle(Ping r, System.Threading.CancellationToken ct)
                => default;
        }

        public sealed class LoggingBehavior : IPipelineBehavior<Ping, int>
        {
            public System.Threading.Tasks.ValueTask<int> Handle(
                Ping r, IRequestHandler<Ping, int> next, System.Threading.CancellationToken ct)
                => next.Handle(r, ct);
        }

        public sealed class OpenLogging<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
            where TRequest : IRequest<TResponse>
        {
            public System.Threading.Tasks.ValueTask<TResponse> Handle(
                TRequest r, IRequestHandler<TRequest, TResponse> next, System.Threading.CancellationToken ct)
                => next.Handle(r, ct);
        }
        """;

    private static Diagnostic[] Analyze(string startupSource)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [
                CSharpSyntaxTree.ParseText(AbstractionsSource, path: "Abstractions.cs"),
                CSharpSyntaxTree.ParseText(DependencyInjectionStubSource, path: "DI.cs"),
                CSharpSyntaxTree.ParseText(RegistrationApiStubSource, path: "RegistrationApi.cs"),
                CSharpSyntaxTree.ParseText(HandlerSource, path: "Handlers.cs"),
                CSharpSyntaxTree.ParseText(startupSource, path: "Startup.cs"),
            ],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MixedRegistrationApiAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        return [.. diagnostics.Where(d => d.Id == "DSOFT010")];
    }

    [Fact]
    public void Reports_A_Closed_Behavior_Registered_After_The_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.PrecompilePipelines();
                    services.AddTransient<IPipelineBehavior<Ping, int>, LoggingBehavior>();
                }
            }
            """;

        Analyze(startup).Length.ShouldBe(1);
    }

    [Fact]
    public void Reports_An_Open_Generic_Behavior_Registered_After_The_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.PrecompilePipelines();
                    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(OpenLogging<,>));
                }
            }
            """;

        Analyze(startup).Length.ShouldBe(1);
    }

    [Fact]
    public void Ignores_A_Behavior_Registered_Before_The_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.AddTransient<IPipelineBehavior<Ping, int>, LoggingBehavior>();
                    services.PrecompilePipelines();
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_A_Registration_On_A_Different_Service_Collection()
    {
        // The scan and the registration are ordered, but they are not the same collection, so there
        // is nothing to report. Reporting here would be a false positive, and DSOFT010 is a Warning.
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection first, IServiceCollection second)
                {
                    first.AddMediator();
                    first.PrecompilePipelines();
                    second.AddTransient<IPipelineBehavior<Ping, int>, LoggingBehavior>();
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_A_Registration_That_Is_Not_A_Pipeline_Component()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.PrecompilePipelines();
                    services.AddTransient<IRequestHandler<Ping, int>, PingHandler>();
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }
}
