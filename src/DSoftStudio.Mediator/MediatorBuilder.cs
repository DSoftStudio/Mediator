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
    /// Replaces the default sequential notification publisher with a parallel implementation
    /// that invokes all notification handlers concurrently via <see cref="Task.WhenAll"/>.
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
