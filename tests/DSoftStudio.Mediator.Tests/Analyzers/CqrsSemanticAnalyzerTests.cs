// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Analyzers;

/// <summary>
/// Verifies that <see cref="CqrsSemanticAnalyzer"/> emits DSOFT006 when a concrete type
/// implements <c>IRequest&lt;T&gt;</c> directly, and does NOT emit it when the type uses
/// the CQRS marker interfaces <c>ICommand&lt;T&gt;</c> or <c>IQuery&lt;T&gt;</c>.
/// </summary>
public class CqrsSemanticAnalyzerTests
{
    /// <summary>
    /// Minimal DSoftStudio.Mediator.Abstractions interfaces embedded as source so the
    /// semantic model resolves them by namespace + MetadataName — exactly as the real
    /// analyzer does at compile time.
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
        }
        """;

    private static GeneratorRunResult RunAnalyzer(string userSource)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(AbstractionsSource, path: "Abstractions.cs"),
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

        var generator = new CqrsSemanticAnalyzer();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new IIncrementalGenerator[] { generator }
                .Select(GeneratorExtensions.AsSourceGenerator));

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Results.Single();
    }

    // ── Positive: should emit DSOFT006 ───────────────────────────

    [Fact]
    public void Emits_DSOFT006_When_IRequest_Used_Directly()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;
            public class MyRequest : IRequest<string> { }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT006");
    }

    [Fact]
    public void Emits_DSOFT006_For_Record_Implementing_IRequest()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;
            public record MyRecord : IRequest<int>;
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldContain(d => d.Id == "DSOFT006");
    }

    [Fact]
    public void Emits_DSOFT006_With_Correct_TypeName_And_ResponseType()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;
            public class GetOrder : IRequest<string> { }
            """;

        var result = RunAnalyzer(source);
        var diag = result.Diagnostics.Single(d => d.Id == "DSOFT006");
        var message = diag.GetMessage();

        message.ShouldContain("GetOrder");
        message.ShouldContain("string");
    }

    [Fact]
    public void Emits_DSOFT006_For_Multiple_Types()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;
            public class Req1 : IRequest<string> { }
            public class Req2 : IRequest<int> { }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.Count(d => d.Id == "DSOFT006").ShouldBe(2);
    }

    // ── Negative: should NOT emit DSOFT006 ───────────────────────

    [Fact]
    public void Does_Not_Emit_DSOFT006_For_ICommand()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;
            public class MyCommand : ICommand<string> { }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT006");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT006_For_IQuery()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;
            public class MyQuery : IQuery<string> { }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT006");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT006_For_Abstract_Class()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;
            public abstract class BaseRequest : IRequest<string> { }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT006");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT006_For_IStreamRequest()
    {
        const string source = """
            using DSoftStudio.Mediator.Abstractions;
            public class MyStream : IStreamRequest<string> { }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT006");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT006_For_Unrelated_Interface()
    {
        const string source = """
            public interface IMyOwn<T> { }
            public class Foo : IMyOwn<string> { }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT006");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT006_For_Class_Without_BaseList()
    {
        const string source = """
            public class PlainClass { }
            """;

        var result = RunAnalyzer(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "DSOFT006");
    }
}
