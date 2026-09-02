// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Analyzers;

/// <summary>
/// A handler that generated code cannot name must be SKIPPED by discovery and reported as
/// <c>DSOFT009</c> — never emitted, and never dropped in silence.
/// <para>
/// Discovery used to reject only <c>file</c> types, so a <c>private</c> nested handler (an ordinary
/// shape inside a test fixture) was registered anyway and the generated file failed to compile with
/// CS0122 — 502 of them, in files the user cannot edit. The
/// <c>Emits_No_Registration_For_*</c> tests below cover the fix, and
/// <see cref="Emits_DSOFT009_For_PrivateNested_Handlers"/> covers the report that replaces the noise.
/// </para>
/// </summary>
public class HandlerAccessibilityDiagnosticTests
{
    private const string AbstractionsSource = """
        namespace DSoftStudio.Mediator.Abstractions
        {
            public interface IRequest<out TResponse> { }
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
        }
        """;

    /// <summary>All three handler kinds, nested privately inside a public class.</summary>
    private const string PrivateNestedHandlers = """
        using DSoftStudio.Mediator.Abstractions;

        public class Fixture
        {
            private class NestedPing : IRequest<int> { }

            private class NestedPingHandler : IRequestHandler<NestedPing, int>
            {
                public System.Threading.Tasks.ValueTask<int> Handle(
                    NestedPing r, System.Threading.CancellationToken ct) => default;
            }

            private class NestedStream : IStreamRequest<int> { }

            private class NestedStreamHandler : IStreamRequestHandler<NestedStream, int>
            {
                public System.Collections.Generic.IAsyncEnumerable<int> Handle(
                    NestedStream r, System.Threading.CancellationToken ct) => null!;
            }

            private class NestedNote : INotification { }

            private class NestedNoteHandler : INotificationHandler<NestedNote>
            {
                public System.Threading.Tasks.Task Handle(
                    NestedNote n, System.Threading.CancellationToken ct) => null!;
            }
        }
        """;

    private static GeneratorRunResult Run(IIncrementalGenerator generator, string userSource)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [
                CSharpSyntaxTree.ParseText(AbstractionsSource, path: "Abstractions.cs"),
                CSharpSyntaxTree.ParseText(userSource, path: "UserCode.cs"),
            ],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator }.Select(GeneratorExtensions.AsSourceGenerator));

        return driver.RunGenerators(compilation).GetRunResult().Results.Single();
    }

    private static string GeneratedText(GeneratorRunResult result)
        => string.Concat(result.GeneratedSources.Select(s => s.SourceText.ToString()));

    [Fact]
    public void Emits_DSOFT009_For_PrivateNested_Handlers()
    {
        var result = Run(new HandlerAccessibilityAnalyzer(), PrivateNestedHandlers);

        var reported = result.Diagnostics.Where(d => d.Id == "DSOFT009").ToArray();

        reported.Length.ShouldBe(3);
        reported.Select(d => d.GetMessage()).ShouldContain(m => m.Contains("request handler"));
        reported.Select(d => d.GetMessage()).ShouldContain(m => m.Contains("stream handler"));
        reported.Select(d => d.GetMessage()).ShouldContain(m => m.Contains("notification handler"));
        reported.ShouldAllBe(d => d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Ignores_PrivateNested_Types_That_Are_Not_Handlers()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class Fixture
            {
                private class JustAHelper { }
            }
            """;

        Run(new HandlerAccessibilityAnalyzer(), source)
            .Diagnostics.ShouldNotContain(d => d.Id == "DSOFT009");
    }

    [Fact]
    public void Ignores_FileLocal_Handlers()
    {
        // `file` types are deliberately invisible; the user already knows.
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            file class FilePing : IRequest<int> { }

            file class FilePingHandler : IRequestHandler<FilePing, int>
            {
                public System.Threading.Tasks.ValueTask<int> Handle(
                    FilePing r, System.Threading.CancellationToken ct) => default;
            }
            """;

        Run(new HandlerAccessibilityAnalyzer(), source)
            .Diagnostics.ShouldNotContain(d => d.Id == "DSOFT009");
    }

    [Fact]
    public void Emits_No_Registration_For_PrivateNested_Handlers()
    {
        // The fix itself: nothing the generated file writes may name these types.
        var generated = GeneratedText(Run(new DependencyInjectionGenerator(), PrivateNestedHandlers));

        generated.ShouldNotContain("NestedPingHandler");
        generated.ShouldNotContain("NestedStreamHandler");
        generated.ShouldNotContain("NestedNoteHandler");
    }

    [Fact]
    public void Emits_No_Extensions_Or_Pipeline_For_PrivateNested_Handlers()
    {
        GeneratedText(Run(new MediatorExtensionsGenerator(), PrivateNestedHandlers))
            .ShouldNotContain("NestedPing");

        GeneratedText(Run(new MediatorPipelineGenerator(), PrivateNestedHandlers))
            .ShouldNotContain("NestedPing");

        GeneratedText(Run(new StreamGenerator(), PrivateNestedHandlers))
            .ShouldNotContain("NestedStream");
    }

    [Fact]
    public void Still_Registers_An_Internal_Nested_Handler()
    {
        // The guard must not overreach: generated code lives in the same assembly, so `internal`
        // (and a non-generic enclosing type) is perfectly nameable.
        const string source = """
            using DSoftStudio.Mediator.Abstractions;

            public class Fixture
            {
                internal class InternalPing : IRequest<int> { }

                internal class InternalPingHandler : IRequestHandler<InternalPing, int>
                {
                    public System.Threading.Tasks.ValueTask<int> Handle(
                        InternalPing r, System.Threading.CancellationToken ct) => default;
                }
            }
            """;

        GeneratedText(Run(new DependencyInjectionGenerator(), source))
            .ShouldContain("InternalPingHandler");

        Run(new HandlerAccessibilityAnalyzer(), source)
            .Diagnostics.ShouldNotContain(d => d.Id == "DSOFT009");
    }
}
