// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Buffers;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Caching.Hybrid;
using Cache = Microsoft.Extensions.Caching.Hybrid.HybridCache;

namespace DSoftStudio.Mediator.HybridCache.Tests;

/// <summary>
/// Under Native AOT the caching pipeline fails in the worst possible place: not at build time, not
/// at publish time, but on the first dispatch of a cacheable request in the published application.
/// <para>
/// <c>HybridCache</c> serializes everything it caches, and <c>AddHybridCache</c> pre-registers a
/// serializer for exactly two types — <see langword="string"/> and <c>byte[]</c>. Anything else
/// falls back to reflection-based <c>System.Text.Json</c>, which AOT disables. Measured: an ordinary
/// DTO threw <c>"Reflection-based serialization has been disabled for this application"</c> from
/// inside Microsoft's serializer, with nothing in the message about this pipeline, the cached
/// request, or what to register. A <see langword="string"/> response cached fine, which is how a
/// project stays quiet until its first DTO — and it is the pre-registered serializer that saves it,
/// not immutability, which was the obvious guess and is wrong.
/// </para>
/// <para>
/// The behavior cannot prevent that — the reflection belongs to the container's serializer — but it
/// can say what happened and what to do, and keep the original as the inner exception.
/// </para>
/// </summary>
public class SerializerGuidanceTests
{
    public sealed record Payload(int Value);

    public sealed record CachedQuery : IRequest<Payload>, ICachedRequest
    {
        public string CacheKey => "k";
        public TimeSpan Duration => TimeSpan.FromMinutes(1);
    }

    private sealed class PassThroughHandler : IRequestHandler<CachedQuery, Payload>
    {
        public ValueTask<Payload> Handle(CachedQuery request, CancellationToken ct) => new(new Payload(1));
    }

    /// <summary>
    /// Stands in for what <c>DefaultHybridCache</c> does once reflection-based JSON is off: the same
    /// exception type, and the same message text Microsoft's serializer produces.
    /// </summary>
    private sealed class ThrowingCache : Cache
    {
        public const string ReflectionDisabled =
            "Reflection-based serialization has been disabled for this application. Either use the "
            + "source generator APIs or explicitly configure the 'JsonSerializerOptions.TypeInfoResolver' property.";

        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key, TState state, Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ReflectionDisabled);

        public override ValueTask SetAsync<T>(
            string key, T value, HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
            => default;

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
            => default;

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
            => default;
    }

    private static async Task<InvalidOperationException> Dispatch()
    {
        var behavior = new CachingBehavior<CachedQuery, Payload>(new ThrowingCache());

        return await Should.ThrowAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new CachedQuery(), new PassThroughHandler(), CancellationToken.None));
    }

    /// <summary>
    /// The guidance only replaces the message where reflection-based JSON is actually off. On an
    /// ordinary run it is not, so the filter declines and the original exception passes through
    /// untouched — which is also what keeps this free for every dispatch that does not throw.
    /// </summary>
    [Fact]
    public async Task Original_Exception_Is_Untouched_While_Reflection_Is_Available()
    {
        if (!System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault)
            return;

        var thrown = await Dispatch();

        thrown.Message.ShouldBe(ThrowingCache.ReflectionDisabled,
            customMessage: "the filter rewrote a message it had no business rewriting");
        thrown.InnerException.ShouldBeNull();
    }

    /// <summary>
    /// Everything the rewritten message has to carry, asserted where the filter is known to engage.
    /// The test process runs with reflection enabled, so this exercises the message builder directly
    /// rather than pretending the runtime switch can be flipped underneath it.
    /// </summary>
    [Fact]
    public void Guidance_Names_The_Type_The_Remedies_And_Keeps_The_Original()
    {
        var original = new InvalidOperationException(ThrowingCache.ReflectionDisabled);

        var guidance = CachingBehaviorGuidance.For<Payload>(original);

        guidance.ShouldContain(nameof(Payload), customMessage: "the failing response type is not named");
        guidance.ShouldContain("IHybridCacheSerializer");
        guidance.ShouldContain("TypeInfoResolver");
        guidance.ShouldContain(ThrowingCache.ReflectionDisabled,
            customMessage: "the original failure was dropped instead of carried");
    }
}
