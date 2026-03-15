// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoft.Sample.SelfHandling.Application.Services;

namespace DSoft.Sample.SelfHandling.Application.Queries;

/// <summary>
/// Self-handling query with DI — the GreetingService parameter is resolved
/// from the container automatically by the generated adapter.
/// </summary>
public record GreetQuery(string Name) : IQuery<string>
{
    internal static string Execute(GreetQuery query, GreetingService greeter)
        => greeter.Greet(query.Name);
}
