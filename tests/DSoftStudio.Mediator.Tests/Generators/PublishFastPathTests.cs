// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// ADR-0066 Publish fast path: the <c>__NotifCache_*</c> classes emitted by
/// <see cref="NotificationGenerator"/> (ArmedSet + SAFE concrete tier + eligibility scan) and
/// the routed bodies emitted by <see cref="PublishInterceptorGenerator"/> — including the
/// <c>DSoftMediatorDisableAggressive</c> force-OFF knob and the all-or-nothing exclusions.
/// </summary>
public class PublishFastPathTests
{
    private const string TwoHandlerSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public sealed record OrderShipped(int Id) : INotification;

        public sealed class EmailHandler : INotificationHandler<OrderShipped>
        {
            public Task Handle(OrderShipped notification, CancellationToken ct) => Task.CompletedTask;
        }

        public sealed class AuditHandler : INotificationHandler<OrderShipped>
        {
            public Task Handle(OrderShipped notification, CancellationToken ct) => Task.CompletedTask;
        }
        """;

    // ── NotificationGenerator: cache class emission ─────────────────────────

    [Fact]
    public void Emits_NotifCache_With_ArmedSet_And_Safe_Tier_For_Eligible_Group()
    {
        var (result, _) = GeneratorTestHarness.Run<NotificationGenerator>(TwoHandlerSource);
        var source = result.AllSource();

        source.ShouldContain("internal static class __NotifCache_TestApp_OrderShipped");
        source.ShouldContain("internal sealed class ArmedSet");
        // Concrete fields in alphabetical handler order: Audit before Email.
        source.ShouldContain("internal readonly global::TestApp.AuditHandler H0;");
        source.ShouldContain("internal readonly global::TestApp.EmailHandler H1;");
        // Armed gate + statically-bound unrolled dispatch + sync fast-path checks.
        source.ShouldContain("get => global::System.Threading.Volatile.Read(ref _armed);");
        source.ShouldContain("var t0 = s.H0.Handle(notification, ct);");
        source.ShouldContain("IsCompletedSuccessfully");
        // SAFE tier: provider-keyed TLS + exact-GetType verification + graceful demotion.
        source.ShouldContain("DispatchSafe");
        source.ShouldContain("handlers[0].GetType() == typeof(global::TestApp.AuditHandler)");
        source.ShouldContain("NotificationCachedDispatcher.DispatchSequential(handlers, notification, ct);");
    }

    [Fact]
    public void Registry_Scans_Eligibility_And_Routes_ObjectSwitch_Through_Cache()
    {
        var (result, _) = GeneratorTestHarness.Run<NotificationGenerator>(TwoHandlerSource);
        var source = result.AllSource();

        // Register now takes the collection and grants eligibility with the generated handler set.
        source.ShouldContain("public static void Register(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        source.ShouldContain("AggressiveNotificationDispatch<global::TestApp.OrderShipped>.SetEligibility(services,");
        source.ShouldContain("typeof(global::TestApp.AuditHandler), typeof(global::TestApp.EmailHandler)");
        // The Publish(object) switch routes: armed gate first, SAFE tier fallback.
        source.ShouldContain("var __armed = __NotifCache_TestApp_OrderShipped.Armed;");
        source.ShouldContain("return __NotifCache_TestApp_OrderShipped.DispatchSafe(sp, __n0, ct);");
    }

    [Fact]
    public void DisableAggressive_Knob_Suppresses_Armed_Tier_But_Keeps_Safe_Tier()
    {
        var (result, _) = GeneratorTestHarness.Run<NotificationGenerator>(
            TwoHandlerSource,
            buildProperties: new() { ["DSoftMediatorDisableAggressive"] = "true" });
        var source = result.AllSource();

        source.ShouldNotContain("AggressiveNotificationDispatch",
            customMessage: "the knob must remove eligibility, arming and the armed holder");
        source.ShouldNotContain(".Armed;");
        // The SAFE concrete tier survives the knob.
        source.ShouldContain("internal static class __NotifCache_TestApp_OrderShipped");
        source.ShouldContain("DispatchSafe");
    }

    [Fact]
    public void ExplicitInterfaceImpl_Handler_Excludes_The_Whole_Group()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public sealed record OrderShipped(int Id) : INotification;

            public sealed class NormalHandler : INotificationHandler<OrderShipped>
            {
                public Task Handle(OrderShipped notification, CancellationToken ct) => Task.CompletedTask;
            }

            public sealed class ExplicitHandler : INotificationHandler<OrderShipped>
            {
                Task INotificationHandler<OrderShipped>.Handle(OrderShipped notification, CancellationToken ct)
                    => Task.CompletedTask;
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<NotificationGenerator>(source);
        var generated = result.AllSource();

        generated.ShouldNotContain("__NotifCache_",
            customMessage: "concrete.Handle would be CS1061 on the explicit impl — all-or-nothing per type");
        // Today's dispatch table + sequential path stay.
        generated.ShouldContain("NotificationDispatch<global::TestApp.OrderShipped>.TryInitialize(");
        generated.ShouldContain("DispatchSequential");
    }

    [Fact]
    public void More_Than_Eight_Handlers_Keep_Todays_Path()
    {
        var sb = new System.Text.StringBuilder("""
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public sealed record OrderShipped(int Id) : INotification;

