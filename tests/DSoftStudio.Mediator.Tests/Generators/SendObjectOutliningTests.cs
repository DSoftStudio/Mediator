// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Guards the shape of the generated <c>Send(object)</c> switch.
/// <para>
/// The bodies used to be pasted into the cases. That cost ~1.2 KB of machine code per request
/// type in one method, which past nine types left the JIT's inline budget (Send(object) went
/// 5.5 -> 9.5 ns on a single added type) and by 26-40 types reached 29-32 KB — about a whole
/// 32 KB L1 instruction cache — so every call thrashed it: 14.9 ns at 40 types, 20.5 ns at 80.
/// Outlined, it stays flat at 5.4-6.8 ns.
/// </para>
/// <para>
/// Both assertions here are load-bearing. Without <c>NoInlining</c> the JIT pulls the bodies back
/// into the switch and rebuilds the oversized method; with a body pasted into a case the method is
/// oversized to begin with. Neither regression changes behaviour, so only a shape test catches it.
/// </para>
/// </summary>
public class SendObjectOutliningTests
{
    private const string TwoRequests = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record GetUser(int Id) : IRequest<string>;
        public sealed class GetUserHandler : IRequestHandler<GetUser, string>
        {
            public ValueTask<string> Handle(GetUser request, CancellationToken ct) => new("user");
        }

        public record GetOrder(int Id) : IRequest<int>;
        public sealed class GetOrderHandler : IRequestHandler<GetOrder, int>
        {
            public ValueTask<int> Handle(GetOrder request, CancellationToken ct) => new(7);
        }
        """;

    [Fact]
    public void Every_Switch_Case_Calls_Out_Instead_Of_Carrying_Its_Body()
    {
        var (result, _) = GeneratorTestHarness.Run<MediatorExtensionsGenerator>(TwoRequests);
        var lines = result.AllSource().Split('\n');

        var caseLines = lines
            .Select((text, index) => (text: text.Trim(), index))
            .Where(l => l.text.StartsWith("case global::TestApp.", StringComparison.Ordinal))
            .ToList();

        caseLines.Count.ShouldBe(2, "both request types should reach the Send(object) switch");

        foreach (var (text, index) in caseLines)
        {
            // The line after the case must be the outlined call — not '{', which is what opening
            // a pasted body looks like.
            var next = lines[index + 1].Trim();
            next.ShouldStartWith(
                "return __SendObjectCase_",
                customMessage: $"the case '{text}' carries its body instead of calling out");
        }
    }

    [Fact]
    public void Every_Outlined_Body_Is_Marked_NoInlining()
    {
        var (result, _) = GeneratorTestHarness.Run<MediatorExtensionsGenerator>(TwoRequests);
        var lines = result.AllSource().Split('\n');

        var declarations = lines
            .Select((text, index) => (text: text.Trim(), index))
            .Where(l => l.text.Contains("__SendObjectCase_", StringComparison.Ordinal)
                        && l.text.StartsWith("private static", StringComparison.Ordinal))
            .ToList();

        declarations.Count.ShouldBe(2, "each pair should get its own outlined dispatch body");

        foreach (var (text, index) in declarations)
        {
            lines[index - 1].ShouldContain(
                "MethodImplOptions.NoInlining",
                customMessage: $"'{text}' may be inlined back into the switch");
        }
    }

    [Fact]
    public void Outlined_Bodies_Still_Carry_The_Dispatch_Protocol()
    {
        var (result, _) = GeneratorTestHarness.Run<MediatorExtensionsGenerator>(TwoRequests);
        var code = result.AllSource();

        // Outlining moved the body; it must not have dropped any of it.
        code.ShouldContain("__SendObjectCase_TestApp_GetUser_string");
        code.ShouldContain("__SendObjectCase_TestApp_GetOrder_int");
        code.ShouldContain("HasPipelineChain");
        code.ShouldContain("AwaitAndBox");
    }
}
