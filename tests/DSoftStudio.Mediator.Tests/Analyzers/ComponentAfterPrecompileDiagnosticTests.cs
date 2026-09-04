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

    /// <summary>
    /// The builder, with the two ways a caller can hold one: the <c>AddMediator(configure)</c> overload
    /// that hands it to a lambda, and the public constructor over a collection. Its component methods
    /// are what branch (a) of the analyzer's IsComponentRegistration matches.
    /// </summary>
    private const string BuilderStubSource = """
        namespace DSoftStudio.Mediator.Abstractions
        {
            public interface IStreamPipelineBehavior<TRequest, TResponse> { }
        }

        namespace DSoftStudio.Mediator
        {
            public sealed class MediatorBuilder
            {
                public MediatorBuilder(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }

                public MediatorBuilder AddOpenBehavior(System.Type behaviorType) => this;

                public MediatorBuilder AddStreamBehavior<T>() => this;
            }

            public static class BuilderServiceCollectionExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediator(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
                    System.Action<MediatorBuilder> configure) => services;

                public static Microsoft.Extensions.DependencyInjection.IServiceCollection PrecompileStreams(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;

                public static Microsoft.Extensions.DependencyInjection.IServiceCollection PrecompileNotifications(
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

    private const string StreamHandlerSource = """
        using DSoftStudio.Mediator.Abstractions;

        public sealed class PingStream : IStreamRequest<int> { }

        public sealed class StreamLogging : IStreamPipelineBehavior<PingStream, int> { }
        """;

    private const string DescriptorStubSource = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public sealed class ServiceDescriptor
            {
                public static ServiceDescriptor Singleton(System.Type service, System.Type impl) => null!;
                public static ServiceDescriptor Scoped(System.Type service, System.Type impl) => null!;
                public static ServiceDescriptor Scoped<TService, TImpl>() where TImpl : class, TService => null!;
            }
        }

        namespace Microsoft.Extensions.DependencyInjection.Extensions
        {
            public static class ServiceCollectionDescriptorExtensions
            {
                public static void TryAddEnumerable(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
                    Microsoft.Extensions.DependencyInjection.ServiceDescriptor descriptor) { }
            }
        }
        """;

    /// <summary>
    /// The companion packages, in their real namespaces and with their real type and method names.
    /// Their extension methods add the descriptor INSIDE themselves, in another assembly, so nothing
    /// at the call site names a component interface — which is exactly why the analyzer has to know
    /// them by name. The decoy carries the same method name in somebody else's namespace.
    /// </summary>
    private const string CompanionStubSource = """
        namespace DSoftStudio.Mediator.FluentValidation
        {
            public static class FluentValidationServiceCollectionExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediatorFluentValidation(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
            }
        }

        namespace DSoftStudio.Mediator.HybridCache
        {
            public static class HybridCacheServiceCollectionExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediatorHybridCache(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
            }
        }

        namespace DSoftStudio.Mediator.OpenTelemetry
        {
            public static class OpenTelemetryServiceCollectionExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediatorInstrumentation(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
            }
        }

        namespace Contoso.Mediator.HybridCache
        {
            public static class HybridCacheServiceCollectionExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddMediatorHybridCache(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
            }
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
                CSharpSyntaxTree.ParseText(BuilderStubSource, path: "Builder.cs"),
                CSharpSyntaxTree.ParseText(StreamHandlerSource, path: "Streams.cs"),
                CSharpSyntaxTree.ParseText(DescriptorStubSource, path: "Descriptors.cs"),
                CSharpSyntaxTree.ParseText(CompanionStubSource, path: "Companions.cs"),
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

    // ── The companion packages ────────────────────────────────────────

    [Theory]
    [InlineData("DSoftStudio.Mediator.FluentValidation", "AddMediatorFluentValidation")]
    [InlineData("DSoftStudio.Mediator.HybridCache", "AddMediatorHybridCache")]
    [InlineData("DSoftStudio.Mediator.OpenTelemetry", "AddMediatorInstrumentation")]
    public void Reports_A_Companion_Registered_After_The_Scan(string ns, string method)
    {
        var startup = $$"""
            using DSoftStudio.Mediator;
            using {{ns}};
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.PrecompilePipelines();
                    services.{{method}}();
                }
            }
            """;

        // The scan has already decided that no chain is needed, so the behavior this call registers
        // never runs. Nothing else catches it: the descriptor is added inside the companion's own
        // method, in another assembly, so there is no IPipelineBehavior argument here to notice.
        Analyze(startup).Length.ShouldBe(1);
    }

    [Theory]
    [InlineData("DSoftStudio.Mediator.FluentValidation", "AddMediatorFluentValidation")]
    [InlineData("DSoftStudio.Mediator.HybridCache", "AddMediatorHybridCache")]
    [InlineData("DSoftStudio.Mediator.OpenTelemetry", "AddMediatorInstrumentation")]
    public void Ignores_A_Companion_Registered_Before_The_Scan(string ns, string method)
    {
        var startup = $$"""
            using DSoftStudio.Mediator;
            using {{ns}};
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.{{method}}();
                    services.PrecompilePipelines();
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_A_Same_Named_Method_From_Another_Vendor()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using Contoso.Mediator.HybridCache;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.PrecompilePipelines();
                    services.AddMediatorHybridCache();
                }
            }
            """;

        // Same type name, same method name, different namespace. The rule is a Warning and users build
        // with TreatWarningsAsErrors, so matching by name alone would break somebody else's build.
        Analyze(startup).ShouldBeEmpty();
    }

    // ── TryAddEnumerable(ServiceDescriptor.X(...)) ────────────────────

    [Fact]
    public void Reports_A_Descriptor_Registered_After_The_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection.Extensions;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.PrecompilePipelines();
                    services.TryAddEnumerable(
                        ServiceDescriptor.Singleton(typeof(IPipelineBehavior<,>), typeof(OpenLogging<,>)));
                }
            }
            """;

        // The component interface is named inside a ServiceDescriptor factory call, not directly in the
        // argument list. This is the form the first-party packages themselves now use, so a rule blind
        // to it would miss the registrations it most needs to see.
        Analyze(startup).Length.ShouldBe(1);
    }

    [Fact]
    public void Reports_A_Generic_Descriptor_Registered_After_The_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection.Extensions;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.PrecompilePipelines();
                    services.TryAddEnumerable(
                        ServiceDescriptor.Scoped<IPipelineBehavior<Ping, int>, LoggingBehavior>());
                }
            }
            """;

        // The closed generic form, where the interface is a type ARGUMENT of the factory rather than a
        // typeof in its parameters.
        Analyze(startup).Length.ShouldBe(1);
    }

    [Fact]
    public void Ignores_A_Descriptor_Registered_Before_The_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection.Extensions;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator();
                    services.TryAddEnumerable(
                        ServiceDescriptor.Singleton(typeof(IPipelineBehavior<,>), typeof(OpenLogging<,>)));
                    services.PrecompilePipelines();
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }

    // ── Receivers the rule could not resolve ──────────────────────────────────────────────────
    // DSOFT010 pairs a registration with a scan of the SAME collection, by symbol. Two shapes never
    // produced a symbol at all, so the rule was blind to them rather than quiet about them.

    /// <summary>
    /// The fluent style, which is how the README writes it. Both scan methods return the collection they
    /// were handed, so the scan's receiver is the PREVIOUS INVOCATION rather than a symbol — the scan
    /// was never recorded, and no registration anywhere in the file could be reported against it.
    /// </summary>
    [Fact]
    public void Reports_A_Behavior_Registered_After_A_Fluent_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator().PrecompilePipelines();
                    services.AddTransient<IPipelineBehavior<Ping, int>, LoggingBehavior>();
                }
            }
            """;

        Analyze(startup).Length.ShouldBe(1);
    }

    /// <summary>
    /// A component registered through the builder's public constructor. Branch (a) of
    /// IsComponentRegistration exists for exactly these methods, but their receiver is the builder, so
    /// before the unwrap it could never pair with a scan on the collection.
    /// </summary>
    [Fact]
    public void Reports_A_Component_Registered_Through_A_New_Builder_After_The_Scan()
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
                    new MediatorBuilder(services).AddOpenBehavior(typeof(OpenLogging<,>));
                }
            }
            """;

        Analyze(startup).Length.ShouldBe(1);
    }

    [Fact]
    public void Ignores_A_Component_Registered_Through_A_Builder_Before_The_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    new MediatorBuilder(services).AddOpenBehavior(typeof(OpenLogging<,>));
                    services.PrecompilePipelines();
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }

    /// <summary>
    /// The false-positive guard, and the reason the unwrap resolves a symbol instead of assuming one:
    /// a builder over ANOTHER collection is not late for this scan, and the rule is a Warning that
    /// users build with <c>TreatWarningsAsErrors</c>.
    /// </summary>
    [Fact]
    public void Ignores_A_Builder_Over_A_Different_Collection()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services, IServiceCollection other)
                {
                    services.AddMediator().PrecompilePipelines();
                    new MediatorBuilder(other).AddOpenBehavior(typeof(OpenLogging<,>));
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }

    /// <summary>
    /// An accepted miss, pinned so it stays deliberate: a builder reached through a local names no
    /// collection at the call site. Which one it wraps is a dataflow question, and this rule prefers a
    /// miss to a guess. If it is ever made to resolve, this test is the one that should change.
    /// </summary>
    [Fact]
    public void Does_Not_Report_A_Builder_Held_In_A_Local()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    var builder = new MediatorBuilder(services);
                    services.PrecompilePipelines();
                    builder.AddOpenBehavior(typeof(OpenLogging<,>));
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }

    // ── The scan has to be the one that governs the component ────────────────────────────────
    // The rule used to pair a component with ANY scan. A stream behavior that follows
    // PrecompileNotifications() but precedes its own PrecompileStreams() is correctly ordered, and
    // reporting it breaks a build that had nothing wrong with it. Three registrations in this repo's
    // own tests were flagged that way the moment the receiver started resolving.

    [Fact]
    public void Ignores_A_Stream_Behavior_Registered_After_An_Unrelated_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator().PrecompileNotifications();
                    services.AddTransient<IStreamPipelineBehavior<PingStream, int>, StreamLogging>();
                    services.PrecompileStreams();
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_A_Request_Behavior_Registered_After_The_Stream_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator().PrecompileStreams();
                    services.AddTransient<IPipelineBehavior<Ping, int>, LoggingBehavior>();
                    services.PrecompilePipelines();
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }

    /// <summary>The other half: against its OWN scan the stream behavior is late, and is reported.</summary>
    [Fact]
    public void Reports_A_Stream_Behavior_Registered_After_The_Stream_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator().PrecompileStreams();
                    services.AddTransient<IStreamPipelineBehavior<PingStream, int>, StreamLogging>();
                }
            }
            """;

        Analyze(startup).Length.ShouldBe(1);
    }

    /// <summary>
    /// <c>AddMediator(configure)</c> ends by precompiling everything, so it governs both kinds — and the
    /// components inside its own lambda are on the "before" side, since the scan boundary is the
    /// containing statement's end.
    /// </summary>
    [Fact]
    public void Reports_A_Late_Configure_Lambda_Against_An_Earlier_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator().PrecompilePipelines();
                    services.AddMediator(b => b.AddOpenBehavior(typeof(OpenLogging<,>)));
                }
            }
            """;

        Analyze(startup).Length.ShouldBe(1);
    }

    [Fact]
    public void Ignores_A_Configure_Lambda_That_Is_The_First_Scan()
    {
        const string startup = """
            using DSoftStudio.Mediator;
            using DSoftStudio.Mediator.Abstractions;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddMediator(b => b.AddOpenBehavior(typeof(OpenLogging<,>)));
                }
            }
            """;

        Analyze(startup).ShouldBeEmpty();
    }
}
