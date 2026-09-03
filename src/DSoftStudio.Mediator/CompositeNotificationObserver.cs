// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Fans one publish out to several registered observers.
    /// <para>
    /// Two adapters observing at once is the normal case, not an exotic one: a tracing bridge and a
    /// profiler both want to watch, and neither can be asked to stand down for the other. Resolving a
    /// single observer meant whichever registered second silently disappeared.
    /// </para>
    /// <para>
    /// Only built when there really are two or more. One observer is used directly and pays nothing
    /// for this type, and none at all never reaches it.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class CompositeNotificationObserver(IMediatorNotificationObserver[] observers)
        : IMediatorNotificationObserver
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

        public IMediatorPublishScope? BeginPublish<TNotification>(TNotification notification)
            where TNotification : INotification
        {
            IMediatorPublishScope[]? opened = null;
            int count = 0;

            for (int i = 0; i < observers.Length; i++)
            {
                var observer = observers[i];
                if (!observer.IsActive)
                    continue;

                var scope = observer.BeginPublish(notification);
                if (scope is null)
                    continue;

                opened ??= new IMediatorPublishScope[observers.Length];
                opened[count++] = scope;
            }

            return count switch
            {
                0 => null,
                // One live observer is the common case even when several are registered — the others
                // are idle. Hand its scope back directly rather than wrapping it.
                1 => opened![0],
                _ => new CompositePublishScope(opened!, count),
            };
        }

        private sealed class CompositePublishScope(IMediatorPublishScope[] scopes, int count) : IMediatorPublishScope
        {
            public IMediatorSubscriberScope? BeginSubscriber(object handler)
            {
                IMediatorSubscriberScope[]? opened = null;
                int opencount = 0;

                for (int i = 0; i < count; i++)
                {
                    var subscriber = scopes[i].BeginSubscriber(handler);
                    if (subscriber is null)
                        continue;

                    opened ??= new IMediatorSubscriberScope[count];
                    opened[opencount++] = subscriber;
                }

                return opencount switch
                {
                    0 => null,
                    1 => opened![0],
                    _ => new CompositeSubscriberScope(opened!, opencount),
                };
            }

            public void OnSubscribersResolved(int subscriberCount)
            {
                for (int i = 0; i < count; i++)
                    scopes[i].OnSubscribersResolved(subscriberCount);
            }

            public void OnError(Exception exception)
            {
                for (int i = 0; i < count; i++)
                    scopes[i].OnError(exception);
            }

            public void Dispose()
            {
                // Reverse order, so an observer that made something ambient unwinds after the ones
                // opened inside it — the same discipline a nest of using blocks would give.
                for (int i = count - 1; i >= 0; i--)
                    scopes[i].Dispose();
            }
        }

        private sealed class CompositeSubscriberScope(IMediatorSubscriberScope[] scopes, int count) : IMediatorSubscriberScope
        {
            public void OnError(Exception exception)
            {
                for (int i = 0; i < count; i++)
                    scopes[i].OnError(exception);
            }

            public void Dispose()
            {
                for (int i = count - 1; i >= 0; i--)
                    scopes[i].Dispose();
            }
        }
    }
}
