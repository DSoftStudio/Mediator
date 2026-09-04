// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.FluentValidation.Tests.Fixtures;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.FluentValidation.Tests;

public class ValidationBehaviorTests
{
    // ── Happy path ────────────────────────────────────────────────────

    [Fact]
    public async Task Valid_request_passes_through_to_handler()
    {
        var sp = TestServiceProvider.Build(s =>
            s.AddTransient<IValidator<CreateUser>, CreateUserValidator>());
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new CreateUser("Alice", "alice@example.com"), TestContext.Current.CancellationToken);

        result.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Request_without_validators_passes_through()
    {
        var sp = TestServiceProvider.Build();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new Ping(21), TestContext.Current.CancellationToken);

        result.ShouldBe(42);
    }

    // ── Single validator failures ─────────────────────────────────────

    [Fact]
    public async Task Invalid_request_throws_MediatorValidationException()
    {
        var sp = TestServiceProvider.Build(s =>
            s.AddTransient<IValidator<CreateUser>, CreateUserValidator>());
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new CreateUser("", "not-an-email")).AsTask());

        ex.Failures.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Empty_name_reports_name_failure()
    {
        var sp = TestServiceProvider.Build(s =>
            s.AddTransient<IValidator<CreateUser>, CreateUserValidator>());
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new CreateUser("", "alice@example.com")).AsTask());

        ex.Failures.ShouldContain(f => f.PropertyName == "Name");
    }

    [Fact]
    public async Task Invalid_email_reports_email_failure()
    {
        var sp = TestServiceProvider.Build(s =>
            s.AddTransient<IValidator<CreateUser>, CreateUserValidator>());
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new CreateUser("Alice", "bad")).AsTask());

        ex.Failures.ShouldContain(f => f.PropertyName == "Email");
    }

    [Fact]
    public async Task Both_fields_invalid_reports_multiple_failures()
    {
        var sp = TestServiceProvider.Build(s =>
            s.AddTransient<IValidator<CreateUser>, CreateUserValidator>());
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new CreateUser("", "")).AsTask());

        ex.Failures.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    // ── Multiple validators for same request ──────────────────────────

    [Fact]
    public async Task Multiple_validators_all_pass()
    {
        var sp = TestServiceProvider.BuildWithAllValidators();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TransferMoney("ACC-1", "ACC-2", 100m), TestContext.Current.CancellationToken);

        result.ShouldBe("transferred:100");
    }

    [Fact]
    public async Task Multiple_validators_aggregate_failures_from_all()
    {
        var sp = TestServiceProvider.BuildWithAllValidators();
        var mediator = sp.GetRequiredService<IMediator>();

        // Empty from/to (first validator) + negative amount (second validator)
        var ex = await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new TransferMoney("", "", -5m)).AsTask());

        // Should have failures from both validators
        ex.Failures.ShouldContain(f => f.PropertyName == "From");
        ex.Failures.ShouldContain(f => f.PropertyName == "To");
        ex.Failures.ShouldContain(f => f.PropertyName == "Amount");
    }

    [Fact]
    public async Task Multiple_validators_only_second_fails()
    {
        var sp = TestServiceProvider.BuildWithAllValidators();
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new TransferMoney("ACC-1", "ACC-2", -1m)).AsTask());

        ex.Failures.Count.ShouldBe(1);
        ex.Failures[0].PropertyName.ShouldBe("Amount");
    }

    [Fact]
    public async Task Multiple_validators_report_each_broken_rule_exactly_once()
    {
        var sp = TestServiceProvider.BuildWithAllValidators();
        var mediator = sp.GetRequiredService<IMediator>();

        // Three broken rules across two validators: From and To (account validator), Amount (amount validator).
        var ex = await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new TransferMoney("", "", -5m)).AsTask());

        ex.Failures.Count.ShouldBe(3);

        foreach (var entry in ex.ErrorsByProperty)
            entry.Value.Distinct().Count().ShouldBe(
                entry.Value.Length,
                $"'{entry.Key}' reported the same message more than once");

        ex.Message.ShouldStartWith("Validation failed with 3 errors.");
    }

    [Fact]
    public async Task Behavior_registered_twice_runs_the_validators_once()
    {
        var counter = new ValidatorCallCounter();

        // A shared library calls AddMediatorFluentValidation(); TestServiceProvider (playing the host)
        // calls it again. A second descriptor puts a second ValidationBehavior in the same chain, and
        // every validator then runs once per behavior.
        var sp = TestServiceProvider.Build(s =>
        {
            s.AddSingleton(counter);
            s.AddScoped<IValidator<Ping>, PingCountingValidator>();
            s.AddMediatorFluentValidation();
        });
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Send(new Ping(21), TestContext.Current.CancellationToken);

        counter.Count.ShouldBe(1);
    }

    // ── Constructor guards ────────────────────────────────────────────

    [Fact]
    public void Behavior_rejects_null_validators_naming_the_parameter()
    {
        var ex = Should.Throw<ArgumentNullException>(
            () => new ValidationBehavior<CreateUser, Guid>(null!));

        // Without an explicit guard the throw comes out of Enumerable.ToArray, blaming "source" —
        // a parameter the caller has never heard of.
        ex.ParamName.ShouldBe("validators");
    }

    // ── Handler is NOT invoked on failure ─────────────────────────────

    [Fact]
    public async Task Handler_is_not_invoked_when_validation_fails()
    {
        var sp = TestServiceProvider.Build(s =>
        {
            s.AddTransient<IValidator<CreateUser>, CreateUserValidator>();
        });
        var mediator = sp.GetRequiredService<IMediator>();

        // If handler were invoked, we'd get a Guid back — but validation should throw first
        await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new CreateUser("", "")).AsTask());
    }

    // ── Query with no validator ───────────────────────────────────────

    [Fact]
    public async Task Query_without_validator_executes_normally()
    {
        var sp = TestServiceProvider.Build();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new GetUser(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.ShouldStartWith("user:");
    }
}
