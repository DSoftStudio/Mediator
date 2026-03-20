// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Mocking;

/// <summary>
/// Regression tests for the 3-project architecture:
/// <list type="bullet">
///   <item><b>Host.Application</b> — references only <c>DSoftStudio.Mediator.Abstractions</c>, defines handlers</item>
///   <item><b>Host</b> — references <c>Host.Application</c> + <c>DSoftStudio.Mediator</c> (with generators)</item>
///   <item><b>Host.Tests</b> — mocks <c>ISender</c> with a test double</item>
/// </list>
/// Verifies that the source generator discovers handlers from the Abstractions-only assembly
/// (Phase 2 type-based scanning) and generates mock-safe typed extensions.
/// </summary>
public class AbstractionsOnlyProjectTests
{
    /// <summary>
    /// Core references needed to compile code that uses ValueTask, CancellationToken, etc.
    /// </summary>
    private static readonly MetadataReference[] s_coreReferences =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ValueTask<>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Threading.CancellationToken).Assembly.Location),
    ];

    /// <summary>
    /// Builds the Abstractions assembly in-memory (IRequest, IRequestHandler, ISender, etc.)
    /// to simulate the real <c>DSoftStudio.Mediator.Abstractions</c> package.
    /// </summary>
    private static MetadataReference BuildAbstractionsAssembly()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;

            namespace DSoftStudio.Mediator.Abstractions
            {
                public interface IRequest<out TResponse> { }

                public interface IRequestHandler<in TRequest, TResponse>
                    where TRequest : IRequest<TResponse>
                {
                    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
                }

                public interface ISender
                {
                    ValueTask<TResponse> Send<TRequest, TResponse>(
                        TRequest request,
                        CancellationToken cancellationToken = default)
                        where TRequest : IRequest<TResponse>;
                }

                public interface IPublisher { }

                public interface IMediator : ISender, IPublisher { }
            }
            """;

        var compilation = CSharpCompilation.Create(
            "DSoftStudio.Mediator.Abstractions",
            [CSharpSyntaxTree.ParseText(source)],
            s_coreReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
            throw new InvalidOperationException(
                "Failed to emit Abstractions assembly: " +
                string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        ms.Position = 0;
        return MetadataReference.CreateFromImage(ms.ToArray());
    }

    /// <summary>
    /// Builds the "Host.Application" assembly that references ONLY Abstractions,
    /// containing a command + handler (simulating a domain/application-layer project).
    /// </summary>
    private static MetadataReference BuildApplicationAssembly(MetadataReference abstractionsRef)
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace Host.Application
            {
                public sealed record RunTaskCommand(string Name) : IRequest<int>;

                public sealed class RunTaskCommandHandler : IRequestHandler<RunTaskCommand, int>
                {
                    public ValueTask<int> Handle(RunTaskCommand request, CancellationToken cancellationToken)
                        => new(42);
                }
            }
            """;

        var references = s_coreReferences.Append(abstractionsRef).ToArray();

        var compilation = CSharpCompilation.Create(
            "Host.Application",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
            throw new InvalidOperationException(
                "Failed to emit Host.Application assembly: " +
                string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        ms.Position = 0;
        return MetadataReference.CreateFromImage(ms.ToArray());
    }

    /// <summary>
    /// Creates the "Host" compilation that references both Abstractions and Host.Application,
    /// then runs <see cref="MediatorExtensionsGenerator"/> — simulating the real Host project.
    /// </summary>
    private static GeneratorRunResult RunExtensionsGenerator(
        MetadataReference abstractionsRef,
        MetadataReference applicationRef)
    {
        // The Host project has minimal source — handlers live in Host.Application.
        const string hostSource = """
            namespace Host { class Program { } }
            """;

        var references = s_coreReferences
            .Append(abstractionsRef)
            .Append(applicationRef)
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "Host",
            [CSharpSyntaxTree.ParseText(hostSource)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MediatorExtensionsGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);

        return driver.GetRunResult().Results.Single();
    }

    // ── Tests ────────────────────────────────────────────────────────

    [Fact]
    public void Phase2_Scanner_Discovers_Handlers_From_AbstractionsOnly_Assembly()
    {
        var abstractionsRef = BuildAbstractionsAssembly();
        var applicationRef = BuildApplicationAssembly(abstractionsRef);
        var result = RunExtensionsGenerator(abstractionsRef, applicationRef);

        // The generator should produce at least one source (MediatorExtensions.g.cs).
        result.GeneratedSources.ShouldNotBeEmpty(
            "MediatorExtensionsGenerator should produce output when handlers exist in a referenced Abstractions-only assembly.");
    }

    [Fact]
    public void TypedExtension_Generated_For_RunTaskCommand()
    {
        var abstractionsRef = BuildAbstractionsAssembly();
        var applicationRef = BuildApplicationAssembly(abstractionsRef);
        var result = RunExtensionsGenerator(abstractionsRef, applicationRef);

        var generatedCode = result.GeneratedSources
            .Select(s => s.SourceText.ToString())
            .Aggregate(string.Empty, (a, b) => a + b);

        // A typed Send extension should be generated for RunTaskCommand discovered via Phase 2 scanner.
        generatedCode.ShouldContain("RunTaskCommand");
    }

    [Fact]
    public void TypedExtension_Uses_VirtualDispatch_Not_Cast()
    {
        var abstractionsRef = BuildAbstractionsAssembly();
        var applicationRef = BuildApplicationAssembly(abstractionsRef);
        var result = RunExtensionsGenerator(abstractionsRef, applicationRef);

        var generatedCode = result.GeneratedSources
            .Select(s => s.SourceText.ToString())
            .Aggregate(string.Empty, (a, b) => a + b);

        // Typed extensions must delegate to sender.Send<TRequest, TResponse>() (virtual dispatch, no cast).
        generatedCode.ShouldContain("sender.Send<");
    }

    [Fact]
    public void SendObject_Has_MockSafe_Guard()
    {
        var abstractionsRef = BuildAbstractionsAssembly();
        var applicationRef = BuildApplicationAssembly(abstractionsRef);
        var result = RunExtensionsGenerator(abstractionsRef, applicationRef);

        var generatedCode = result.GeneratedSources
            .Select(s => s.SourceText.ToString())
            .Aggregate(string.Empty, (a, b) => a + b);

        // Send(object) must use 'is not IServiceProviderAccessor' guard for mock safety.
        generatedCode.ShouldContain("is not");

        // The error message should guide users to the typed overload.
        generatedCode.ShouldContain("explicit generic overload");
    }

    [Fact]
    public void SendObject_DoesNot_Use_Hard_Cast()
    {
        var abstractionsRef = BuildAbstractionsAssembly();
        var applicationRef = BuildApplicationAssembly(abstractionsRef);
        var result = RunExtensionsGenerator(abstractionsRef, applicationRef);

        var generatedCode = result.GeneratedSources
            .Select(s => s.SourceText.ToString())
            .Aggregate(string.Empty, (a, b) => a + b);

        // Ensure there is NO hard castclass pattern like ((IServiceProviderAccessor)sender).
        // A hard cast would throw InvalidCastException on mock ISender instances.
        generatedCode.ShouldNotContain("((global::DSoftStudio.Mediator.IServiceProviderAccessor)sender)");
    }

    [Fact]
    public void No_Diagnostics_Emitted_By_ExtensionsGenerator()
    {
        var abstractionsRef = BuildAbstractionsAssembly();
        var applicationRef = BuildApplicationAssembly(abstractionsRef);
        var result = RunExtensionsGenerator(abstractionsRef, applicationRef);

        result.Diagnostics.ShouldBeEmpty(
            "MediatorExtensionsGenerator should not emit diagnostics when discovering handlers from an Abstractions-only assembly.");
    }
}
