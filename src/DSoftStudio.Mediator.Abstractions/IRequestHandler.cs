// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Handles a request of type <typeparamref name="TRequest"/> and returns <typeparamref name="TResponse"/>.
    /// </summary>
    /// <typeparam name="TRequest">The request type, which must implement <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type the request produces.</typeparam>
    public interface IRequestHandler<in TRequest, TResponse>
      where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles <paramref name="request"/> and produces the response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The return type is <see cref="ValueTask{TResult}"/> rather than <see cref="Task{TResult}"/>
        /// on purpose: it avoids a heap allocation when the result is available synchronously, which
        /// is the key design choice behind zero-allocation dispatch.
        /// </para>
        /// <para>
        /// So when the work is synchronous, return a completed value rather than marking the method
        /// <c>async</c>. An <c>async</c> method that never suspends still builds an async state
        /// machine, and that cost is paid on every dispatch:
        /// </para>
        /// <code>
        /// public ValueTask&lt;int&gt; Handle(Ping request, CancellationToken ct)
        ///     =&gt; new ValueTask&lt;int&gt;(42);
        /// </code>
        /// <para>
        /// Use <c>async</c> when the handler genuinely awaits something. For a request that produces
        /// no meaningful value, use <see cref="Unit"/> as <typeparamref name="TResponse"/> and return
        /// <c>new ValueTask&lt;Unit&gt;(Unit.Value)</c>.
        /// </para>
        /// </remarks>
        /// <param name="request">The request to handle.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>The response for <paramref name="request"/>.</returns>
        ValueTask<TResponse> Handle(
            TRequest request,
            CancellationToken cancellationToken);
    }
}
