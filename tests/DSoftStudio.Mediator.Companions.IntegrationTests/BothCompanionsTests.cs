// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.FluentValidation;
using DSoftStudio.Mediator.HybridCache;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Companions.IntegrationTests;

/// <summary>
/// Caching and validation on the SAME request. Neither package's suite could see this — each test
/// project references only its own package — so the one thing a real application does routinely was
/// the one thing nothing exercised.
/// <para>
/// Behaviors run in registration order, and here that order decides whether a cache hit is validated
/// at all. That is a security-relevant consequence of a line's position in Program.cs, so it is
/// pinned rather than described.
/// </para>
/// </summary>
public class BothCompanionsTests
{
    private static ServiceProvider Build(CallLog log, bool validateFirst)
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddHybridCache();
        services.AddSingleton(log);
        services.AddSingleton(new ReportGate());
        services.AddScoped<IValidator<GetAccount>, GetAccountValidator>();

        if (validateFirst)
        {
            services.AddMediatorFluentValidation();
            services.AddMediatorHybridCache();
        }
        else
        {
            services.AddMediatorHybridCache();
            services.AddMediatorFluentValidation();
        }

        services.PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Validation_registered_first_still_runs_on_a_cache_hit()
    {
        var log = new CallLog();
        using var provider = Build(log, validateFirst: true);
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Send(new GetAccount("acct-1"), TestContext.Current.CancellationToken);
        await mediator.Send(new GetAccount("acct-1"), TestContext.Current.CancellationToken);

        // Validation is the OUTER behavior, so it sees both dispatches; caching is inner and serves the
        // second from its entry. This is the order an application wants: the cache never lets an
        // unvalidated request through, because validation happens before the cache is consulted.
        log.HandlerCalls.ShouldBe(1);
        log.ValidatorCalls.ShouldBe(2);
    }

    [Fact]
    public async Task Caching_registered_first_serves_a_hit_WITHOUT_validating_it()
    {
        var log = new CallLog();
        using var provider = Build(log, validateFirst: false);
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Send(new GetAccount("acct-2"), TestContext.Current.CancellationToken);
        await mediator.Send(new GetAccount("acct-2"), TestContext.Current.CancellationToken);

        // Caching is the OUTER behavior here, so a hit returns before validation is ever reached. The
        // second dispatch is not validated at all.
        //
        // This is a consequence of registration order, not a defect — but it is the kind of thing that
        // has to be written down and pinned, because "swap two lines in Program.cs" is not obviously a
        // security decision, and the rules a request skips are silently skipped.
        log.HandlerCalls.ShouldBe(1);
        log.ValidatorCalls.ShouldBe(1);
    }

    [Fact]
    public async Task An_invalid_request_never_reaches_the_cache_in_either_order()
    {
        foreach (var validateFirst in new[] { true, false })
        {
            var log = new CallLog();
            using var provider = Build(log, validateFirst);
            var mediator = provider.GetRequiredService<IMediator>();

            await Should.ThrowAsync<MediatorValidationException>(
                () => mediator.Send(new GetAccount(""), TestContext.Current.CancellationToken).AsTask());

            // A rejected request must leave nothing behind: the handler never ran, so there is no value
            // to cache, and a later valid dispatch of the same key must still reach the handler.
            log.HandlerCalls.ShouldBe(0, $"validateFirst={validateFirst}");

            await Should.ThrowAsync<MediatorValidationException>(
                () => mediator.Send(new GetAccount(""), TestContext.Current.CancellationToken).AsTask());

            // And it is rejected again rather than served a cached failure.
            log.HandlerCalls.ShouldBe(0, $"validateFirst={validateFirst}");
        }
    }

    [Fact]
    public async Task Both_companions_together_leave_the_pipeline_chain_cacheable()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddHybridCache();
        services.AddSingleton(new CallLog());
        services.AddSingleton(new ReportGate());
        services.AddMediatorFluentValidation();
        services.AddMediatorHybridCache();
        services.PrecompilePipelines();

        // Two components in one chain, registered by two packages with two different lifetimes —
        // Scoped for validation, Singleton for caching. The chain takes the narrowest of them and stays
        // cacheable; one Transient among them would have demoted it, which is exactly what both
        // packages used to do.
        var chain = services.SingleOrDefault(d =>
            d.ServiceType == typeof(PipelineChainHandler<GetAccount, string>));

        chain.ShouldNotBeNull();
        chain.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        RequestDispatch<GetAccount, string>.IsPipelineChainCacheable.ShouldBeTrue();

        await Task.CompletedTask;
    }
}
