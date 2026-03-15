// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoft.Sample.SelfHandling.Application.Services;

/// <summary>
/// Simple service to demonstrate DI injection into self-handling requests.
/// </summary>
public sealed class GreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}
