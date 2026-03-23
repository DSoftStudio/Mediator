// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Host.Application.Models;

/// <summary>
/// Simple DTO used to verify nullable reference-type responses
/// survive cross-assembly discovery via <c>ReferencedAssemblyScanner</c>.
/// </summary>
public sealed class UserDto
{
    public required string Name { get; init; }
}
