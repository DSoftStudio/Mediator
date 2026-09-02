// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Marks a request whose response is a stream of <typeparamref name="TResponse"/> values,
    /// dispatched with <c>CreateStream</c> rather than <c>Send</c>.
    /// <para>
    /// Useful for large datasets, event feeds and progressive responses, where buffering the whole
    /// result set in memory is impractical.
    /// </para>
    /// </summary>
    /// <typeparam name="TResponse">The type of each item produced by the stream.</typeparam>
    public interface IStreamRequest<out TResponse> { }

    /// <summary>
    /// Handles a stream request of type <typeparamref name="TRequest"/>, producing
    /// <typeparamref name="TResponse"/> items as they become available.
    /// </summary>
    /// <typeparam name="TRequest">The request type, which must implement <see cref="IStreamRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The type of each item produced by the stream.</typeparam>
    public interface IStreamRequestHandler<in TRequest, out TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        /// <summary>
        /// Produces the stream of responses for <paramref name="request"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Implemented as an iterator, yielding each item as it is ready:
        /// </para>
        /// <code>
        /// public async IAsyncEnumerable&lt;int&gt; Handle(
        ///     GetNumbers request,
        ///     [EnumeratorCancellation] CancellationToken ct)
        /// {
        ///     yield return 1;
        ///     yield return 2;
        ///     yield return 3;
        /// }
        /// </code>
        /// <para>
        /// The token passed to <c>CreateStream</c> always arrives here. Annotate the parameter with
        /// <c>[EnumeratorCancellation]</c> so that a token supplied on the consuming side with
        /// <c>WithCancellation</c> reaches it too; without the attribute only that second token is
        /// lost, and the compiler warns (CS8425).
        /// </para>
        /// <para>
        /// Written as an iterator, this body does not start until the returned stream is enumerated.
        /// Dispatch itself is not deferred: <c>CreateStream</c> resolves the handler and builds the
        /// behavior chain when it is called, so a stream created inside a scope has already captured
        /// both even if it is enumerated later — and enumerating it after the scope is disposed uses
        /// the objects it captured.
        /// </para>
        /// </remarks>
        /// <param name="request">The request to handle.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>The stream of responses.</returns>
        IAsyncEnumerable<TResponse> Handle(
            TRequest request,
            CancellationToken cancellationToken);
    }
}
