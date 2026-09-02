// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator;

/// <summary>
/// Fluent builder for configuring the mediator pipeline at DI registration time.
/// <para>
/// Use within the <c>AddMediator(Action&lt;MediatorBuilder&gt;)</c> overload to register
/// open-generic behaviors, stream behaviors, pre/post processors, exception handlers,
/// and alternative notification publishers.
/// </para>
/// <example>
/// <code>
/// services.AddMediator(builder =&gt;
/// {
///     builder.AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;));
///     builder.AddParallelNotificationPublisher();
/// });
/// </code>
/// </example>
/// </summary>
public sealed class MediatorBuilder
{
    /// <summary>
    /// The service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MediatorBuilder"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public MediatorBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>
    /// Registers an open-generic pipeline behavior (<see cref="IPipelineBehavior{TRequest, TResponse}"/>).
    /// The source generator will close the generic for every discovered request/response pair at startup.
    /// </summary>
    /// <param name="behaviorType">
    /// An open-generic type implementing <see cref="IPipelineBehavior{TRequest, TResponse}"/>.
    /// Example: <c>typeof(LoggingBehavior&lt;,&gt;)</c>.
    /// </param>
    /// <param name="lifetime">The DI service lifetime. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="behaviorType"/> is not an open generic type.
    /// </exception>
    public MediatorBuilder AddOpenBehavior(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type behaviorType,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);

        if (!behaviorType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"Type '{behaviorType.Name}' must be an open generic type definition (e.g., typeof(MyBehavior<,>)).",
                nameof(behaviorType));

