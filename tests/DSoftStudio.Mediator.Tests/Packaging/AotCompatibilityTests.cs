// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using DSoftStudio.Mediator.Generators;
using DSoftStudio.Mediator.Tests.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Packaging;

/// <summary>
/// Guards the NativeAOT claim the packages make.
/// <para>
/// Four csproj files set <c>IsAotCompatible</c>, the Abstractions package advertises
/// <c>native-aot</c> in its tags, and <c>RequestObjectDispatch</c> / <c>NotificationObjectDispatch</c>
/// say in their own documentation that there is "no MakeGenericType, no Expression.Compile, no
/// reflection". Until this file existed, nothing checked any of it: a single reflective call added
/// to a dispatch path would keep every test green, keep the analyzers quiet in most shapes, and only
/// surface as an IL3050 in a consumer's own AOT publish — or, worse, as a crash at their startup.
/// </para>
/// <para>
/// Verified against a real NativeAOT publish (win-x64, ILC + MSVC link): the native binary runs and
/// dispatches correctly, and cold start measured 8.3 ms against 78.9 ms for the same source on the
/// JIT. That publish takes minutes and a C++ toolchain, so it cannot live in the test run; these
/// checks are the fast proxy for it and catch the regression that would break it.
/// </para>
/// </summary>
public class AotCompatibilityTests
{
    /// <summary>
    /// (declaring type, member) pairs NativeAOT either cannot compile or can only support by
    /// rooting code the trimmer would otherwise remove. None of them has any business on a
    /// source-generated dispatch path.
    /// </summary>
    private static readonly (string Type, string Member)[] ForbiddenCalls =
    [
        ("System.Type", "MakeGenericType"),
        ("System.Reflection.MethodInfo", "MakeGenericMethod"),
        ("System.Reflection.MethodBase", "MakeGenericMethod"),
        ("System.Reflection.MethodBase", "Invoke"),
        ("System.Activator", "CreateInstance"),
        ("System.Reflection.Assembly", "GetTypes"),
        ("System.Reflection.Assembly", "get_DefinedTypes"),
        ("System.Linq.Expressions.LambdaExpression", "Compile"),
        ("System.Linq.Expressions.Expression", "Compile"),
    ];

    /// <summary>Whole namespaces that cannot exist in an AOT image at all.</summary>
    private static readonly string[] ForbiddenNamespaces =
    [
        "System.Reflection.Emit",
    ];

    [Fact]
    public void Runtime_Assembly_Calls_Nothing_NativeAot_Cannot_Compile()
        => AssertNoDynamicCode(typeof(DSoftStudio.Mediator.RequestObjectDispatch).Assembly.Location);

    [Fact]
    public void Abstractions_Assembly_Calls_Nothing_NativeAot_Cannot_Compile()
        => AssertNoDynamicCode(typeof(DSoftStudio.Mediator.Abstractions.IMediator).Assembly.Location);

    private static void AssertNoDynamicCode(string assemblyPath)
    {
        File.Exists(assemblyPath).ShouldBeTrue($"cannot inspect what is not there: {assemblyPath}");

        var offences = FindDynamicCodeReferences(assemblyPath);

        offences.ShouldBeEmpty(
            customMessage:
            $"{Path.GetFileName(assemblyPath)} references APIs NativeAOT cannot compile. " +
            "Either the call is genuinely needed — in which case the IsAotCompatible declaration and " +
            "the 'no reflection' documentation are now false and must change with it — or it is an " +
            $"accident. Found: {string.Join(", ", offences)}");
    }

    /// <summary>
    /// The generated code is where a reflective shortcut is most tempting, and it is not covered by
    /// the assembly scan above: it is compiled into the CONSUMER's assembly, not ours.
    /// </summary>
    [Fact]
    public void Generated_Dispatch_Code_Contains_No_Reflection()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record GetUser(int Id) : IRequest<string>;
            public sealed class GetUserHandler : IRequestHandler<GetUser, string>
            {
                public ValueTask<string> Handle(GetUser request, CancellationToken ct) => new("user");
            }

            public record UserChanged(int Id) : INotification;
            public sealed class UserChangedHandler : INotificationHandler<UserChanged>
            {
                public Task Handle(UserChanged notification, CancellationToken ct) => Task.CompletedTask;
            }

            public record Ticks(int Count) : IStreamRequest<int>;
            public sealed class TicksHandler : IStreamRequestHandler<Ticks, int>
            {
                public async IAsyncEnumerable<int> Handle(Ticks request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
                {
                    for (var i = 0; i < request.Count; i++) { await Task.Yield(); yield return i; }
                }
            }
            """;

        var emitted =
            GeneratorTestHarness.Run<MediatorExtensionsGenerator>(source).Result.AllSource() +
            GeneratorTestHarness.Run<MediatorPipelineGenerator>(source).Result.AllSource() +
            GeneratorTestHarness.Run<NotificationGenerator>(source).Result.AllSource() +
            GeneratorTestHarness.Run<StreamGenerator>(source).Result.AllSource();

        // Stripped of comments first: the dispatch files DOCUMENT that they avoid these very APIs,
        // so matching raw text would fail on the sentence promising the opposite.
        var code = StripComments(emitted);

        foreach (var api in new[]
                 {
                     "MakeGenericType", "MakeGenericMethod", "Activator.CreateInstance",
                     "Expression.Lambda", ".Compile()", "Reflection.Emit",
                     "Assembly.Load", "GetTypes()",
                 })
        {
            code.ShouldNotContain(api,
                customMessage: $"generated dispatch code uses {api}, which NativeAOT cannot compile");
        }
    }

    private static ImmutableArray<string> FindDynamicCodeReferences(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();

        var offences = ImmutableArray.CreateBuilder<string>();

        foreach (var handle in md.MemberReferences)
        {
            var member = md.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference)
                continue;

            var declaring = md.GetTypeReference((TypeReferenceHandle)member.Parent);
            var ns = md.GetString(declaring.Namespace);
            var typeName = ns.Length == 0
                ? md.GetString(declaring.Name)
                : ns + "." + md.GetString(declaring.Name);
            var memberName = md.GetString(member.Name);

            if (ForbiddenNamespaces.Any(f => ns.StartsWith(f, StringComparison.Ordinal)))
                offences.Add($"{typeName}::{memberName}");
            else if (ForbiddenCalls.Any(f => f.Type == typeName && f.Member == memberName))
                offences.Add($"{typeName}::{memberName}");
        }

        return offences.Distinct().ToImmutableArray();
    }

    /// <summary>Removes // and /* */ comments so documentation cannot be mistaken for code.</summary>
    private static string StripComments(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var withoutTrivia = root.ReplaceTrivia(
            root.DescendantTrivia().Where(t =>
                t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)),
            (_, _) => default);

        return withoutTrivia.ToFullString();
    }
}
