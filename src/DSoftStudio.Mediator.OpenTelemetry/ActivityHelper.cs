// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Helper methods for <see cref="Activity"/> operations.
/// </summary>
internal static class ActivityHelper
{
    /// <summary>
    /// Records an exception as an Activity event following OTel semantic conventions.
    /// </summary>
    public static void RecordException(Activity activity, Exception exception, bool includeStackTrace)
    {
        var tags = new ActivityTagsCollection
        {
            { "exception.type", exception.GetType().FullName! },
            { "exception.message", exception.Message }
        };

        if (includeStackTrace)
            tags.Add("exception.stacktrace", exception.ToString());

        activity.AddEvent(new ActivityEvent("exception", tags: tags));
    }
}
