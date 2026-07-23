// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

// Per-TFM artifacts subfolder so net10 and net11 runs never overwrite each other's results.
#if NET11_0_OR_GREATER
const string tfm = "net11.0";
#else
const string tfm = "net10.0";
#endif

var artifactsPath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "../../../../BenchmarkDotNet.Artifacts", tfm));

var config = DefaultConfig.Instance.WithArtifactsPath(artifactsPath);

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, config);
