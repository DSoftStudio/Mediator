// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// ============================================================================
// Cross-Project Mocking Sample — Unit Tests
// ============================================================================
//
// This test project references ONLY DSoftStudio.Mediator.Abstractions:
//   - No source generators run here
//   - No interceptors are generated
//   - Moq mocks of ISender/IMediator work perfectly in any build configuration
//
// KEY RULE:
//   Always use the explicit two-generic-parameter form in Setup/Verify:
//     x.Send<TRequest, TResponse>(...)   ← interface method (mockable ✅)
//
//   The single-parameter form (sender.Send(new MyCommand())) resolves to a
//   generated extension method in projects with generators — but since this
//   test project has no generators, only the interface method is available.
// ============================================================================

using Host.Application.Behaviors;
using Host.Application.Commands;
using Host.Application.Queries;
using Host.Application.Services;

namespace Host.Tests;

public class OrderServiceTests
{
    // ── Test 1: PlaceOrderAsync sends CreateOrderCommand ────────────

    [Fact]
    public async Task PlaceOrderAsync_SendsCreateOrderCommand_ReturnsOrderId()
    {
        // Arrange
        var senderMock = new Mock<ISender>(MockBehavior.Strict);

        senderMock
            .Setup(x => x.Send<CreateOrderCommand, int>(
                It.Is<CreateOrderCommand>(c => c.ProductName == "Widget" && c.Quantity == 5),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1001);

        var service = new OrderService(senderMock.Object);

        // Act
        var orderId = await service.PlaceOrderAsync("Widget", 5);

        // Assert
        Assert.Equal(1001, orderId);
        senderMock.Verify(
            x => x.Send<CreateOrderCommand, int>(
                It.IsAny<CreateOrderCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Test 2: GetOrderSummaryAsync sends GetOrderQuery ────────────

    [Fact]
    public async Task GetOrderSummaryAsync_SendsGetOrderQuery_ReturnsSummary()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        senderMock
            .Setup(x => x.Send<GetOrderQuery, string>(
                It.Is<GetOrderQuery>(q => q.OrderId == 42),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Order #42: Widget × 5");

        var service = new OrderService(senderMock.Object);

        // Act
        var summary = await service.GetOrderSummaryAsync(42);

        // Assert
        Assert.Equal("Order #42: Widget × 5", summary);
    }

    // ── Test 3: Verify no unexpected commands are sent ───────────────

    [Fact]
    public async Task PlaceOrderAsync_DoesNotSendUnexpectedCommands()
    {
        // Arrange
        var senderMock = new Mock<ISender>(MockBehavior.Strict);

        senderMock
            .Setup(x => x.Send<CreateOrderCommand, int>(
                It.IsAny<CreateOrderCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new OrderService(senderMock.Object);

        // Act
        await service.PlaceOrderAsync("Gadget", 1);

        // Assert — only CreateOrderCommand was sent, nothing else
        senderMock.Verify(
            x => x.Send<CreateOrderCommand, int>(
                It.IsAny<CreateOrderCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        senderMock.VerifyNoOtherCalls();
    }

    // ── Test 4: Capture and inspect the command ─────────────────────

    [Fact]
    public async Task PlaceOrderAsync_CapturesCommandDetails()
    {
        // Arrange
        CreateOrderCommand? captured = null;

        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(x => x.Send<CreateOrderCommand, int>(
                It.IsAny<CreateOrderCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateOrderCommand, CancellationToken>((cmd, _) => captured = cmd)
            .ReturnsAsync(999);

        var service = new OrderService(senderMock.Object);

        // Act
        await service.PlaceOrderAsync("Premium Widget", 10);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal("Premium Widget", captured!.ProductName);
        Assert.Equal(10, captured.Quantity);
    }

    // ── Test 5: IMediator mock also works ───────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_WorksWithIMediatorMock()
    {
        // IMediator extends ISender — OrderService accepts ISender,
        // so an IMediator mock can be used too.
        var mediatorMock = new Mock<IMediator>();

        mediatorMock
            .Setup(x => x.Send<CreateOrderCommand, int>(
                It.IsAny<CreateOrderCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2002);

        var service = new OrderService(mediatorMock.Object);

        var orderId = await service.PlaceOrderAsync("Deluxe Widget", 3);

        Assert.Equal(2002, orderId);
    }

    // ── Test 6: Pipeline behavior wraps the handler ─────────────────

    [Fact]
    public async Task LoggingBehavior_LogsBeforeAndAfterHandler()
    {
        // Arrange — create a real LoggingBehavior with a captured log
        var logs = new List<string>();
        var behavior = new LoggingBehavior<CreateOrderCommand, int>(logs.Add);

        // Stub the "next" handler in the pipeline
        var handlerMock = new Mock<IRequestHandler<CreateOrderCommand, int>>();
        handlerMock
            .Setup(h => h.Handle(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Act
        var result = await behavior.Handle(
            new CreateOrderCommand("Widget", 1),
            handlerMock.Object,
            CancellationToken.None);

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, logs.Count);
        Assert.Contains("Handling CreateOrderCommand", logs[0]);
        Assert.Contains("Handled CreateOrderCommand", logs[1]);
        handlerMock.Verify(
            h => h.Handle(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
