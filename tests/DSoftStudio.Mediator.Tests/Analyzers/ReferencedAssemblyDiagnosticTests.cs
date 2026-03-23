// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Analyzers;

/// <summary>
/// Verifies that <see cref="DependencyInjectionGenerator"/> emits DSOFT005 when
/// an external assembly contains an <c>internal</c> handler that is not accessible
/// from the consuming compilation (no <c>[InternalsVisibleTo]</c>).
/// <para>
/// The test creates a three-assembly scenario in-memory:
/// <list type="number">
///   <item><c>DSoftStudio.Mediator.Abstractions</c> — interfaces (separate assembly so
///         <c>ReferencesAbstractions</c> returns <c>true</c> for the external lib)</item>
///   <item><c>ExternalLib</c> — handler classes referencing Abstractions</item>
///   <item><c>ConsumerApp</c> — references both and runs the generator</item>
/// </list>
/// </para>
/// </summary>
public class ReferencedAssemblyDiagnosticTests
{
    /// <summary>
    /// Minimal DSoftStudio.Mediator.Abstractions interfaces.
    /// </summary>
    private const string AbstractionsSource = """
        namespace DSoftStudio.Mediator.Abstractions
        {
            public interface IRequest<out TResponse> { }

            public interface IRequestHandler<in TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                System.Threading.Tasks.ValueTask<TResponse> Handle(
                    TRequest request, System.Threading.CancellationToken ct);
            }

            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class MediatorHandlerRegistrationAttribute : System.Attribute
            {
                public MediatorHandlerRegistrationAttribute(System.Type serviceType, System.Type implementationType) { }
            }
        }
        """;

    /// <summary>
    /// Stub DI types so the generated code compiles.
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

    private static readonly MetadataReference RuntimeRef =
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

    /// <summary>
    /// Compiles the Abstractions source into a standalone assembly named
    /// <c>DSoftStudio.Mediator.Abstractions</c> so that <c>ReferencesAbstractions</c>
    /// finds it via <c>module.ReferencedAssemblySymbols</c>.
    /// </summary>
    private static MetadataReference BuildAbstractionsAssembly()
    {
        var compilation = CSharpCompilation.Create(
            "DSoftStudio.Mediator.Abstractions",
            new[] { CSharpSyntaxTree.ParseText(AbstractionsSource) },
            new[] { RuntimeRef },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return EmitToReference(compilation, "DSoftStudio.Mediator.Abstractions");
    }

    /// <summary>
    /// Compiles an external library that references the standalone Abstractions assembly.
    /// </summary>
    private static MetadataReference BuildExternalLibrary(
        MetadataReference abstractionsRef,
        string externalSource,
        string assemblyName = "ExternalLib",
        string? internalsVisibleTo = null)
    {
        var sources = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(externalSource, path: "ExternalHandlers.cs"),
        };

        if (internalsVisibleTo is not null)
        {
            sources.Add(CSharpSyntaxTree.ParseText(
                $"[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"{internalsVisibleTo}\")]",
                path: "AssemblyInfo.cs"));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            sources,
            new MetadataReference[] { RuntimeRef, abstractionsRef },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return EmitToReference(compilation, assemblyName);
    }

    private static GeneratorRunResult RunGeneratorWithExternalLib(
        MetadataReference abstractionsRef,
        MetadataReference externalRef)
    {
        var syntaxTrees = new SyntaxTree[]
        {
            CSharpSyntaxTree.ParseText(DependencyInjectionStubSource, path: "DI.cs"),
            CSharpSyntaxTree.ParseText("class Placeholder { }", path: "Consumer.cs"),
        };

        var compilation = CSharpCompilation.Create(
            "ConsumerApp",
            syntaxTrees,
            new MetadataReference[] { RuntimeRef, abstractionsRef, externalRef },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DependencyInjectionGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new IIncrementalGenerator[] { generator }
                .Select(GeneratorExtensions.AsSourceGenerator));

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Results.Single();
    }

    private static MetadataReference EmitToReference(CSharpCompilation compilation, string label)
    {
        using var ms = new System.IO.MemoryStream();
        var emitResult = compilation.Emit(ms);
        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new InvalidOperationException($"Failed to emit '{label}':\n{errors}");
        }

        ms.Position = 0;
        return MetadataReference.CreateFromImage(ms.ToArray());
    }

    // ── DSOFT005: Internal handler in external assembly ──────────

