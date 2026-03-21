// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoftStudio.Mediator.Generators
{
    /// <summary>
    /// Identifies which open-generic pipeline interface a behavior type implements.
    /// </summary>
    internal enum PipelineInterfaceKind
    {
        /// <summary><c>IPipelineBehavior&lt;TRequest, TResponse&gt;</c></summary>
        Behavior,

        /// <summary><c>IRequestPostProcessor&lt;TRequest, TResponse&gt;</c></summary>
        PostProcessor,

        /// <summary><c>IRequestExceptionHandler&lt;TRequest, TResponse&gt;</c></summary>
        ExceptionHandler,

        /// <summary><c>IStreamPipelineBehavior&lt;TRequest, TResponse&gt;</c></summary>
        StreamBehavior
    }

    /// <summary>
    /// Describes an open-generic pipeline behavior type discovered at compile time.
    /// Used by the source generator to emit AOT-safe closed-generic DI registrations
    /// that replace the open-generic descriptors before the DI container attempts
    /// <c>MakeGenericType</c> (which fails for value-type <c>TResponse</c> under Native AOT).
    /// </summary>
    internal readonly struct BehaviorTypeInfo : System.IEquatable<BehaviorTypeInfo>
    {
        /// <summary>
        /// The kind of pipeline interface this behavior implements.
        /// </summary>
        public PipelineInterfaceKind Kind { get; }

        /// <summary>
        /// Fully qualified type name in open-generic form for <c>typeof</c> comparison.
        /// Example: <c>"global::DSoftStudio.Mediator.FluentValidation.ValidationBehavior&lt;,&gt;"</c>
        /// </summary>
        public string OpenTypeName { get; }

        /// <summary>
        /// Fully qualified type name without generic type parameters.
        /// Example: <c>"global::DSoftStudio.Mediator.FluentValidation.ValidationBehavior"</c>
        /// </summary>
        public string BaseTypeName { get; }

        public BehaviorTypeInfo(PipelineInterfaceKind kind, string openTypeName, string baseTypeName)
        {
            Kind = kind;
            OpenTypeName = openTypeName;
            BaseTypeName = baseTypeName;
        }

        public bool Equals(BehaviorTypeInfo other) =>
            Kind == other.Kind &&
            OpenTypeName == other.OpenTypeName &&
            BaseTypeName == other.BaseTypeName;

        public override bool Equals(object obj) =>
            obj is BehaviorTypeInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (OpenTypeName?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (BaseTypeName?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
