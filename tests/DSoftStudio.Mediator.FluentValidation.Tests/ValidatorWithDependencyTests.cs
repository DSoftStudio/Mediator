// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.FluentValidation.Tests.Fixtures;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.FluentValidation.Tests;

public class ValidatorWithDependencyTests
{
    [Fact]
    public async Task Validator_with_DI_dependency_resolves_and_validates()
    {
        var sp = TestServiceProvider.Build(s =>
        {
            s.AddSingleton<IBlockedAccountService, BlockedAccountService>();
            s.AddTransient<IValidator<TransferMoney>, TransferMoneyBlockedValidator>();
        });
        var mediator = sp.GetRequiredService<IMediator>();

        // BLOCKED-001 is blocked by the service
        var ex = await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new TransferMoney("BLOCKED-001", "ACC-2", 50m)).AsTask());

        ex.Failures.ShouldContain(f => f.PropertyName == "From");
        ex.Failures[0].ErrorMessage.ShouldContain("blocked");
    }

    [Fact]
    public async Task Validator_with_DI_passes_when_not_blocked()
    {
        var sp = TestServiceProvider.Build(s =>
        {
            s.AddSingleton<IBlockedAccountService, BlockedAccountService>();
            s.AddTransient<IValidator<TransferMoney>, TransferMoneyBlockedValidator>();
        });
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TransferMoney("ACC-1", "ACC-2", 50m));

        result.ShouldBe("transferred:50");
    }
}
