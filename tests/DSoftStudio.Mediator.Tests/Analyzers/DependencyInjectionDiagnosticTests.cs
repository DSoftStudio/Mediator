// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Analyzers;

/// <summary>
/// Verifies that <see cref="DependencyInjectionGenerator"/> emits the correct diagnostics:
/// <list type="bullet">
///   <item>DSOFT001 — no handler for request type</item>
///   <item>DSOFT002 — duplicate request handler</item>
///   <item>DSOFT003 — duplicate stream handler</item>
/// </list>
/// </summary>
public class DependencyInjectionDiagnosticTests
{
    /// <summary>
    /// Minimal DSoftStudio.Mediator.Abstractions interfaces embedded as source so the
    /// semantic model resolves them by namespace + MetadataName.
    /// </summary>
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

    /// <summary>
    /// Stub Microsoft.Extensions.DependencyInjection types so the generated code compiles.
    /// </summary>
    private const string DependencyInjectionStubSource = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection : System.Collections.Generic.IList<ServiceDescriptor> { }
            public class ServiceDescriptor { }
            public static class ServiceCollectionServiceExtensions
            {
                public static IServiceCollection AddTransient<TService, TImpl>(IServiceCollection s) where TImpl : class, TService => s;
                public static IServiceCollection AddSingleton<TService, TImpl>(IServiceCollection s) where TImpl : class, TService => s;
            }
            public static class ServiceCollectionContainerBuilderExtensions
            {
                public static object BuildServiceProvider(IServiceCollection s) => new object();
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

    private static GeneratorRunResult RunGenerator(string userSource)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(AbstractionsSource, path: "Abstractions.cs"),
            CSharpSyntaxTree.ParseText(DependencyInjectionStubSource, path: "DI.cs"),
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

        var generator = new DependencyInjectionGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new IIncrementalGenerator[] { generator }
                .Select(GeneratorExtensions.AsSourceGenerator));

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Results.Single();
    }

    // ── DSOFT002: Duplicate request handler ──────────────────────

    [Fact]
    public void Emits_DSOFT002_When_Duplicate_RequestHandlers_Exist()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class MyRequest : IRequest<string> { }

            public class HandlerA : IRequestHandler<MyRequest, string>
            {
                public System.Threading.Tasks.ValueTask<string> Handle(
                    MyRequest r, System.Threading.CancellationToken ct) => default;
            }

            public class HandlerB : IRequestHandler<MyRequest, string>
            {
                public System.Threading.Tasks.ValueTask<string> Handle(
                    MyRequest r, System.Threading.CancellationToken ct) => default;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT002");
        var diag = result.Diagnostics.First(d => d.Id == "DSOFT002");
        diag.GetMessage().ShouldContain("HandlerA");
        diag.GetMessage().ShouldContain("HandlerB");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT002_For_Single_RequestHandler()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class MyRequest : IRequest<string> { }

            public class MyHandler : IRequestHandler<MyRequest, string>
            {
                public System.Threading.Tasks.ValueTask<string> Handle(
                    MyRequest r, System.Threading.CancellationToken ct) => default;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT002");
    }

    // ── DSOFT003: Duplicate stream handler ───────────────────────

    [Fact]
    public void Emits_DSOFT003_When_Duplicate_StreamHandlers_Exist()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class MyStream : IStreamRequest<string> { }

            public class StreamHandlerA : IStreamRequestHandler<MyStream, string>
            {
                public System.Collections.Generic.IAsyncEnumerable<string> Handle(
                    MyStream r, System.Threading.CancellationToken ct) => default!;
            }

            public class StreamHandlerB : IStreamRequestHandler<MyStream, string>
            {
                public System.Collections.Generic.IAsyncEnumerable<string> Handle(
                    MyStream r, System.Threading.CancellationToken ct) => default!;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT003");
        var diag = result.Diagnostics.First(d => d.Id == "DSOFT003");
        diag.GetMessage().ShouldContain("StreamHandlerA");
        diag.GetMessage().ShouldContain("StreamHandlerB");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT003_For_Single_StreamHandler()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class MyStream : IStreamRequest<string> { }

            public class MyStreamHandler : IStreamRequestHandler<MyStream, string>
            {
                public System.Collections.Generic.IAsyncEnumerable<string> Handle(
                    MyStream r, System.Threading.CancellationToken ct) => default!;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT003");
    }

    // ── Notification handlers: multiple is valid (no diagnostic) ─

    [Fact]
    public void Does_Not_Emit_Diagnostic_For_Multiple_NotificationHandlers()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class MyEvent : INotification { }

            public class NotifHandlerA : INotificationHandler<MyEvent>
            {
                public System.Threading.Tasks.Task Handle(
                    MyEvent n, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
            }

            public class NotifHandlerB : INotificationHandler<MyEvent>
            {
                public System.Threading.Tasks.Task Handle(
                    MyEvent n, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT002");
        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT003");
    }

    // ── No handlers at all: valid state (no diagnostic) ─────────

    [Fact]
    public void Does_Not_Emit_Diagnostic_When_No_Handlers()
    {
        const string source = """
            public class Empty { }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT002");
        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT003");
    }

    // ── DSOFT001: No handler for request type ────────────────────

    [Fact]
    public void Emits_DSOFT001_When_Request_Has_No_Handler()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class OrphanRequest : IRequest<string> { }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT001");
        var diag = result.Diagnostics.First(d => d.Id == "DSOFT001");
        diag.GetMessage().ShouldContain("OrphanRequest");
    }

    [Fact]
    public void Emits_DSOFT001_When_ICommand_Has_No_Handler()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class OrphanCommand : ICommand<int> { }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT001");
        var diag = result.Diagnostics.First(d => d.Id == "DSOFT001");
        diag.GetMessage().ShouldContain("OrphanCommand");
    }

    [Fact]
    public void Emits_DSOFT001_When_IQuery_Has_No_Handler()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class OrphanQuery : IQuery<string> { }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT001");
        var diag = result.Diagnostics.First(d => d.Id == "DSOFT001");
        diag.GetMessage().ShouldContain("OrphanQuery");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT001_When_Request_Has_Handler()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class MyRequest : IRequest<string> { }

            public class MyHandler : IRequestHandler<MyRequest, string>
            {
                public System.Threading.Tasks.ValueTask<string> Handle(
                    MyRequest r, System.Threading.CancellationToken ct) => default;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT001");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT001_For_Self_Handling_Request()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class SelfRequest : IRequest<string>
            {
                public static string Execute(SelfRequest request) => "ok";
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT001");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT001_For_Abstract_Request_Type()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public abstract class BaseRequest : IRequest<string> { }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT001");
    }
}
