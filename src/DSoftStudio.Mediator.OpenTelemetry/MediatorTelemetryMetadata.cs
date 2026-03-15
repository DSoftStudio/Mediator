// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Per-type metadata cache for request tracing and metrics.
/// The CLR creates one set of static fields per closed generic type.
/// Fields are initialized once on first access and read as direct field loads (~1 ns).
/// </summary>
internal static class MediatorTelemetryMetadata<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public static readonly string RequestKind = DetectKind();
    public static readonly string SpanName = $"{typeof(TRequest).Name} {RequestKind}";
    public static readonly string RequestType = typeof(TRequest).FullName!;
    public static readonly string ResponseType = typeof(TResponse).FullName!;

    private static string DetectKind()
    {
        if (typeof(ICommand).IsAssignableFrom(typeof(TRequest))) return "command";
        if (typeof(IQuery).IsAssignableFrom(typeof(TRequest))) return "query";
        return "request";
    }
}

/// <summary>
/// Per-type metadata cache for stream tracing and metrics.
/// </summary>
internal static class MediatorStreamMetadata<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public static readonly string SpanName = $"{typeof(TRequest).Name} stream";
    public static readonly string RequestType = typeof(TRequest).FullName!;
    public static readonly string ResponseType = typeof(TResponse).FullName!;
    public static readonly string RequestKind = "stream";
}

/// <summary>
/// Per-type metadata cache for notification tracing and metrics.
/// </summary>
internal static class MediatorNotificationMetadata<TNotification>
    where TNotification : INotification
{
    public static readonly string SpanName = $"{typeof(TNotification).Name} publish";
    public static readonly string RequestType = typeof(TNotification).FullName!;
    public static readonly string RequestKind = "notification";
}
