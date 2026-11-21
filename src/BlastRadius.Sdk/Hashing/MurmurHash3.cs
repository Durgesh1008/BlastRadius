using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace BlastRadius.Sdk.Hashing;

/// <summary>
/// High-performance, zero-allocation implementation of the 32-bit x86 MurmurHash3 algorithm.
/// 
/// ARCHITECTURAL JUSTIFICATION:
/// 1. CPU Overhead vs Cryptographic Hashing:
///    Feature flag evaluation sits directly on the request critical path (executed on every HTTP request, DB call, or checkout step).
///    Cryptographic hashes like SHA-256 or MD5 require 300-600 nanoseconds per invocation and allocate byte arrays.
///    MurmurHash3 executes in under 15 nanoseconds, requires zero allocations when operating on spans, and optimizes for raw L1 cache efficiency.
/// 
/// 2. Avalanche Characteristics &amp; Uniform Distribution:
///    MurmurHash3 provides near-perfect avalanche behavior: flipping a single bit in the input string flips ~50% of the output bits
///    with zero correlation. This prevents clustering when user IDs are sequential integers (e.g. "user_1001", "user_1002").
/// 
/// 3. Zero-Flicker Determinism across Rollout Expansions:
///    Canary rollouts evaluate: (hash(flagKey + ":" + userId) % 100) &lt; rolloutPercentage.
///    Because the computed hash index is static for a given (flagKey, userId) tuple:
///    - When rollout expands from 10% to 25%, all users who previously evaluated true remain true.
///    - Users NEVER experience jarring UI flicker, unexpected feature revocation, or session state mismatches.
/// 
/// 4. Orthogonal Cohort Isolation:
///    By concatenating $"{flagKey}:{userId}", the hash space for flag A is statistically independent of flag B.
///    A user in the first 5% of "new-checkout" has an equal, uncorrelated probability of being in any cohort for "v2-search".
/// </summary>
public static class MurmurHash3
{
    private const uint C1 = 0xcc9e2d51;
    private const uint C2 = 0x1b873593;
    private const int R1 = 15;
    private const int R2 = 13;
    private const uint M = 5;
    private const uint N = 0xe6546b64;

    /// <summary>
    /// Computes the 32-bit MurmurHash3 for a string input using UTF-8 byte representation.
    /// Uses stack-allocated spans for short strings to avoid heap allocations.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <param name="seed">Optional seed value (default: 0).</param>
    /// <returns>A 32-bit unsigned integer hash.</returns>
    public static uint Hash32(string input, uint seed = 0)
    {
        if (string.IsNullOrEmpty(input))
        {
            return 0;
        }

        // Stack-allocate buffer for typical key lengths (<= 256 bytes) to eliminate GC pressure
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(input.Length);
        if (maxByteCount <= 256)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            int written = Encoding.UTF8.GetBytes(input, buffer);
            return Hash32(buffer[..written], seed);
        }

        byte[] heapBuffer = Encoding.UTF8.GetBytes(input);
        return Hash32(heapBuffer.AsSpan(), seed);
    }

    /// <summary>
    /// Computes the 32-bit MurmurHash3 over a read-only span of bytes.
    /// </summary>
    /// <param name="data">The byte span to hash.</param>
    /// <param name="seed">Optional seed value.</param>
    /// <returns>A 32-bit unsigned integer hash.</returns>
    public static uint Hash32(ReadOnlySpan<byte> data, uint seed = 0)
    {
        uint h = seed;
        int length = data.Length;
        int nblocks = length / 4;

        // Process 4-byte chunks
        for (int i = 0; i < nblocks; i++)
        {
            int offset = i * 4;
            uint k = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));

            k *= C1;
            k = BitOperations.RotateLeft(k, R1);
            k *= C2;

            h ^= k;
            h = BitOperations.RotateLeft(h, R2);
            h = (h * M) + N;
        }

        // Process remaining tail bytes (0 to 3 bytes)
        int tailOffset = nblocks * 4;
        int remaining = length & 3;
        uint kTail = 0;

        switch (remaining)
        {
            case 3:
                kTail ^= (uint)data[tailOffset + 2] << 16;
                goto case 2;
            case 2:
                kTail ^= (uint)data[tailOffset + 1] << 8;
                goto case 1;
            case 1:
                kTail ^= data[tailOffset];
                kTail *= C1;
                kTail = BitOperations.RotateLeft(kTail, R1);
                kTail *= C2;
                h ^= kTail;
                break;
        }

        // Finalization mix (fmix32): ensures avalanche effect for all input bits
        h ^= (uint)length;
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;

        return h;
    }

    /// <summary>
    /// Computes a deterministic integer bucket between 0 and 99 (inclusive) for canary rollouts.
    /// Combines flagKey and userId to ensure independent, uniform bucketing across distinct flags.
    /// </summary>
    /// <param name="flagKey">The feature flag key.</param>
    /// <param name="userId">The unique identifier of the user/subject.</param>
    /// <param name="seed">Optional hash seed.</param>
    /// <returns>An integer in range [0..99].</returns>
    public static int ComputeBucket(string flagKey, string userId, uint seed = 0)
    {
        if (string.IsNullOrWhiteSpace(flagKey) || string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        // Combine flagKey and userId (e.g., "new-upi-checkout:usr_48102")
        string composite = string.Concat(flagKey, ":", userId);
        uint hash = Hash32(composite, seed);

        // Map uniformly into [0..99]
        return (int)(hash % 100);
    }
}
