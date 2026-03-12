// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Unique message types for coverage tests (avoid static race conditions) ──

public record CovPing : IRequest<int>;
public record CovAsyncNotification : INotification;
public record CovSyncNotification : INotification;
public record CovObjNotification : INotification;
public record CovStream : IStreamRequest<int>;
public record CovBehaviorStream : IStreamRequest<int>;

// ── Handlers ──

public sealed class CovPingHandler : IRequestHandler<CovPing, int>
{
    public ValueTask<int> Handle(CovPing request, CancellationToken ct) => new(42);
}

public sealed class CovAsyncNotificationHandler : INotificationHandler<CovAsyncNotification>
{
    public int CallCount;

    public async Task Handle(CovAsyncNotification notification, CancellationToken ct)
    {
        await Task.Yield();
        Interlocked.Increment(ref CallCount);
    }
}

public sealed class CovAsyncNotificationHandler2 : INotificationHandler<CovAsyncNotification>
{
    public int CallCount;

    public async Task Handle(CovAsyncNotification notification, CancellationToken ct)
    {
        await Task.Yield();
        Interlocked.Increment(ref CallCount);
    }
}

public sealed class CovSyncNotificationHandler : INotificationHandler<CovSyncNotification>
{
    public int CallCount;

    public Task Handle(CovSyncNotification notification, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        return Task.CompletedTask;
    }
}

public sealed class CovObjNotificationHandler : INotificationHandler<CovObjNotification>
{
    public int CallCount;

    public Task Handle(CovObjNotification notification, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        return Task.CompletedTask;
    }
}

public sealed class CovStreamHandler : IStreamRequestHandler<CovStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        CovStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return 10;
        yield return 20;
    }
}

public sealed class CovBehaviorStreamHandler : IStreamRequestHandler<CovBehaviorStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        CovBehaviorStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return 1;
        yield return 2;
    }
}

public sealed class CovStreamBehavior : IStreamPipelineBehavior<CovBehaviorStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        CovBehaviorStream request,
        IStreamRequestHandler<CovBehaviorStream, int> next,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in next.Handle(request, ct))
        {
            yield return item * 10;
        }
    }
}

// ── Tests ──

public class UnitOperatorTests
{
    [Fact]
    public void Equality_Operator_ReturnsTrue()
    {
        (Unit.Value == new Unit()).ShouldBeTrue();
    }

    [Fact]
    public void Inequality_Operator_ReturnsFalse()
    {
        (Unit.Value != new Unit()).ShouldBeFalse();
    }

    [Fact]
    public async Task Task_StaticField_ReturnsCompletedTaskWithUnit()
    {
        Unit.Task.IsCompletedSuccessfully.ShouldBeTrue();
        var result = await Unit.Task;
        result.ShouldBe(Unit.Value);
    }

    [Fact]
    public async Task ValueTask_StaticField_ReturnsCompletedWithUnit()
    {
        Unit.ValueTask.IsCompletedSuccessfully.ShouldBeTrue();
        var result = await Unit.ValueTask;
        result.ShouldBe(Unit.Value);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        Unit.Value.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void CompareTo_Null_ReturnsNegative()
    {
        Unit.Value.CompareTo(null).ShouldBeLessThan(0);
    }
}

public class MediatorHandlerRegistrationAttributeTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var svc = typeof(IRequestHandler<Ping, int>);
        var impl = typeof(PingHandler);

        var attr = new MediatorHandlerRegistrationAttribute(svc, impl);

        attr.ServiceType.ShouldBe(svc);
        attr.ImplementationType.ShouldBe(impl);
    }
}