        Services.Add(new ServiceDescriptor(typeof(IPipelineBehavior<,>), behaviorType, lifetime));
        return this;
    }

    /// <summary>
    /// Registers a closed stream pipeline behavior.
    /// <typeparamref name="T"/> must implement <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/>
    /// for specific request/response types.
    /// </summary>
    /// <typeparam name="T">The concrete stream behavior type.</typeparam>
    /// <param name="lifetime">The DI service lifetime. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <typeparamref name="T"/> does not implement <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/>.
    /// </exception>
    public MediatorBuilder AddStreamBehavior<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] T>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where T : class
        => RegisterByOpenInterface(typeof(T), typeof(IStreamPipelineBehavior<,>), lifetime,
            nameof(T), "IStreamPipelineBehavior<TRequest, TResponse>");

    /// <summary>
    /// Registers a request pre-processor.
    /// <typeparamref name="T"/> must implement <see cref="IRequestPreProcessor{TRequest}"/>.
    /// </summary>
    /// <typeparam name="T">The concrete pre-processor type.</typeparam>
    /// <param name="lifetime">The DI service lifetime. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <typeparamref name="T"/> does not implement <see cref="IRequestPreProcessor{TRequest}"/>.
    /// </exception>
    public MediatorBuilder AddRequestPreProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] T>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where T : class
        => RegisterByOpenInterface(typeof(T), typeof(IRequestPreProcessor<>), lifetime,
            nameof(T), "IRequestPreProcessor<TRequest>");

    /// <summary>
    /// Registers a request post-processor.
    /// <typeparamref name="T"/> must implement <see cref="IRequestPostProcessor{TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="T">The concrete post-processor type.</typeparam>
    /// <param name="lifetime">The DI service lifetime. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <typeparamref name="T"/> does not implement <see cref="IRequestPostProcessor{TRequest, TResponse}"/>.
    /// </exception>
    public MediatorBuilder AddRequestPostProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] T>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where T : class
        => RegisterByOpenInterface(typeof(T), typeof(IRequestPostProcessor<,>), lifetime,
            nameof(T), "IRequestPostProcessor<TRequest, TResponse>");

    /// <summary>
    /// Registers a request exception handler.
    /// <typeparamref name="T"/> must implement <see cref="IRequestExceptionHandler{TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="T">The concrete exception handler type.</typeparam>
    /// <param name="lifetime">The DI service lifetime. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <typeparamref name="T"/> does not implement <see cref="IRequestExceptionHandler{TRequest, TResponse}"/>.
    /// </exception>
    public MediatorBuilder AddRequestExceptionHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] T>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where T : class
        => RegisterByOpenInterface(typeof(T), typeof(IRequestExceptionHandler<,>), lifetime,
            nameof(T), "IRequestExceptionHandler<TRequest, TResponse>");

    /// <summary>
    /// Registers a dispatch observer (<see cref="IMediatorDispatchObserver"/>) — an adapter that wraps the
    /// WHOLE request-dispatch boundary (pre-processors, behaviors, handler and post-processors), e.g. to open
    /// one tracing span around the entire pipeline. Unlike a pipeline behavior, an observer can nest the
    /// pre-/post-processors (which run outside the behavior chain) under its scope.
    /// <para>
    /// The mediator pays nothing when no observer is registered: the dispatch stays on its fast path. Defaults
    /// to <see cref="ServiceLifetime.Singleton"/> — an observer is a stateless cross-cutting adapter.
    /// </para>
    /// <para>
    /// Only ONE observer is ever used: the first one registered wins and any others are silently
    /// ignored. Compose several concerns inside a single adapter rather than registering several
    /// observers.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The concrete observer type implementing <see cref="IMediatorDispatchObserver"/>.</typeparam>
    /// <param name="lifetime">The DI service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>This builder for chaining.</returns>
    public MediatorBuilder AddDispatchObserver<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where T : class, IMediatorDispatchObserver
    {
        Services.Add(new ServiceDescriptor(typeof(IMediatorDispatchObserver), typeof(T), lifetime));
        return this;
    }

    /// <summary>
    /// Registers a pre-configured dispatch observer instance (<see cref="IMediatorDispatchObserver"/>). Use
    /// this overload when the observer carries configuration that cannot be resolved from DI (the OpenTelemetry
    /// bridge registers its tracing observer this way). See <see cref="AddDispatchObserver{T}(ServiceLifetime)"/>.
    /// <para>
    /// As with the other overload, only the first registered observer is used; any others are
    /// silently ignored.
    /// </para>
    /// </summary>
    /// <param name="observer">The observer instance to register as a singleton.</param>
    /// <returns>This builder for chaining.</returns>
    public MediatorBuilder AddDispatchObserver(IMediatorDispatchObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        Services.AddSingleton(observer);
        return this;
    }

    /// <summary>
    /// Registers <see cref="ParallelNotificationPublisher"/>, which queues every notification
    /// handler to the thread pool and awaits them together via <see cref="Task.WhenAll"/>.
    /// <para>
    /// Handlers then run concurrently whether or not they suspend, so they must be safe to run
    /// alongside each other. Awaiting the publish rethrows the first failure; the rest are on the
    /// task's <see cref="Task.Exception"/> aggregate.
    /// </para>
    /// <para>
    /// Registering any <see cref="INotificationPublisher"/> also changes how handlers are resolved
    /// — through <c>IEnumerable&lt;INotificationHandler&lt;T&gt;&gt;</c> instead of the generated
    /// concrete factories — which bypasses the generated fast path, costs a container lookup per
    /// publish, and yields a different singleton instance for a handler that keeps state.
    /// </para>
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public MediatorBuilder AddParallelNotificationPublisher()
    {
        Services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();
        return this;
    }

    /// <summary>
    /// Registers <paramref name="implementationType"/> against the closed
    /// <paramref name="openInterface"/> it implements, throwing when it implements none.
    /// Shared by the stream-behavior / pre-processor / post-processor / exception-handler registrations.
    /// </summary>
    private MediatorBuilder RegisterByOpenInterface(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] Type implementationType,
        Type openInterface,
        ServiceLifetime lifetime,
        string parameterName,
        string interfaceDisplayName)
    {
        foreach (var iface in implementationType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == openInterface)
            {
                Services.Add(new ServiceDescriptor(iface, implementationType, lifetime));
                return this;
            }
        }

        throw new ArgumentException(
            $"Type '{implementationType.Name}' does not implement {interfaceDisplayName}.",
            parameterName);
    }
}
