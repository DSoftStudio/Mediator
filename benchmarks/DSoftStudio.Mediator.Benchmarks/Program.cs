// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

var artifactsPath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "../../../../BenchmarkDotNet.Artifacts"));

var config = DefaultConfig.Instance.WithArtifactsPath(artifactsPath);

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, config);
