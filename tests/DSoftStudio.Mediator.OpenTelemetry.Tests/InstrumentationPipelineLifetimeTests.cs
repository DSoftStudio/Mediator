// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

/// <summary>
/// The instrumentation registers open-generic behaviors, so they land in EVERY request's and every
/// stream's chain — and the generated registration clears the cacheable flag and demotes the whole
/// chain to Transient the moment ONE component in it is Transient.
/// <para>
/// Registering these Transient therefore made turning telemetry on rebuild every chain on every
/// dispatch, application-wide, for requests that emit no metric at all. All three depend only on the
/// options and metrics singletons, so Singleton captures nothing narrower than itself.
/// </para>
/// <para>
/// Joins the serialized OTel collection: the dispatch flags these assert on are process-global.
/// </para>
/// </summary>
[Collection("OTel")]
public class InstrumentationPipelineLifetimeTests
{
    [Fact]
    public void The_instrumentation_behaviors_are_registered_as_singletons()
    {
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation();

        var behaviors = services
            .Where(s => s.ServiceType == typeof(IPipelineBehavior<,>)
                     || s.ServiceType == typeof(IStreamPipelineBehavior<,>))
            .ToList();

        behaviors.ShouldNotBeEmpty();

        // One Transient among them is enough to demote every chain, so this is asserted over all of
        // them rather than the one that happened to regress.
        behaviors.ShouldAllBe(s => s.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Enabling_metrics_leaves_the_request_chain_cacheable()
    {
        ResetDispatchFlags<TestCommand, string>();

        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddMediatorInstrumentation();
        services.PrecompilePipelines();

        // The end-to-end consequence, not just the descriptor: a Transient metrics behavior makes the
        // generated registration skip MarkPipelineChainCacheable, and PipelineChainCache is then never
        // used — every dispatch of every request in the application re-resolves and re-links its chain.
        RequestDispatch<TestCommand, string>.IsPipelineChainCacheable
            .ShouldBeTrue("instrumentation must not cost every request its cached pipeline chain");
    }

    [Fact]
    public void Enabling_instrumentation_leaves_the_stream_chain_cacheable()
    {
        ResetStreamDispatchFlags<TestStreamRequest, int>();

        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddMediatorInstrumentation();
        services.PrecompileStreams();

        // Tracing alone reaches this one: the stream tracing behavior is registered whenever tracing is
        // on, which is the default.
        StreamDispatch<TestStreamRequest, int>.IsStreamChainCacheable
            .ShouldBeTrue("instrumentation must not cost every stream its cached pipeline chain");
    }

    private static void ResetDispatchFlags<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
        => ResetFlags(typeof(RequestDispatch<TRequest, TResponse>),
                      "_hasPipelineChain", "_isPipelineChainCacheable");

    private static void ResetStreamDispatchFlags<TRequest, TResponse>()
        where TRequest : IStreamRequest<TResponse>
        => ResetFlags(typeof(StreamDispatch<TRequest, TResponse>),
                      "_isStreamChainCacheable");

    private static void ResetFlags(Type type, params string[] names)
    {
        foreach (var name in names)
        {
            // Deliberately not null-forgiving: if one of these fields is renamed, the reset would
            // silently stop happening and the test would pass on a flag another case had already set.
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            field.ShouldNotBeNull($"{type.Name} has no field '{name}' — the reset would be a no-op");
            field!.SetValue(null, false);
        }
    }
}