public class SequentialNotificationPublisherCoverageTests
{
    [Fact]
    public async Task Publish_EmptyHandlers_ReturnsCompleted()
    {
        var publisher = new SequentialNotificationPublisher();
        var handlers = Array.Empty<INotificationHandler<CovSyncNotification>>();

        var task = publisher.Publish(handlers, new CovSyncNotification(), CancellationToken.None);
        await task;

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Publish_SyncHandlers_CompletesWithoutAsync()
    {
        var h1 = new CovSyncNotificationHandler();
        var publisher = new SequentialNotificationPublisher();
        var handlers = new INotificationHandler<CovSyncNotification>[] { h1 };

        await publisher.Publish(handlers, new CovSyncNotification(), CancellationToken.None);

        h1.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_AsyncHandlers_AwaitsAll()
    {
        var h1 = new CovAsyncNotificationHandler();
        var h2 = new CovAsyncNotificationHandler2();
        var publisher = new SequentialNotificationPublisher();
        var handlers = new INotificationHandler<CovAsyncNotification>[] { h1, h2 };

        await publisher.Publish(handlers, new CovAsyncNotification(), CancellationToken.None);

        h1.CallCount.ShouldBe(1);
        h2.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_ListInput_MaterializesToArray()
    {
        var h1 = new CovSyncNotificationHandler();
        var publisher = new SequentialNotificationPublisher();
        var handlers = new List<INotificationHandler<CovSyncNotification>> { h1 };

        await publisher.Publish(handlers, new CovSyncNotification(), CancellationToken.None);

        h1.CallCount.ShouldBe(1);
    }
}

public class ParallelNotificationPublisherCoverageTests
{
    [Fact]
    public async Task Publish_EmptyHandlers_ReturnsCompleted()
    {
        var publisher = new ParallelNotificationPublisher();
        var handlers = Array.Empty<INotificationHandler<CovSyncNotification>>();

        var task = publisher.Publish(handlers, new CovSyncNotification(), CancellationToken.None);
        await task;

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Publish_ListInput_MaterializesToArray()
    {
        var h1 = new CovSyncNotificationHandler();
        var publisher = new ParallelNotificationPublisher();
        var handlers = new List<INotificationHandler<CovSyncNotification>> { h1 };

        await publisher.Publish(handlers, new CovSyncNotification(), CancellationToken.None);

        h1.CallCount.ShouldBe(1);
    }
}

public class NotificationObjectDispatchCoverageTests
{
    [Fact]
    public void Dispatch_UnregisteredType_Throws()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(
            () => NotificationObjectDispatch.Dispatch(
                new UnregisteredNotification(), sp, null, CancellationToken.None));
    }
}

public class MediatorPublishObjectCoverageTests
{
    [Fact]
    public async Task Publish_NonNotificationObject_Throws()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await Should.ThrowAsync<ArgumentException>(
            () => mediator.Publish("not a notification", CancellationToken.None));
    }

    [Fact]
    public async Task Publish_NullObject_Throws()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => mediator.Publish((object)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Publish_NullGenericNotification_Throws()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => mediator.Publish<CovSyncNotification>(null!, CancellationToken.None));
    }
}

public class MediatorPublishWithCustomPublisherTests
{
    [Fact]
    public async Task Publish_UsesCustomPublisher_WhenRegistered()
    {
        var h1 = new CovSyncNotificationHandler();
        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<CovSyncNotification>>(h1);
        services.AddSingleton<INotificationPublisher, SequentialNotificationPublisher>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish(new CovSyncNotification());

        h1.CallCount.ShouldBe(1);
    }
}

public class StreamPipelineInvokerCoverageTests
{
    [Fact]
    public async Task Invoke_WithNoBehaviors_ReturnsStreamDirectly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<CovStream, int>, CovStreamHandler>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream<CovStream, int>(new CovStream()))
            items.Add(item);

        items.ShouldBe(new[] { 10, 20 });
    }

    [Fact]
    public async Task Invoke_WithBehaviors_ChainsThroughBehaviors()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<CovBehaviorStream, int>, CovBehaviorStreamHandler>();
        services.AddTransient<IStreamPipelineBehavior<CovBehaviorStream, int>, CovStreamBehavior>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream<CovBehaviorStream, int>(new CovBehaviorStream()))
            items.Add(item);

        items.ShouldBe(new[] { 10, 20 });
    }

    [Fact]
    public async Task Invoke_DirectCall_NoBehaviors()
    {
        // Call StreamPipelineInvoker.Invoke directly — covers the public static path
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<CovStream, int>, CovStreamHandler>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();

        var items = new List<int>();
        await foreach (var item in StreamPipelineInvoker.Invoke<CovStream, int>(
            new CovStream(), sp, CancellationToken.None))
            items.Add(item);

        items.ShouldBe(new[] { 10, 20 });
    }

    [Fact]
    public async Task Invoke_DirectCall_WithBehaviors()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<CovBehaviorStream, int>, CovBehaviorStreamHandler>();
        services.AddTransient<IStreamPipelineBehavior<CovBehaviorStream, int>, CovStreamBehavior>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();

        var items = new List<int>();
        await foreach (var item in StreamPipelineInvoker.Invoke<CovBehaviorStream, int>(
            new CovBehaviorStream(), sp, CancellationToken.None))
            items.Add(item);

        items.ShouldBe(new[] { 10, 20 });
    }
}

public class MediatorSendNullCoverageTests
{
    [Fact]
    public async Task Send_NullRequest_Throws()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => mediator.Send<CovPing, int>(null!).AsTask());
    }

    [Fact]
    public void CreateStream_NullRequest_Throws()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        Should.Throw<ArgumentNullException>(
            () => mediator.CreateStream<CovStream, int>(null!));
    }
}
