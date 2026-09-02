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
        /// Annotate the token parameter with <c>[EnumeratorCancellation]</c> so a token passed to
        /// <c>WithCancellation</c> on the consuming side reaches this method.
        /// </para>
        /// <para>
        /// Nothing runs until the returned stream is enumerated.
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
