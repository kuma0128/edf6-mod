using System.IO.Compression;
using System.Security.Cryptography;

namespace EDF6WeaponUnlocker.Core;

public static class WeaponStarCatalog
{
    private const int PackedBytesPerWeapon = 4;
    private const int PackedLength = WeaponCatalog.KnownWeaponCount * PackedBytesPerWeapon;
    private const string PackedSha256 = "4A018C13855D7C23A5E75A5900031D2763B5B953DE82CE12CAAA68D565780A9C";

    // Two 4-bit maximum-star values per byte, eight stat slots per weapon.
    // Derived from the 1,564-entry Steam EDF6 WEAPONTABLE. Zero denotes an
    // unused stat slot. Brotli keeps the verified table small without relying
    // on files from a mod-loader installation at runtime.
    private const string CompressedTableBase64 =
        "G28YMAaeelXtKcBc/953VzQORaM5jGrnTuEPV+gv9rA4KAUu//L3Dq0pwFzB//+lzc5LnSy9H4/wrSuE4WAUe5PcPyU/ofQJO5Ta/vwtQynNOCyLa0UrjEd49DoWY1FtobIDSKVM8jQDg4ADFS8oGCgoeHDgwI8tu4/cuUUVrqy+0QU23oBmCVfXyV53q3/EdAvAad3sx+UNaqKR4x1Gaa+UB8CeGoWWRSpG2u5t9JR2t7M/Frg/GtihvovVDLIxgIocVSmzh3dFA8c8E+tZ7tDN1NFURAnF9odrnYarTzi9qK26uqIKdIfnDbZMneJ6IJL20f/8K+VhqRXLWRTt0qSzoGP9C51UdKGytnQzUB/G6GGeKROVa/6eUumYEbC+e6s5vevdln+SRWd9RmtgKb+Tl+EFzrrV3CwAnERIUIxUf7IrRK/1Clt1pk9T2jndY6WnmWy4Qzjk2DdCjoYShh/jwIMAJV7au5YAExG0s/cRHj2h9Ns+6pW4wYUaW4zih0qUH7KOHlQCCr7KsyXXWgNBG507ra7HaYhn4cBF+TcYR+RLZ6lwgEvaxdDgDZyOAVQwJ0uWllF/BJH4/JZ6E9LvNTMuP5rX65DL1tz8z4K0v1Ozz87CYNCYpuy69esBdWu2JmhkJmYid2U4uUo30SBhLJ7mvqJ1P0qSsgdukoHlBIZhKgYrCT6GqmPc/W+p5B8Q9JaHLyHQ79w3270JYUs9pmJmVRZgBGYGjpxQYk3Ppd1eIu1FJ9Neiv5VicGY2nQHlGOoKXdvvwTWsn3t7b8lWWKiPsMRKicLUZsluUMR7yU6uidJPWTsklh8CGrgqCmfnU4/pgF1OYwIMATbwlSN8tivLk4XTTPmVnOrrS5f3bH6PhX7MpvzDLRNcQ+oFpeAcrX0cB0O/Nnv+yPWXSTTzDvNqyW3ybuX5uSM6BJ3ez5hl0aX/CJts5wBx5V/yUptHXxjY5/c/nn4Qr1asFKqGN4bxZhLZla46bQp/1YYiPwv5IFRrPi/GmXqdru5Swf7mpfvDYS9/jjcNbg0aBv9SX/Kh0g+VIZ4xlwG+mERd0MaK0PiYIDBbzNIgnCut/7B8K8Dj5u59OLjkiitdQsuJAxIzKR1hW7ySj4o/uXQPUTqSVGKeLJEG6fcas6fmThj2PiPQd1T270DNQf3vcJR0Prt6yg56KPLE7ebwUVPiYV9rEVpUpH0pKRM6cMgE6+5VKMKxZAAsPc23LtyWHHq516FPk/BGW19LSd2nWdYQGFWnTu3fQMoxS1Xfb2Bl8Z8hZsJJkrEAflAuQZdqF0pgwTHdOoruZja0TS99JBUanaX/wUA0rObvi6TiiX8mOow6eZblbidHhMNWLc5eeOs3BqzfniJnqdI+GWgy4lRZ5aAChwD9v4f8CCXj+ybwjAz/Ork+dGjXXvhKeOh4elI3PerrNujsh9cLVU2Wy3vufS0y7CfYh/qmnrYrlZczvmsksTNj9HHm3sQOlp+Xd6CfeeXrAHPjluiRP+CvwGejcgzW81J/wVXW6mB5Z9UIAEusBfwCse43/AmMemHgd3Sxi3gwOc3VtdH+/ANrN2PcgBf93IK+ULN2qCgPHe4WupJpolJzycg79jL2NF4D+xd7Ft/Rvhma2rZofrZcx14pMuewdV9bBK+1ZhyhYTvq352fRonl6NTPxk=";

    private static readonly byte[] PackedTable = LoadAndVerify();

    public static byte[] GetMaxStars(int weaponId)
    {
        ValidateWeaponId(weaponId);
        var result = new byte[8];
        CopyMaxStars(weaponId, result);
        return result;
    }

    public static void CopyMaxStars(int weaponId, Span<byte> destination)
    {
        ValidateWeaponId(weaponId);
        if (destination.Length < 8)
        {
            throw new ArgumentException("Destination must contain at least eight bytes.", nameof(destination));
        }

        var packedOffset = weaponId * PackedBytesPerWeapon;
        for (var statIndex = 0; statIndex < 8; statIndex++)
        {
            var packed = PackedTable[packedOffset + (statIndex / 2)];
            destination[statIndex] = (byte)(statIndex % 2 == 0
                ? packed & 0x0F
                : packed >> 4);
        }
    }

    public static bool IsAtMax(int weaponId, ReadOnlySpan<byte> actualStars)
    {
        if (actualStars.Length != 8)
        {
            return false;
        }

        Span<byte> maximums = stackalloc byte[8];
        CopyMaxStars(weaponId, maximums);
        for (var statIndex = 0; statIndex < maximums.Length; statIndex++)
        {
            var maximum = maximums[statIndex];
            if (maximum == 0)
            {
                if (actualStars[statIndex] != 0)
                {
                    return false;
                }
            }
            else if (actualStars[statIndex] < maximum)
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] LoadAndVerify()
    {
        var compressed = Convert.FromBase64String(CompressedTableBase64);
        var unpacked = new byte[PackedLength];
        using (var source = new MemoryStream(compressed, writable: false))
        using (var brotli = new BrotliStream(source, CompressionMode.Decompress))
        {
            brotli.ReadExactly(unpacked);
            if (brotli.ReadByte() != -1)
            {
                throw new InvalidDataException("Weapon maximum-star table has unexpected trailing data.");
            }
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(unpacked));
        if (!string.Equals(actualHash, PackedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Weapon maximum-star table failed its integrity check.");
        }

        return unpacked;
    }

    private static void ValidateWeaponId(int weaponId)
    {
        if (weaponId is < 0 or >= WeaponCatalog.KnownWeaponCount)
        {
            throw new ArgumentOutOfRangeException(nameof(weaponId));
        }
    }
}
