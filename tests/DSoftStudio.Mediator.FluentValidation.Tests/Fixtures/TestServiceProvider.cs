// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.FluentValidation.Tests.Fixtures;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.FluentValidation.Tests;

internal static class TestServiceProvider
{
    /// <summary>
    /// Builds a fully configured service provider with mediator + FluentValidation.
    /// Optionally registers validators via <paramref name="configureValidators"/>.
    /// </summary>
    public static IServiceProvider Build(Action<IServiceCollection>? configureValidators = null)
    {
        var services = new ServiceCollection();

        services
            .AddMediator()
            .RegisterMediatorHandlers();

        configureValidators?.Invoke(services);

        services
            .AddMediatorFluentValidation()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds a provider with all standard test validators registered.
    /// </summary>
    public static IServiceProvider BuildWithAllValidators()
    {
        return Build(services =>
        {
            services.AddTransient<IValidator<CreateUser>, CreateUserValidator>();
            services.AddTransient<IValidator<TransferMoney>, TransferMoneyAccountValidator>();
            services.AddTransient<IValidator<TransferMoney>, TransferMoneyAmountValidator>();
        });
    }
}
