// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.SelfHandling.Application.Commands;

/// <summary>
/// Sync self-handling command — no separate handler class needed.
/// The source generator discovers the static Execute method and wires it automatically.
/// </summary>
public record MultiplyCommand(int A, int B) : ICommand<int>
{
    internal static int Execute(MultiplyCommand cmd)
        => cmd.A * cmd.B;
}
