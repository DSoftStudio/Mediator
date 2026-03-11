// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoftStudio.Mediator.Abstractions {
    /// <summary>
    /// Marker interface for a request that returns <typeparamref name="TResponse"/>.
    /// Commands and queries implement this to participate in the mediator pipeline.
    /// </summary>
    public interface IRequest<out TResponse> { };
}


