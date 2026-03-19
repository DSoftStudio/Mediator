// ============================================================================
// How to mock ISender / IMediator with Moq
// ============================================================================
//
// KEY RULE:
//   Always use the explicit two-generic-parameter form in Setup/Verify:
//     x.Send<TRequest, TResponse>(...)   ← interface method (mockable ✅)
//
//   Never use the single-parameter form:
//     x.Send(new MyCommand())            ← generated extension (not mockable ❌)
//
// WHY:
//   DSoftStudio.Mediator generates typed extension methods via interceptors.
//   Moq cannot mock extension methods. The ISender interface defines:
//
//     ValueTask<TResponse> Send<TRequest, TResponse>(
//         TRequest request, CancellationToken ct)
//         where TRequest : IRequest<TResponse>;
//
//   Mocking this interface method directly works perfectly.
// ============================================================================

namespace MockingExample;

public class WorkflowTests
{
    // ── Test 1: Mock ISender (preferred — narrower interface) ───────────

    [Fact]
    public async Task RunAsync_SendsBothCommands()
    {
        // Arrange
        var senderMock = new Mock<ISender>(MockBehavior.Strict);

        senderMock
            .Setup(x => x.Send<RunTask1Command, Unit>(
                It.IsAny<RunTask1Command>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        senderMock
            .Setup(x => x.Send<RunTask2Command, Unit>(
                It.IsAny<RunTask2Command>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        var workflow = new Workflow(senderMock.Object);

        // Act
        await workflow.RunAsync();

        // Assert — each command sent exactly once
        senderMock.Verify(
            x => x.Send<RunTask1Command, Unit>(
                It.IsAny<RunTask1Command>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        senderMock.Verify(
            x => x.Send<RunTask2Command, Unit>(
                It.IsAny<RunTask2Command>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify no unexpected calls (e.g. RunTask3Command was NOT sent)
        senderMock.VerifyNoOtherCalls();
    }

    // ── Test 2: Mock IMediator (when you also need Publish/CreateStream) ─

    [Fact]
    public async Task RunAsync_WithIMediator()
    {
        // IMediator : ISender, IPublisher — same Send method
        var mediatorMock = new Mock<IMediator>(MockBehavior.Strict);

        mediatorMock
            .Setup(x => x.Send<RunTask1Command, Unit>(
                It.IsAny<RunTask1Command>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        mediatorMock
            .Setup(x => x.Send<RunTask2Command, Unit>(
                It.IsAny<RunTask2Command>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        // IMediator implements ISender, so it can be passed where ISender is expected
        var workflow = new Workflow(mediatorMock.Object);

        await workflow.RunAsync();

        mediatorMock.Verify(
            x => x.Send<RunTask1Command, Unit>(
                It.IsAny<RunTask1Command>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mediatorMock.Verify(
            x => x.Send<RunTask2Command, Unit>(
                It.IsAny<RunTask2Command>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mediatorMock.VerifyNoOtherCalls();
    }

    // ── Test 3: Verify command was NOT sent ─────────────────────────────

    [Fact]
    public async Task RunAsync_DoesNotSendUnexpectedCommands()
    {
        var senderMock = new Mock<ISender>();

        senderMock
            .Setup(x => x.Send<RunTask1Command, Unit>(
                It.IsAny<RunTask1Command>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        senderMock
            .Setup(x => x.Send<RunTask2Command, Unit>(
                It.IsAny<RunTask2Command>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        var workflow = new Workflow(senderMock.Object);
        await workflow.RunAsync();

        // RunTask3Command should never be sent
        senderMock.Verify(
            x => x.Send<RunTask3Command, Unit>(
                It.IsAny<RunTask3Command>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Test 4: Callback to inspect the command ─────────────────────────

    [Fact]
    public async Task RunAsync_CanCaptureCommandInstances()
    {
        RunTask1Command? captured = null;

        var senderMock = new Mock<ISender>();

        senderMock
            .Setup(x => x.Send<RunTask1Command, Unit>(
                It.IsAny<RunTask1Command>(),
                It.IsAny<CancellationToken>()))
            .Callback<RunTask1Command, CancellationToken>((cmd, _) => captured = cmd)
            .ReturnsAsync(Unit.Value);

        senderMock
            .Setup(x => x.Send<RunTask2Command, Unit>(
                It.IsAny<RunTask2Command>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        var workflow = new Workflow(senderMock.Object);
        await workflow.RunAsync();

        Assert.NotNull(captured);
    }
}
