// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace MockingExample;

/// <summary>
/// Verifies that the source-generated interceptors do NOT rewrite call sites
/// inside expression tree lambdas used by mocking frameworks (Moq).
/// <para>
/// Without the expression tree detection fix, the interceptor rewrites
/// <c>x.Send&lt;T,R&gt;(...)</c> inside <c>mock.Setup(x =&gt; ...)</c> into a
/// static extension method, causing <see cref="System.NotSupportedException"/>:
/// "Extension methods may not be used in setup / verification expressions."
/// </para>
/// </summary>
public class MockSetupTests
{
    // ── ISender.Send<T, R> ───────────────────────────────────────────

    [Fact]
    public void ISender_Setup_ExplicitGenerics_DoesNotThrow()
    {
        var mock = new Mock<ISender>();

        mock.Setup(x => x.Send<Ping, int>(
            It.IsAny<Ping>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void ISender_Verify_ExplicitGenerics_DoesNotThrow()
    {
        var mock = new Mock<ISender>();

        mock.Setup(x => x.Send<Ping, int>(
            It.IsAny<Ping>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        mock.Verify(
            x => x.Send<Ping, int>(
                It.IsAny<Ping>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ISender_Setup_UnitResponse_DoesNotThrow()
    {
        var mock = new Mock<ISender>();

        mock.Setup(x => x.Send<PingVoid, Unit>(
            It.IsAny<PingVoid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void ISender_Setup_MultipleCommands_DoesNotThrow()
    {
        var mock = new Mock<ISender>();

        mock.Setup(x => x.Send<Ping, int>(
            It.IsAny<Ping>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        mock.Setup(x => x.Send<PingVoid, Unit>(
            It.IsAny<PingVoid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        Assert.NotNull(mock.Object);
    }

    // ── IPublisher.Publish<T> ────────────────────────────────────────

    [Fact]
    public void IPublisher_Setup_ExplicitGeneric_DoesNotThrow()
    {
        var mock = new Mock<IPublisher>();

        mock.Setup(x => x.Publish(
            It.IsAny<PingNotification>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void IPublisher_Verify_ExplicitGeneric_DoesNotThrow()
    {
        var mock = new Mock<IPublisher>();

        mock.Setup(x => x.Publish(
            It.IsAny<PingNotification>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mock.Verify(
            x => x.Publish(
                It.IsAny<PingNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── IMediator (ISender + IPublisher + CreateStream) ──────────────

    [Fact]
    public void IMediator_Setup_Send_DoesNotThrow()
    {
        var mock = new Mock<IMediator>();

        mock.Setup(x => x.Send<Ping, int>(
            It.IsAny<Ping>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void IMediator_Setup_Publish_DoesNotThrow()
    {
        var mock = new Mock<IMediator>();

        mock.Setup(x => x.Publish(
            It.IsAny<PingNotification>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void IMediator_Setup_CreateStream_DoesNotThrow()
    {
        var mock = new Mock<IMediator>();

        mock.Setup(x => x.CreateStream<PingStream, int>(
            It.IsAny<PingStream>(),
            It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(1, 2, 3));

        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void IMediator_Setup_AllThreeFlows_DoesNotThrow()
    {
        var mock = new Mock<IMediator>();

        // Send
        mock.Setup(x => x.Send<Ping, int>(
            It.IsAny<Ping>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Publish
        mock.Setup(x => x.Publish(
            It.IsAny<PingNotification>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Stream
        mock.Setup(x => x.CreateStream<PingStream, int>(
            It.IsAny<PingStream>(),
            It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(1, 2, 3));

        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void IMediator_Verify_AllThreeFlows_DoesNotThrow()
    {
        var mock = new Mock<IMediator>();

        mock.Verify(
            x => x.Send<Ping, int>(
                It.IsAny<Ping>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        mock.Verify(
            x => x.Publish(
                It.IsAny<PingNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        mock.Verify(
            x => x.CreateStream<PingStream, int>(
                It.IsAny<PingStream>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Realistic workflow: Strict mock Setup → Verify ─────────────

    /// <summary>
    /// Reproduces the exact pattern from the GitHub issue: a strict mock of
    /// <see cref="IMediator"/> with multiple command Setups and Verifies.
    /// <para>
    /// <b>Note:</b> Direct invocation on a mock (<c>mock.Object.Send(...)</c>) is
    /// intercepted by the source generator in projects that reference the generators.
    /// For end-to-end workflow tests (Setup → Execute → Verify), reference only
    /// <c>DSoftStudio.Mediator.Abstractions</c> (no generators) in the test project.
    /// This test validates the Setup/Verify path which is the part protected by the
    /// expression tree detection fix.
    /// </para>
    /// </summary>
    [Fact]
    public void Workflow_StrictMock_MultipleSetupAndVerify()
    {
        var mock = new Mock<IMediator>(MockBehavior.Strict);

        // Setup: configure expected calls (expression tree lambdas — interceptor skips these)
        mock.Setup(x => x.Send<Ping, int>(
            It.IsAny<Ping>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        mock.Setup(x => x.Send<PingVoid, Unit>(
            It.IsAny<PingVoid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        // Verify: each command was never called (no execution yet)
        mock.Verify(
            x => x.Send<Ping, int>(
                It.IsAny<Ping>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        mock.Verify(
            x => x.Send<PingVoid, Unit>(
                It.IsAny<PingVoid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        mock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Strict mock that configures Send + Publish + Stream on the same
    /// <see cref="IMediator"/> mock — the full surface area.
    /// </summary>
    [Fact]
    public void Workflow_StrictMock_AllFlows_SetupAndVerify()
    {
        var mock = new Mock<IMediator>(MockBehavior.Strict);

        // Send
        mock.Setup(x => x.Send<Ping, int>(
            It.IsAny<Ping>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Publish
        mock.Setup(x => x.Publish(
            It.IsAny<PingNotification>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Stream
        mock.Setup(x => x.CreateStream<PingStream, int>(
            It.IsAny<PingStream>(),
            It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(1, 2, 3));

        // Verify all three flows
        mock.Verify(
            x => x.Send<Ping, int>(
                It.IsAny<Ping>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        mock.Verify(
            x => x.Publish(
                It.IsAny<PingNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        mock.Verify(
            x => x.CreateStream<PingStream, int>(
                It.IsAny<PingStream>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        mock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that multiple Setups on different command types can coexist
    /// on the same mock using <see cref="MockBehavior.Strict"/> with
    /// Callback tracking — the interceptor skips all expression tree lambdas.
    /// </summary>
    [Fact]
    public void Workflow_MultipleSetups_CallbackTracking()
    {
        var callLog = new List<string>();
        var mock = new Mock<ISender>();

        mock.Setup(x => x.Send<Ping, int>(
            It.IsAny<Ping>(),
            It.IsAny<CancellationToken>()))
            .Callback(() => callLog.Add("Ping"))
            .ReturnsAsync(42);

        mock.Setup(x => x.Send<PingVoid, Unit>(
            It.IsAny<PingVoid>(),
            It.IsAny<CancellationToken>()))
            .Callback(() => callLog.Add("PingVoid"))
            .ReturnsAsync(Unit.Value);

        mock.Setup(x => x.Send<SlowPing, int>(
            It.IsAny<SlowPing>(),
            It.IsAny<CancellationToken>()))
            .Callback(() => callLog.Add("SlowPing"))
            .ReturnsAsync(99);

        // Setups are independent — each has its own Callback
        Assert.NotNull(mock.Object);
        Assert.Empty(callLog);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
