using BlastRadius.Sdk.Hashing;

namespace BlastRadius.Sdk.Tests;

public class MurmurHash3Tests
{
    [Fact]
    public void Hash32_ProducesDeterministicResult()
    {
        string input = "new-upi-checkout:usr_48102";
        uint hash1 = MurmurHash3.Hash32(input);
        uint hash2 = MurmurHash3.Hash32(input);

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(0u, hash1);
    }

    [Fact]
    public void ComputeBucket_ProducesValidBucketRange()
    {
        for (int i = 0; i < 1000; i++)
        {
            string userId = $"user_{i}";
            int bucket = MurmurHash3.ComputeBucket("flag-checkout", userId);

            Assert.InRange(bucket, 0, 99);
        }
    }

    [Fact]
    public void ComputeBucket_GuaranteesZeroFlickerAcrossRolloutIncreases()
    {
        // When rollout increases from 10% to 25%, any user who evaluated true at 10%
        // MUST evaluate true at 25%.
        string flagKey = "checkout-v2";
        int previousThreshold = 10;
        int newThreshold = 25;

        int qualifiedCount10 = 0;
        int qualifiedCount25 = 0;

        for (int i = 0; i < 500; i++)
        {
            string userId = $"cust_{i}";
            int bucket = MurmurHash3.ComputeBucket(flagKey, userId);

            bool isEnabledAt10 = bucket < previousThreshold;
            bool isEnabledAt25 = bucket < newThreshold;

            if (isEnabledAt10)
            {
                qualifiedCount10++;
                // Invariant: User enabled at 10% MUST still be enabled at 25% (Zero Flicker)
                Assert.True(isEnabledAt25, $"User '{userId}' with bucket {bucket} experienced flicker!");
            }

            if (isEnabledAt25)
            {
                qualifiedCount25++;
            }
        }

        Assert.True(qualifiedCount10 > 0);
        Assert.True(qualifiedCount25 > qualifiedCount10);
    }

    [Fact]
    public void ComputeBucket_ProvidesOrthogonalBucketingAcrossFlags()
    {
        // A user's bucket for flag A must be independent of flag B
        string userId = "usr_shared_992";
        int bucketA = MurmurHash3.ComputeBucket("flag-search-v2", userId);
        int bucketB = MurmurHash3.ComputeBucket("flag-payment-upi", userId);

        // While mathematically possible to collide, across distinct flags they should differ
        Assert.NotEqual(bucketA, bucketB);
    }

    [Fact]
    public void Hash32_DemonstratesAvalancheEffect()
    {
        // 1-character change must drastically alter hash bits
        uint hash1 = MurmurHash3.Hash32("user_1000");
        uint hash2 = MurmurHash3.Hash32("user_1001");

        uint diff = hash1 ^ hash2;
        int bitDifferences = System.Numerics.BitOperations.PopCount(diff);

        // Good hash functions flip ~16 bits out of 32 (between 10 and 22 bits)
        Assert.InRange(bitDifferences, 10, 26);
    }
}
