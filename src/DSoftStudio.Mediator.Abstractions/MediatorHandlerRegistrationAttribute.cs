// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Assembly-level attribute emitted by the source generator to advertise
    /// handler registrations to downstream (referencing) projects.
    /// <para>
    /// Each project's <c>DependencyInjectionGenerator</c> discovers local handlers
    /// and emits one attribute per handler. Generators in referencing projects read
    /// these attributes to build a complete, cross-assembly registry without reflection.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class MediatorHandlerRegistrationAttribute : Attribute
    {
        /// <summary>
        /// The service interface type (e.g. <c>IRequestHandler&lt;TRequest, TResponse&gt;</c>).
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// The concrete handler implementation type.
        /// </summary>
        public Type ImplementationType { get; }

        public MediatorHandlerRegistrationAttribute(Type serviceType, Type implementationType)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
        }
    }
}
