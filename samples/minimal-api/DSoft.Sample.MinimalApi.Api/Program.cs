// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoft.Sample.MinimalApi.Api.Endpoints;
using DSoftStudio.Mediator;

var builder = WebApplication.CreateBuilder(args);

// Register mediator + handlers + precompile pipelines
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompilePipelines();

// Add a fallback authorization policy for the RefundOrder endpoint demo
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManagerPolicy", policy => policy.RequireAssertion(_ => true));
});

var app = builder.Build();

// Map endpoint groups
app.MapUserEndpoints();
app.MapOrderEndpoints();

app.Run();
