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

        /// <summary>
        /// The <c>(request, response)</c> pairs this behavior may legally be named closed over, as
        /// <c>"request|response"</c> keys — or EMPTY when it applies to every pair, which is the
        /// ordinary case.
        /// <para>
        /// Only a behavior that narrows itself with a constraint of its own
        /// (<c>where TRequest : IAuditable</c>) is restricted, and for one it carries the resolved
        /// answer rather than the question: the constraints were evaluated where the symbols were,
        /// and only strings cross into the incremental pipeline. Emitting a name the constraint
        /// rejects is CS0311 in a file the consumer cannot edit, and resolving one MSDI would have
        /// skipped is wrong even where it happens to compile.
        /// </para>
        /// </summary>
        public EquatableArray<string> ApplicablePairKeys { get; }

        /// <summary>
        /// Whether <see cref="ApplicablePairKeys"/> is a filter at all. Distinguishes "applies
        /// everywhere" (false) from "narrowed, and nothing matched" (true, empty) — which must emit
        /// nothing rather than everything.
        /// </summary>
        public bool IsNarrowed { get; }

        public BehaviorTypeInfo(PipelineInterfaceKind kind, string openTypeName, string baseTypeName)
            : this(kind, openTypeName, baseTypeName, isNarrowed: false, EquatableArray<string>.Empty)
        {
        }

        public BehaviorTypeInfo(
            PipelineInterfaceKind kind,
            string openTypeName,
            string baseTypeName,
            bool isNarrowed,
            EquatableArray<string> applicablePairKeys)
        {
            Kind = kind;
            OpenTypeName = openTypeName;
            BaseTypeName = baseTypeName;
            IsNarrowed = isNarrowed;
            ApplicablePairKeys = applicablePairKeys;
        }

        /// <summary>The key shape used by <see cref="ApplicablePairKeys"/>.</summary>
        public static string PairKey(string requestType, string responseType)
            => requestType + "|" + responseType;

        /// <summary>Whether this behavior may be named closed over the given pair.</summary>
        public bool AppliesTo(string requestType, string responseType)
        {
            if (!IsNarrowed)
                return true;

            var key = PairKey(requestType, responseType);
            foreach (var applicable in ApplicablePairKeys)
            {
                if (applicable == key)
                    return true;
            }

            return false;
        }

        public bool Equals(BehaviorTypeInfo other) =>
            Kind == other.Kind &&
            OpenTypeName == other.OpenTypeName &&
            BaseTypeName == other.BaseTypeName &&
            IsNarrowed == other.IsNarrowed &&
            ApplicablePairKeys.Equals(other.ApplicablePairKeys);

        public override bool Equals(object obj) =>
            obj is BehaviorTypeInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (OpenTypeName?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (BaseTypeName?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ IsNarrowed.GetHashCode();
                hash = (hash * 397) ^ ApplicablePairKeys.GetHashCode();
                return hash;
            }
        }
    }
}
