// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

/// <summary>
/// Shared collection that serializes all OTel test classes.
/// Required because ActivitySource and Meter are static singletons —
/// concurrent listeners from parallel tests cause cross-contamination.
/// </summary>
[CollectionDefinition("OTel")]
public sealed class OTelTestCollection;
