// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Drives the real <see cref="NotificationGenerator"/> in-memory and asserts the generated
/// <c>NotificationDispatch.g.cs</c> — the compile-time notification dispatch tables that replace
/// runtime service enumeration. It had zero coverage before this.
/// </summary>
public class NotificationGeneratorTests
{
    private const string NotificationHandler = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record OrderPlaced(int Id) : INotification;

        public sealed class EmailHandler : INotificationHandler<OrderPlaced>
        {
            public Task Handle(OrderPlaced notification, CancellationToken ct) => Task.CompletedTask;
        }
        """;

    [Fact]
    public void Generates_NotificationDispatch_For_Handler()
    {
        var (result, _) = GeneratorTestHarness.Run<NotificationGenerator>(NotificationHandler);
        var code = result.AllSource();

        code.ShouldContain("NotificationRegistry");
        code.ShouldContain("PrecompileNotifications");
        code.ShouldContain("TryInitialize");                        // dispatch table populated for the notification
        code.ShouldContain("NotificationObjectDispatch.Register");  // AOT-safe Publish(object) path
        code.ShouldContain("PublishObjectSwitch");                  // source-generated type-switch fast path
        code.ShouldContain("OrderPlaced");
        code.ShouldContain("EmailHandler");
    }

    [Fact]
    public void Groups_Multiple_Handlers_For_Same_Notification()
    {
        // Two handlers for one notification → both factories land in the same dispatch group (covers the
        // per-handler inner loop). Abstract + open-generic handlers must be skipped (GetHandlerInfo rejection).
        const string twoHandlers = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record OrderPlaced(int Id) : INotification;

            public sealed class EmailHandler : INotificationHandler<OrderPlaced>
            {
                public Task Handle(OrderPlaced n, CancellationToken ct) => Task.CompletedTask;
            }

            public sealed class AuditHandler : INotificationHandler<OrderPlaced>
            {
                public Task Handle(OrderPlaced n, CancellationToken ct) => Task.CompletedTask;
            }

            // Abstract handler — must be skipped by the generator.
            public abstract class BaseHandler : INotificationHandler<OrderPlaced>
            {
                public abstract Task Handle(OrderPlaced n, CancellationToken ct);
            }

            // Open-generic handler — must be skipped (the generator only registers closed concrete handlers).
            public sealed class GenericHandler<T> : INotificationHandler<OrderPlaced>
            {
                public Task Handle(OrderPlaced n, CancellationToken ct) => Task.CompletedTask;
            }

            // file-local handler — must be skipped (cannot be referenced for registration).
            file sealed class FileLocalHandler : INotificationHandler<OrderPlaced>
            {
                public Task Handle(OrderPlaced n, CancellationToken ct) => Task.CompletedTask;
            }

            // A class WITH a base list that is NOT a notification handler — exercises the
            // "candidate matched syntactically but rejected semantically" branch.
            public sealed class NotAHandler : System.IDisposable
            {
                public void Dispose() { }
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<NotificationGenerator>(twoHandlers);
        var code = result.AllSource();

        code.ShouldContain("EmailHandler");
        code.ShouldContain("AuditHandler");
        code.ShouldNotContain("BaseHandler");      // abstract → skipped
        code.ShouldNotContain("GenericHandler");   // open-generic → skipped
    }

    [Fact]
    public void Generates_Empty_Dispatch_When_No_Handlers()
    {
        // A notification type with no handler: the registry skeleton (+ PrecompileNotifications) is still
        // emitted, but with no dispatch group → no PublishObjectSwitch. Covers the empty-groups branch.
        const string none = """
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record OrderPlaced(int Id) : INotification;
            """;

        var (result, _) = GeneratorTestHarness.Run<NotificationGenerator>(none);
        var code = result.AllSource();

        code.ShouldContain("NotificationRegistry");
        code.ShouldContain("PrecompileNotifications");
        code.ShouldNotContain("PublishObjectSwitch");
    }
}
