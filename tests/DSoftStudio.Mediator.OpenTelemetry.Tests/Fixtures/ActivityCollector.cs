// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

/// <summary>
/// Captures activities from the mediator ActivitySource for test assertions.
/// </summary>
internal sealed class ActivityCollector : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = [];

    public IReadOnlyList<Activity> Activities => _activities;

    public ActivityCollector()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MediatorInstrumentation.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { },
            ActivityStopped = activity => _activities.Add(activity)
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();
}
