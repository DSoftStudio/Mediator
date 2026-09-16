// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoftStudio.Mediator.HybridCache;

/// <summary>
/// Turns a deferred, misdirected caching failure into one that says what to do.
/// <para>
/// <c>HybridCache</c> serializes every payload it caches, and with no
/// <c>IHybridCacheSerializer&lt;T&gt;</c> registered for the type it falls back to reflection-based
/// <c>System.Text.Json</c>. Native AOT and trimming disable exactly that, so the first dispatch of a
/// cacheable request throws — after the build succeeded, after the AOT publish succeeded, and after
/// the application started. ILC does warn (IL2026 / IL3050), but it warns about Microsoft's
/// serializer rather than about this pipeline, and it does not fail the publish. The message that
/// reaches the developer names neither the request nor the remedy.
/// </para>
/// <para>
/// <b>Why a <see langword="string"/> response caches fine and a DTO does not.</b> Not immutability —
/// that was the obvious guess and it is wrong. <c>AddHybridCache</c> pre-registers a serializer for
/// exactly two types, <see langword="string"/> and <c>byte[]</c>; everything else reaches the
/// reflective fallback. Marking a DTO <c>[ImmutableObject(true)]</c> does NOT help: verified in a
/// native binary, a sealed record carrying it throws exactly like an unmarked one, because the
/// serializer-free path needs writes disabled on BOTH cache tiers, which means caching nothing at
/// all. Immutability changes only the read side — the cached instance is handed back directly
/// instead of being deserialized per read — which is also why that marker is a hazard rather than a
/// workaround: measured, one caller mutated its result and the next caller received the same
/// instance carrying that mutation.
/// </para>
/// <para>
/// Non-generic on purpose. Held inside <c>CachingBehavior&lt;TRequest, TResponse&gt;</c> this text
/// would be compiled once per closed pair, for a path that exists only to explain a failure.
/// </para>
/// </summary>
internal static class CachingBehaviorGuidance
{
    public static string For<TResponse>(Exception inner) =>
        $"Caching '{typeof(TResponse)}' failed because reflection-based JSON serialization is "
        + "disabled, which is the default under Native AOT and trimming. HybridCache serializes "
        + "every payload it caches, and only string and byte[] come with a serializer registered. "
        + "To fix, declare a JsonSerializerContext listing the cached response types and register it "
        + "as the serializer options, keyed on the open generic:\n"
        + "    [JsonSerializable(typeof(" + typeof(TResponse).Name + "))]\n"
        + "    internal sealed partial class AppJsonContext : JsonSerializerContext;\n"
        + "    services.AddKeyedSingleton<JsonSerializerOptions>(\n"
        + "        typeof(IHybridCacheSerializer<>),\n"
        + "        new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default });\n"
        + "The explicit type argument and the open-generic key are both required: without them the "
        + "registration either does not compile or is silently ignored. Alternatively register an "
        + "IHybridCacheSerializer<> for this type, or stop caching this request. The original "
        + "failure is kept as the inner exception: "
        + inner.Message;
}
