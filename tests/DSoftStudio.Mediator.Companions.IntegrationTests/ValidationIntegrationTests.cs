// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.FluentValidation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Companions.IntegrationTests;

/// <summary>
/// Validation through a real container and the real generator.
/// <para>
/// The package's own suite asserted that the right failures were PRESENT, never how many, and always
/// built the same happy wiring — which is how a defect that duplicated every message across validators
/// stayed invisible in twenty-one green tests. These go at exactness, at what the validator is handed,
/// and at what happens on the second dispatch rather than the first.
/// </para>
/// </summary>
public class ValidationIntegrationTests
{
    private static ServiceProvider Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        configure(services);
        services.AddMediatorFluentValidation();
        services.PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task The_callers_token_reaches_the_validator()
    {
        var probe = new TokenProbe();
        using var provider = Build(s =>
        {
            s.AddSingleton(probe);
            s.AddScoped<IValidator<SlowCommand>, SlowCommandValidator>();
        });

        using var cts = new CancellationTokenSource();
        await provider.GetRequiredService<IMediator>().Send(new SlowCommand("x"), cts.Token);

        // An async validator that ignores the token turns a cancelled request into work that keeps
        // running. Nothing in the existing suite passed a real token through to a validator at all.
        probe.Observed.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task Every_dispatch_validates_again_even_though_the_chain_is_cached()
    {
        var log = new CallLog();
        using var provider = Build(s =>
        {
            s.AddSingleton(log);
            s.AddScoped<IValidator<GetAccount>, GetAccountValidator>();
        });

        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Send(new GetAccount("a"), TestContext.Current.CancellationToken);
        await mediator.Send(new GetAccount("b"), TestContext.Current.CancellationToken);
        await mediator.Send(new GetAccount("c"), TestContext.Current.CancellationToken);

        // The chain is cached now, and the behavior is Scoped. Caching the CHAIN must never turn into
        // caching the VALIDATION — a request that skipped its rules because an earlier one passed is a
        // security hole, and the lifetime change that made the chain cacheable is exactly the kind of
        // change that could have caused it.
        log.ValidatorCalls.ShouldBe(3);
    }

    [Fact]
    public async Task A_scoped_validator_is_resolved_once_per_scope()
    {
        var seen = new ScopeObservations();
        using var provider = Build(s =>
        {
            s.AddSingleton(seen);
            s.AddScoped<ScopeMarker>();
            s.AddScoped<IValidator<ScopedCommand>, ScopedCommandValidator>();
        });

        for (int i = 0; i < 3; i++)
        {
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new ScopedCommand("x"), TestContext.Current.CancellationToken);
        }

        // Three scopes, three distinct markers. A behavior promoted to Singleton would have captured the
        // first scope's validator and served it to all three — the captive dependency the Scoped
        // registration exists to avoid, and which no descriptor assertion can detect.
        seen.Ids.Count.ShouldBe(3);
        seen.Ids.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task Two_validators_report_each_broken_rule_exactly_once_end_to_end()
    {
        var log = new CallLog();
        using var provider = Build(s =>
        {
            s.AddSingleton(log);
            s.AddScoped<IValidator<GetAccount>, GetAccountValidator>();
            s.AddScoped<IValidator<GetAccount>, GetAccountFormatValidator>();
        });

        var error = await Should.ThrowAsync<MediatorValidationException>(
            () => provider.GetRequiredService<IMediator>()
                .Send(new GetAccount(""), TestContext.Current.CancellationToken).AsTask());

        // Two DIFFERENT validators, one broken rule each. Two failures, two distinct messages. Under a
        // shared ValidationContext the second validator returned its own failure plus the first's, so
        // this read three, with "Account id is required." repeated — and the old suite's
        // ShouldContain-style assertions passed either way. Exactness is the whole point here.
        error.Failures.Count.ShouldBe(2);
        error.Message.ShouldStartWith("Validation failed with 2 errors.");

        var messages = error.Failures.Select(f => f.ErrorMessage).ToArray();
        messages.Distinct().Count().ShouldBe(messages.Length);

        foreach (var entry in error.ErrorsByProperty)
            entry.Value.Distinct().Count().ShouldBe(
                entry.Value.Length, $"'{entry.Key}' repeated a message");

        // And the handler never ran.
        log.HandlerCalls.ShouldBe(0);
    }
}
