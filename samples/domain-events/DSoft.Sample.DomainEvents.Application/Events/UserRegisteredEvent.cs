// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.DomainEvents.Application.Events;

/// <summary>
/// Published when a new user registers.
/// Multiple handlers react independently to this single event.
/// </summary>
public record UserRegisteredEvent(Guid UserId, string Email) : INotification;
