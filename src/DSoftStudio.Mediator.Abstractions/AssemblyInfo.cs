// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

// This package advertises native-aot in its PackageTags and is the one every consumer references,
// yet it shipped carrying no trim marker at all. The cause is the target framework: <IsTrimmable>
// and <IsAotCompatible> are no-ops on netstandard2.0, so setting them in the csproj — as the other
// four packages do — would change nothing here.
//
// Those properties exist only to emit this attribute, and there is no separate IsAotCompatible
// attribute in ECMA metadata; IsTrimmable IS the marker the trimmer and the AOT analyzers read.
// Emitting it directly is therefore the whole fix, and it applies to the assembly we actually ship
// rather than to a target framework we would have had to add.
//
// True by construction: this assembly is contracts only — interfaces, two attributes and Unit. It
// holds no method body that could reflect. AotCompatibilityTests enforces that rather than trusting
// it, scanning the compiled assembly for the APIs NativeAOT cannot compile.
[assembly: AssemblyMetadata("IsTrimmable", "True")]
