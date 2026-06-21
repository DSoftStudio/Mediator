// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Covers the self-handling-request discovery (<c>HandlerDiscovery.TryGetSelfHandlingRequest</c>) across every
/// <c>Execute</c> return shape and parameter kind, plus the <see cref="SelfHandlerParam"/> /
/// <see cref="SelfHandlerDetail"/> value structs directly.
/// </summary>
public class SelfHandlerTests
{
    // ── Value structs (equality + properties used by the incremental pipeline) ────────────────

    [Fact]
    public void SelfHandlerParam_Equality_And_Properties()
    {
        var a = new SelfHandlerParam(SelfHandlerParam.KindRequest, "R");
        var same = new SelfHandlerParam(SelfHandlerParam.KindRequest, "R");

        a.Kind.ShouldBe(SelfHandlerParam.KindRequest);
        a.TypeName.ShouldBe("R");
        a.Equals(same).ShouldBeTrue();
        a.GetHashCode().ShouldBe(same.GetHashCode());
        a.Equals(new SelfHandlerParam(SelfHandlerParam.KindService, "R")).ShouldBeFalse();    // different kind
        a.Equals(new SelfHandlerParam(SelfHandlerParam.KindRequest, "Other")).ShouldBeFalse(); // different type
        a.Equals((object)same).ShouldBeTrue();
        a.Equals((object)"x").ShouldBeFalse();
    }

    [Fact]
    public void SelfHandlerDetail_Equality_And_Properties()
    {
        var ps = new EquatableArray<SelfHandlerParam>(new[] { new SelfHandlerParam(SelfHandlerParam.KindRequest, "R") });
        var a = new SelfHandlerDetail("R", "string", SelfHandlerDetail.ReturnSync, ps);
        var same = new SelfHandlerDetail("R", "string", SelfHandlerDetail.ReturnSync, ps);

        a.RequestType.ShouldBe("R");
        a.ResponseType.ShouldBe("string");
        a.ReturnKind.ShouldBe(SelfHandlerDetail.ReturnSync);
        a.Parameters.Length.ShouldBe(1);
        a.Equals(same).ShouldBeTrue();
        a.GetHashCode().ShouldBe(same.GetHashCode());
        a.Equals(new SelfHandlerDetail("R", "string", SelfHandlerDetail.ReturnTaskOfT, ps)).ShouldBeFalse();
        a.Equals((object)same).ShouldBeTrue();
        a.Equals((object)"x").ShouldBeFalse();
    }

    // ── Discovery across every Execute return shape + parameter kind ──────────────────────────

    [Fact]
    public void Discovers_Self_Handlers_With_All_Return_Shapes_And_Param_Kinds()
    {
        // One self-handling request per return shape (sync T / Task<T> / ValueTask<T> / void→Unit / Task→Unit)
        // and the ValueTask one also exercises all three SelfHandlerParam kinds (request, service, cancellation).
        const string src = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public interface IClock { }

            public record SyncReq(int X) : IRequest<string>
            {
                public static string Execute(SyncReq r) => "s";                       // ReturnSync, KindRequest
            }

            public record VtReq(int X) : IRequest<string>
            {
                public static ValueTask<string> Execute(VtReq r, IClock clock, CancellationToken ct) => new("v"); // ReturnValueTaskOfT + all param kinds
            }

            public record TaskReq(int X) : IRequest<int>
            {
                public static Task<int> Execute(TaskReq r) => Task.FromResult(1);      // ReturnTaskOfT
            }

            public record VoidReq(int X) : IRequest<Unit>
            {
                public static void Execute(VoidReq r) { }                             // ReturnVoid → Unit
            }

            public record TaskUnitReq(int X) : IRequest<Unit>
            {
                public static Task Execute(TaskUnitReq r) => Task.CompletedTask;       // ReturnTask → Unit
            }
            """;

        // DependencyInjectionGenerator consumes the full SelfHandlerDetail (return kind + params) to emit the
        // self-handler adapter, so this exercises both the discovery branches and their use.
        var (result, _) = GeneratorTestHarness.Run<DependencyInjectionGenerator>(src);
        var code = result.AllSource();

        code.ShouldContain("SyncReq");
        code.ShouldContain("VtReq");
        code.ShouldContain("TaskReq");
        code.ShouldContain("VoidReq");
        code.ShouldContain("TaskUnitReq");
    }
}
