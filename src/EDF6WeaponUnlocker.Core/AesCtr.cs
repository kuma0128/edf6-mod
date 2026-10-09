using System.Security.Cryptography;

namespace EDF6WeaponUnlocker.Core;

public static class AesCtr
{
    public static byte[] Transform(ReadOnlySpan<byte> input, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException("AES key must be 16, 24, or 32 bytes.", nameof(key));
        }

        if (iv.Length != 16)
        {
            throw new ArgumentException("AES CTR IV must be 16 bytes.", nameof(iv));
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key.ToArray();

        using var encryptor = aes.CreateEncryptor();
        var counter = iv.ToArray();
        var keyStream = new byte[16];
        var output = new byte[input.Length];

        for (var offset = 0; offset < input.Length; offset += 16)
        {
            var transformed = encryptor.TransformBlock(counter, 0, counter.Length, keyStream, 0);
            if (transformed != 16)
            {
                throw new CryptographicException("AES block transform returned an unexpected length.");
            }

            var blockLength = Math.Min(16, input.Length - offset);
            for (var index = 0; index < blockLength; index++)
            {
                output[offset + index] = (byte)(input[offset + index] ^ keyStream[index]);
            }

            IncrementBigEndian(counter);
        }

        CryptographicOperations.ZeroMemory(counter);
        CryptographicOperations.ZeroMemory(keyStream);
        return output;
    }

    private static void IncrementBigEndian(Span<byte> counter)
    {
        for (var index = counter.Length - 1; index >= 0; index--)
        {
            counter[index]++;
            if (counter[index] != 0)
            {
                return;
            }
        }
    }
}

