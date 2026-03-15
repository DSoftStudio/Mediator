// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.SelfHandling.Application.Commands;

/// <summary>
/// Void self-handling command — returns Unit.
/// Demonstrates the static void Execute pattern for fire-and-forget commands.
/// </summary>
public record LogMessageCommand(string Message) : ICommand<Unit>
{
    public static bool LastMessageLogged { get; private set; }

    internal static void Execute(LogMessageCommand cmd)
    {
        Console.WriteLine($"[LOG] {cmd.Message}");
        LastMessageLogged = true;
    }
}
