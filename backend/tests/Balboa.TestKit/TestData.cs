using Bogus;

namespace Balboa.TestKit;

/// <summary>Deterministic fake data (fixed seed) so failures reproduce.</summary>
public static class TestData
{
    private static readonly Faker _faker = new() { Random = new Randomizer(8675309) };

    /// <summary>Upper-case alphanumeric code of the given length.</summary>
    public static string Code(int length = 8) => _faker.Random.String2(length, "ABCDEFGHJKLMNPQRSTUVWXYZ23456789");

    public static string Name() => _faker.Commerce.ProductName();

    public static Guid NewGuid() => _faker.Random.Guid();
}