    [Fact]
    public void Emits_DSOFT005_When_External_Handler_Is_Internal()
    {
        const string externalSource = """
            using DSoftStudio.Mediator.Abstractions;

            public class ExternalQuery : IRequest<string> { }

            internal class InternalHandler : IRequestHandler<ExternalQuery, string>
            {
                public System.Threading.Tasks.ValueTask<string> Handle(
                    ExternalQuery r, System.Threading.CancellationToken ct) => default;
            }
            """;

        var abstractionsRef = BuildAbstractionsAssembly();
        var externalRef = BuildExternalLibrary(abstractionsRef, externalSource);
        var result = RunGeneratorWithExternalLib(abstractionsRef, externalRef);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT005");
        var diag = result.Diagnostics.First(d => d.Id == "DSOFT005");
        diag.GetMessage().ShouldContain("InternalHandler");
        diag.GetMessage().ShouldContain("ExternalLib");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT005_When_External_Handler_Is_Public()
    {
        const string externalSource = """
            using DSoftStudio.Mediator.Abstractions;

            public class ExternalQuery : IRequest<string> { }

            public class PublicHandler : IRequestHandler<ExternalQuery, string>
            {
                public System.Threading.Tasks.ValueTask<string> Handle(
                    ExternalQuery r, System.Threading.CancellationToken ct) => default;
            }
            """;

        var abstractionsRef = BuildAbstractionsAssembly();
        var externalRef = BuildExternalLibrary(abstractionsRef, externalSource);
        var result = RunGeneratorWithExternalLib(abstractionsRef, externalRef);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT005");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT005_When_InternalsVisibleTo_Grants_Access()
    {
        const string externalSource = """
            using DSoftStudio.Mediator.Abstractions;

            public class ExternalQuery : IRequest<string> { }

            internal class InternalHandler : IRequestHandler<ExternalQuery, string>
            {
                public System.Threading.Tasks.ValueTask<string> Handle(
                    ExternalQuery r, System.Threading.CancellationToken ct) => default;
            }
            """;

        var abstractionsRef = BuildAbstractionsAssembly();
        var externalRef = BuildExternalLibrary(
            abstractionsRef, externalSource,
            internalsVisibleTo: "ConsumerApp");

        var result = RunGeneratorWithExternalLib(abstractionsRef, externalRef);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT005");
    }

    [Fact]
    public void Emits_DSOFT005_When_InternalsVisibleTo_Targets_Wrong_Assembly()
    {
        const string externalSource = """
            using DSoftStudio.Mediator.Abstractions;

            public class ExternalQuery : IRequest<string> { }

            internal class InternalHandler : IRequestHandler<ExternalQuery, string>
            {
                public System.Threading.Tasks.ValueTask<string> Handle(
                    ExternalQuery r, System.Threading.CancellationToken ct) => default;
            }
            """;

        var abstractionsRef = BuildAbstractionsAssembly();
        // Grant access to a different assembly — not our consumer
        var externalRef = BuildExternalLibrary(
            abstractionsRef, externalSource,
            internalsVisibleTo: "SomeOtherProject");

        var result = RunGeneratorWithExternalLib(abstractionsRef, externalRef);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT005");
    }

    // ── DSOFT001: No handler for external request type ───────────

    [Fact]
    public void Emits_DSOFT001_When_External_Request_Has_No_Handler()
    {
        const string externalSource = """
            using DSoftStudio.Mediator.Abstractions;

            public class OrphanExternalQuery : IRequest<string> { }
            """;

        var abstractionsRef = BuildAbstractionsAssembly();
        var externalRef = BuildExternalLibrary(abstractionsRef, externalSource);
        var result = RunGeneratorWithExternalLib(abstractionsRef, externalRef);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT001");
        var diag = result.Diagnostics.First(d => d.Id == "DSOFT001");
        diag.GetMessage().ShouldContain("OrphanExternalQuery");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT001_When_External_Request_Has_Handler()
    {
        const string externalSource = """
            using DSoftStudio.Mediator.Abstractions;

            public class ExternalQuery : IRequest<string> { }

            public class ExternalHandler : IRequestHandler<ExternalQuery, string>
            {
                public System.Threading.Tasks.ValueTask<string> Handle(
                    ExternalQuery r, System.Threading.CancellationToken ct) => default;
            }
            """;

        var abstractionsRef = BuildAbstractionsAssembly();
        var externalRef = BuildExternalLibrary(abstractionsRef, externalSource);
        var result = RunGeneratorWithExternalLib(abstractionsRef, externalRef);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT001");
    }
}
