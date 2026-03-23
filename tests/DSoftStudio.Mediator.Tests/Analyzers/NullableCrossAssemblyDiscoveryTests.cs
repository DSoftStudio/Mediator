// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Analyzers;

/// <summary>
/// Regression tests for the CS8631 nullable constraint mismatch bug in cross-assembly
/// handler discovery.
/// <para>
/// When <c>typeof()</c> is used inside C# assembly attributes, nullable reference type
/// annotations are stripped at the IL level — e.g., <c>typeof(User?)</c> becomes
/// <c>typeof(User)</c> in metadata. The fix in
/// <see cref="ReferencedAssemblyScanner"/> re-resolves service types from
/// <c>AllInterfaces</c> to recover the nullable annotation.
/// </para>
/// <para>
/// Two discovery paths are tested:
/// <list type="number">
///   <item><b>Phase 1 (attribute-based):</b> ExternalLib WITH generators emits
///     <c>[MediatorHandlerRegistration]</c> attributes — <c>typeof()</c> strips nullable.
///     The fix re-resolves the service type from <c>AllInterfaces</c>.</item>
///   <item><b>Phase 2 (type-based):</b> ExternalLib with only Abstractions — type scanning
///     reads <c>AllInterfaces</c> directly, which preserves nullable via PE metadata
///     (<c>NullableAttribute</c>).</item>
/// </list>
/// </para>
/// </summary>
public class NullableCrossAssemblyDiscoveryTests
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

    // ── Phase 1: Attribute-based discovery ───────────────────────────

    /// <summary>
    /// <b>Scenario 1 — Phase 1 (attribute-based discovery):</b>
    /// <para>
    /// <c>ExternalLib</c> was compiled with DSoftStudio.Mediator generators, so it carries
    /// <c>[assembly: MediatorHandlerRegistration(typeof(IRequestHandler&lt;GetUser, User?&gt;),
    /// typeof(GetUserHandler))]</c> in source. However, <c>typeof()</c> strips nullable
    /// reference type annotations at the IL level — the attribute metadata stores
    /// <c>IRequestHandler&lt;GetUser, User&gt;</c> (no <c>?</c>).
    /// </para>
    /// <para>
    /// The fix in <c>CollectHandlersFromAttributes</c> re-resolves the service type from
    /// <c>implType.AllInterfaces</c>, which correctly preserves nullable annotations from
    /// PE metadata (<c>NullableAttribute</c>). This prevents CS8631 warnings and false
    /// DSOFT001 diagnostics in the consuming project.
    /// </para>
    /// </summary>
    [Fact]
    public void Phase1_Attribute_Based_Discovery_Preserves_Nullable_Response_Type()
    {
        // Simulates a project compiled WITH generators:
        //   - Handler implements IRequestHandler<GetUser, User?>
        //   - [MediatorHandlerRegistration] attribute with typeof(IRequestHandler<GetUser, User?>)
        //     → in IL, typeof() strips nullable: becomes typeof(IRequestHandler<GetUser, User>)
        const string externalSource = """
            #nullable enable
            using DSoftStudio.Mediator.Abstractions;

            [assembly: DSoftStudio.Mediator.Abstractions.MediatorHandlerRegistration(
                typeof(DSoftStudio.Mediator.Abstractions.IRequestHandler<GetUser, User?>),
                typeof(GetUserHandler))]

            public class User { }
            public class GetUser : IRequest<User?> { }
            public class GetUserHandler : IRequestHandler<GetUser, User?>
            {
                public System.Threading.Tasks.ValueTask<User?> Handle(
                    GetUser request, System.Threading.CancellationToken ct) => default;
            }
            """;

        var abstractionsRef = BuildAbstractionsAssembly();
        var externalRef = BuildExternalLibrary(abstractionsRef, externalSource);
        var result = RunGeneratorOnConsumer(abstractionsRef, externalRef);

        // Phase 1 reads attributes → fix re-resolves nullable from AllInterfaces
        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT001");

        // Verify the generated DI registration preserves the nullable annotation
        var generatedSource = result.GeneratedSources
            .Single(s => s.HintName == "MediatorServiceRegistry.g.cs")
            .SourceText.ToString();

        generatedSource.ShouldContain("global::User?>");
    }

    // ── Phase 2: Type-based discovery ────────────────────────────────

    /// <summary>
    /// <b>Scenario 2 — Phase 2 (type-based discovery):</b>
    /// <para>
    /// <c>ExternalLib</c> references only <c>DSoftStudio.Mediator.Abstractions</c> (no generators),
    /// so it has <em>no</em> <c>[MediatorHandlerRegistration]</c> attributes. Phase 2 scans
    /// <c>AllInterfaces</c> on exported types, which preserves nullable annotations from PE
    /// metadata (<c>NullableAttribute</c>).
    /// </para>
    /// </summary>
    [Fact]
    public void Phase2_Type_Based_Discovery_Preserves_Nullable_Response_Type()
    {
        // Simulates a project referencing ONLY Abstractions (no generators).
        // No assembly attribute → Phase 2 type-based discovery kicks in.
        const string externalSource = """
            #nullable enable
            using DSoftStudio.Mediator.Abstractions;

            public class User { }
            public class GetUser : IRequest<User?> { }
            public class GetUserHandler : IRequestHandler<GetUser, User?>
            {
                public System.Threading.Tasks.ValueTask<User?> Handle(
                    GetUser request, System.Threading.CancellationToken ct) => default;
            }
            """;

        var abstractionsRef = BuildAbstractionsAssembly();
        var externalRef = BuildExternalLibrary(abstractionsRef, externalSource);
        var result = RunGeneratorOnConsumer(abstractionsRef, externalRef);

        // Phase 2 reads AllInterfaces → nullable preserved from PE metadata
        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT001");

        // Verify the generated DI registration preserves the nullable annotation
        var generatedSource = result.GeneratedSources
            .Single(s => s.HintName == "MediatorServiceRegistry.g.cs")
            .SourceText.ToString();

        generatedSource.ShouldContain("global::User?>");
    }

    // ── Infrastructure helpers ───────────────────────────────────────

    private static MetadataReference BuildAbstractionsAssembly()
    {
        var compilation = CSharpCompilation.Create(
            "DSoftStudio.Mediator.Abstractions",
            new[] { CSharpSyntaxTree.ParseText(AbstractionsSource) },
            new[] { RuntimeRef },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return EmitToReference(compilation, "DSoftStudio.Mediator.Abstractions");
    }

    private static MetadataReference BuildExternalLibrary(
        MetadataReference abstractionsRef,
        string externalSource)
    {
        var compilation = CSharpCompilation.Create(
            "ExternalLib",
            new[] { CSharpSyntaxTree.ParseText(externalSource, path: "ExternalHandlers.cs") },
            new MetadataReference[] { RuntimeRef, abstractionsRef },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        return EmitToReference(compilation, "ExternalLib");
    }

    private static GeneratorRunResult RunGeneratorOnConsumer(
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
}
