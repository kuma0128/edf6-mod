namespace EDF6WeaponUnlocker.Core;

public static class Crc32C
{
    private const uint Polynomial = 0x82F63B78;

    /// <summary>Computes standard reflected CRC-32C (Castagnoli).</summary>
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ Polynomial : crc >> 1;
            }
        }

        return crc ^ uint.MaxValue;
    }

    /// <summary>
    /// EDF6 stores the bitwise inverse of the standard CRC-32C result.
    /// </summary>
    public static uint ComputeEdf6StoredValue(ReadOnlySpan<byte> data) => ~Compute(data);
}

