// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Fans one request dispatch out to several registered observers.
    /// <para>
    /// The request port used to take <c>observers[0]</c> and drop the rest without a word, so a
    /// second adapter — a profiler alongside a tracing bridge — simply never ran. The notification
    /// port fans out; this is the same rule for requests.
    /// </para>
    /// <para>
    /// Built once per scope, in <see cref="PipelineChainHandler{TRequest, TResponse}"/>'s constructor,
    /// and only when two or more are registered. One observer is used directly and never touches this
    /// type; none at all leaves the field null and the dispatch on its fast path.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class CompositeDispatchObserver(IMediatorDispatchObserver[] observers) : IMediatorDispatchObserver
    {
        public bool IsActive
        {
            get
            {
                for (int i = 0; i < observers.Length; i++)
                {
                    if (observers[i].IsActive)
                        return true;
                }

                return false;
            }
        }

        public IMediatorDispatchScope? BeginDispatch<TRequest, TResponse>(TRequest request, IRequestHandler<TRequest, TResponse> handler)
            where TRequest : IRequest<TResponse>
        {
            IMediatorDispatchScope[]? opened = null;
            int count = 0;

            for (int i = 0; i < observers.Length; i++)
            {
                var observer = observers[i];
                if (!observer.IsActive)
                    continue;

                var scope = observer.BeginDispatch(request, handler);
                if (scope is null)
                    continue;

                opened ??= new IMediatorDispatchScope[observers.Length];
                opened[count++] = scope;
            }

            return count switch
            {
                0 => null,
                // Several registered but only one live is the ordinary case — a bridge with no
                // exporter attached is idle. Hand its scope back rather than wrapping it.
                1 => opened![0],
                _ => new CompositeDispatchScope(opened!, count),
            };
        }

        private sealed class CompositeDispatchScope(IMediatorDispatchScope[] scopes, int count) : IMediatorDispatchScope
        {
            public void OnError(Exception exception)
            {
                for (int i = 0; i < count; i++)
                    scopes[i].OnError(exception);
            }

            public void Dispose()
            {
                // Reverse order, so an observer that made something ambient unwinds after anything
                // opened inside it — what a nest of using blocks would give.
                for (int i = count - 1; i >= 0; i--)
                    scopes[i].Dispose();
            }
        }
    }
}
