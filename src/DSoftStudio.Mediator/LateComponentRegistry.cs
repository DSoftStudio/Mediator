// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator;

/// <summary>
/// The pipeline components a second <c>AddMediator(configure)</c> registered after the chains had
/// already been built. Present in the service collection ONLY when there is something to report, and
/// read by the generated <c>ValidateMediatorHandlers()</c>.
/// </summary>
public sealed class LatePipelineComponentReport
{
    /// <summary>Creates a report over the component service types that arrived late.</summary>
    /// <param name="componentServiceTypes">The service types, exactly as registered.</param>
    public LatePipelineComponentReport(Type[] componentServiceTypes)
    {
        ArgumentNullException.ThrowIfNull(componentServiceTypes);
        ComponentServiceTypes = componentServiceTypes;
    }

    /// <summary>The component service types registered after the scan, in registration order.</summary>
    public IReadOnlyList<Type> ComponentServiceTypes { get; }
}

/// <summary>
/// Records pipeline components registered by an <c>AddMediator(configure)</c> call that ran AFTER the
/// pipeline scan had already fixed every chain, so that <c>ValidateMediatorHandlers()</c> can name them.
/// <para>
/// The generated overload's <c>configure(builder)</c> step is unguarded — it registers whatever the
/// lambda asks for on every call — while <c>RegisterPipelineChains</c> returns early on its sentinel.
/// A second call therefore adds components that no chain will ever be rebuilt around: they are either
/// silently never run, or, when the first scan produced a Singleton chain, they are captured by it and
/// the container refuses to build under <c>ValidateScopes</c>.
/// </para>
/// <para>
/// Detection is by OBSERVATION, not inference: the sentinel proves the chains were already frozen, and
/// only the descriptors appended by this call are examined. That is what keeps it free of false
/// positives — a second bare <c>PrecompilePipelines()</c> registers nothing and is still the documented
/// no-op, and a configure lambda that only calls <c>AddParallelNotificationPublisher()</c> adds no
/// component and is not reported.
/// </para>
/// <para>
/// Cost is registration-time only and proportional to what the lambda added: nothing is registered when
/// there is nothing to report, no reflection is used beyond a generic-definition comparison, and the
/// dispatch path never sees any of it.
/// </para>
/// </summary>
public static class LateComponentRegistry
{
    /// <summary>
    /// The service types whose registration order decides whether a chain is built for them. A dispatch
    /// observer is included because the chain's constructor consumes it, so it is as late as any behavior.
    /// </summary>
    private static readonly Type[] ComponentServiceTypes =
    [
        typeof(IPipelineBehavior<,>),
        typeof(IRequestPreProcessor<>),
        typeof(IRequestPostProcessor<,>),
        typeof(IRequestExceptionHandler<,>),
        typeof(IStreamPipelineBehavior<,>),
        typeof(IMediatorDispatchObserver),
    ];

    /// <summary>
    /// Examines the descriptors appended since <paramref name="firstIndexAdded"/> and records any that
    /// register a pipeline component. Called by generated code only when the pipeline sentinel was
    /// already present before the configure lambda ran.
    /// </summary>
    /// <param name="services">The collection the late call registered into.</param>
    /// <param name="firstIndexAdded">The collection's count immediately before the configure lambda ran.</param>
    public static void Record(IServiceCollection services, int firstIndexAdded)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (firstIndexAdded < 0)
            return;

        List<Type>? late = null;
        for (int i = firstIndexAdded; i < services.Count; i++)
        {
            var serviceType = services[i].ServiceType;

            // A component is registered open (typeof(IPipelineBehavior<,>)) or closed
            // (IPipelineBehavior<Ping, int>); both have to match the same entry.
            var probe = serviceType.IsGenericType && !serviceType.IsGenericTypeDefinition
                ? serviceType.GetGenericTypeDefinition()
                : serviceType;

            foreach (var component in ComponentServiceTypes)
            {
                if (probe != component)
                    continue;

                (late ??= []).Add(serviceType);
                break;
            }
        }

        if (late is null)
            return;

        // Added, not TryAdded: a third late call is its own report, and the validator reads them all.
        services.AddSingleton(new LatePipelineComponentReport([.. late]));
    }
}