            """);
        for (int i = 0; i < 9; i++)
        {
            sb.AppendLine($$"""
                public sealed class Handler{{i}} : INotificationHandler<OrderShipped>
                {
                    public Task Handle(OrderShipped notification, CancellationToken ct) => Task.CompletedTask;
                }
                """);
        }

        var (result, _) = GeneratorTestHarness.Run<NotificationGenerator>(sb.ToString());
        result.AllSource().ShouldNotContain("__NotifCache_",
            customMessage: "the unroll cap is 8 — larger fan-outs keep the interface loop");
    }

    [Fact]
    public void Generated_Cache_Compiles_Cleanly_With_The_Runtime()
    {
        var (_, output) = GeneratorTestHarness.Run<NotificationGenerator>(TwoHandlerSource);

        var errors = output.GetDiagnostics()
            .Where(static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToList();
        errors.ShouldBeEmpty();
    }

    // ── PublishInterceptorGenerator: routed bodies ─────────────────────────

    private const string CallSiteSource = TwoHandlerSource + """


        public static class CallSites
        {
            public static Task Run(DSoftStudio.Mediator.Abstractions.IPublisher publisher)
                => publisher.Publish(new OrderShipped(1));
        }
        """;

    [Fact]
    public void Release_Interceptor_Emits_Armed_Gate_Before_Castclass_And_Safe_Tail()
    {
        var (result, _) = GeneratorTestHarness.Run<PublishInterceptorGenerator>(
            CallSiteSource, interceptors: true, release: true);
        var source = result.AllSource();

        source.ShouldContain("var __armed = global::DSoftStudio.Mediator.Generated.TestAssembly.__NotifCache_TestApp_OrderShipped.Armed;");
        source.ShouldContain("DispatchSafe(sp, notification, cancellationToken);");

        // Placement: the armed gate must run BEFORE the castclass in Release bodies.
        int armedIdx = source.IndexOf("var __armed", StringComparison.Ordinal);
        int castIdx = source.IndexOf("(global::DSoftStudio.Mediator.IServiceProviderAccessor)publisher", StringComparison.Ordinal);
        armedIdx.ShouldBeGreaterThanOrEqualTo(0);
        castIdx.ShouldBeGreaterThan(armedIdx);
    }

    [Fact]
    public void Debug_Interceptor_Keeps_Mock_Probe_First()
    {
        var (result, _) = GeneratorTestHarness.Run<PublishInterceptorGenerator>(
            CallSiteSource, interceptors: true, release: false);
        var source = result.AllSource();

        int probeIdx = source.IndexOf("publisher is not global::DSoftStudio.Mediator.IServiceProviderAccessor", StringComparison.Ordinal);
        int armedIdx = source.IndexOf("var __armed", StringComparison.Ordinal);
        probeIdx.ShouldBeGreaterThanOrEqualTo(0);
        armedIdx.ShouldBeGreaterThan(probeIdx,
            "defensive bodies keep the mock isinst probe FIRST — mock ISender/IPublisher behavior is sacred");
    }

    [Fact]
    public void Interceptor_Knob_Removes_Gate_Keeps_Safe_Tail()
    {
        var (result, _) = GeneratorTestHarness.Run<PublishInterceptorGenerator>(
            CallSiteSource, interceptors: true, release: true,
            buildProperties: new() { ["DSoftMediatorDisableAggressive"] = "true" });
        var source = result.AllSource();

        source.ShouldNotContain("var __armed");
        source.ShouldContain("DispatchSafe(sp, notification, cancellationToken);");
    }

    [Fact]
    public void Interceptor_Without_Cache_Keeps_Todays_Body()
    {
        // The only handler is an explicit impl -> no cache class -> the interceptor must fall
        // back to today's DispatchSequential body (referencing a nonexistent cache = CS0103).
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public sealed record OrderShipped(int Id) : INotification;

            public sealed class ExplicitHandler : INotificationHandler<OrderShipped>
            {
                Task INotificationHandler<OrderShipped>.Handle(OrderShipped notification, CancellationToken ct)
                    => Task.CompletedTask;
            }

            public static class CallSites
            {
                public static Task Run(IPublisher publisher)
                    => publisher.Publish(new OrderShipped(1));
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<PublishInterceptorGenerator>(
            source, interceptors: true, release: true);
        var generated = result.AllSource();

        generated.ShouldNotContain("__NotifCache_");
        generated.ShouldContain("DispatchSequential(notification, sp, cancellationToken);");
    }
}
