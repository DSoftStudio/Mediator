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

        var result = await mediator.Send(new CreateUser("Alice", "alice@example.com"));

        result.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Request_without_validators_passes_through()
    {
        var sp = TestServiceProvider.Build();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new Ping(21));

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

        var result = await mediator.Send(new TransferMoney("ACC-1", "ACC-2", 100m));

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

        var result = await mediator.Send(new GetUser(Guid.NewGuid()));

        result.ShouldStartWith("user:");
    }
}
