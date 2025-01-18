using static System.Buffers.Binary.BinaryPrimitives;
using System.Diagnostics.CodeAnalysis;

namespace RecoverSaveGen3.Lib;

public static class Fixer3
{
    private const int SIZE_G3RAW = 0x20000;
    private const int SIZE_G3RAWHALF = 0x10000;

    private const int SIZE_SECTOR = 0x1000;
    private const int SIZE_SECTOR_USED = 0xF80;
    private const uint Signature = 0x0801_2025;

    private const int FooterLeeway = 0x40; // Allow for RTC footers, usually only 0x10/0x20 but whatever.

    public static bool IsSizeWorthLookingAt(long size) => size <= (SIZE_G3RAW + FooterLeeway);

    public static bool TryFixSaveFile(ReadOnlyMemory<byte> file, [NotNullWhen(true)] out byte[]? result, out Fix3Result message)
    {
        result = null;
        if (file.Length is (>= SIZE_G3RAWHALF and <= SIZE_G3RAWHALF + FooterLeeway))
            return TryInflateFromHalf(file, out result, out message);

        // Allow for a little extra size for RTC footers, otherwise reject
        if (file.Length < SIZE_G3RAW)
        {
            message = Fix3Result.TooSmall;
            return false;
        }
        if (file.Length > SIZE_G3RAW + FooterLeeway)
        {
            message = Fix3Result.TooBig;
            return false;
        }

        file = file[..SIZE_G3RAW];
        return TryInflateFromFull(file, out result, out message);
    }

    private static bool TryInflateFromHalf(ReadOnlyMemory<byte> file,
        [NotNullWhen(true)] out byte[]? result, out Fix3Result message)
    {
        result = new byte[SIZE_G3RAW];
        var span = result.AsSpan();
        span.Fill(0xFF);
        file.Span.CopyTo(span);

        var wasSuccess = TryInflateFromFull(result, out result, out message);
        if (wasSuccess)
            message |= Fix3Result.Inflated;
        return wasSuccess;
    }

    private static bool TryInflateFromFull(ReadOnlyMemory<byte> file,
        [NotNullWhen(true)] out byte[]? result, out Fix3Result message)
    {
        Span<BlockState> present = stackalloc BlockState[14];
        Span<ushort> savedCounts = stackalloc ushort[14];
        Span<ushort> checksums = stackalloc ushort[14];
        var blocks = new ReadOnlyMemory<byte>[14];

        for (int i = 0; i < file.Length; i += 0x1000)
        {
            var chunk = file.Slice(i, SIZE_SECTOR);
            var blockID = ReadUInt16LittleEndian(chunk.Span[0xFF4..]);
            if (blockID >= 14)
                continue;
            var signature = ReadUInt32LittleEndian(chunk.Span[0xFF8..]);
            if (signature != Signature)
                continue;

            var checksum = ReadUInt16LittleEndian(chunk.Span[0xFF6..]);
            var dataRegion = chunk[..SIZE_SECTOR_USED];
            var actualChecksum = Checksums.CheckSum32(dataRegion.Span);
            var chkValid = checksum == actualChecksum;
            var current = chkValid ? BlockState.Valid : BlockState.BadChecksum;

            var counter = ReadUInt16LittleEndian(chunk.Span[0xFFC..]);
            var previous = present[blockID];
            if (previous != BlockState.Missing)
            {
                if (!PickBlock(savedCounts[blockID], counter, previous, current))
                    continue;
            }
            present[blockID] = current;
            savedCounts[blockID] = counter;
            blocks[blockID] = chunk;
            checksums[blockID] = actualChecksum;
        }

        ushort maxCtr = 0;
        foreach (var ctr in savedCounts)
            maxCtr = Math.Max(ctr, maxCtr);

        message = Fix3Result.Recovered;
        result = null;
        if (present.Contains(BlockState.Missing))
        {
            bool allCritical = !present[..4].Contains(BlockState.Missing);
            if (!allCritical)
            {
                message = Fix3Result.MissingCriticalBlocks;
                return false;
            }

            // Spoof the missing blocks
            for (ushort i = 0; i < 14; i++)
            {
                if (present[i] != BlockState.Missing)
                    continue;
                blocks[i] = GetFakeBlock(i);
                present[i] = BlockState.Valid;
            }
            message |= Fix3Result.MissingBoxBlocks;
        }

        savedCounts.Fill(maxCtr);
        result = new byte[SIZE_G3RAW];
        for (int i = 0; i < 14; i++)
        {
            // Copy the block to the result.
            var dest = result.AsSpan(i * SIZE_SECTOR, SIZE_SECTOR);
            blocks[i].Span.CopyTo(dest);

            // Update the footer of each block with what it *should* be.
            WriteUInt16LittleEndian(dest[0xFF6..], checksums[i]);
            WriteUInt32LittleEndian(dest[0xFF8..], Signature);
            WriteUInt16LittleEndian(dest[0xFFC..], savedCounts[i]);

            // Mirror to the other side of the save as well.
            var other = result.AsSpan((i + 14) * SIZE_SECTOR, SIZE_SECTOR);
            dest.CopyTo(other);
        }

        message |= RetrieveExtraDataBlocks(file.Span, result);
        return true;
    }

    private static byte[] GetFakeBlock(ushort index)
    {
        var data = new byte[SIZE_SECTOR];
        var span = data.AsSpan();
        WriteUInt16LittleEndian(span[0xFF4..], index);
        // Checksum, etc. will be updated later.
        return data;
    }

    private static bool PickBlock(ushort ctrPrev, ushort ctrCurrent, BlockState prev, BlockState current)
    {
        if (current is BlockState.Valid && prev is not BlockState.Valid)
            return true;

        var compare = CounterCompare.CompareCounters(ctrPrev, ctrCurrent);
        return compare == CounterCompare.Second;
    }

    private static Fix3Result RetrieveExtraDataBlocks(ReadOnlySpan<byte> file, Span<byte> result)
    {
        const int startBlock = 0x1C;
        int validSectors = 0;
        for (int i = startBlock; i < 0x20; i++)
        {
            var data = file.Slice(i * SIZE_SECTOR, SIZE_SECTOR);
            if (!IsSectorUninitialized(data))
            {
                // Verify checksum first
                var checksum = ReadUInt16LittleEndian(data[0xFF4..]);
                var dataRegion = data[..SIZE_SECTOR_USED];
                var dataChecksum = Checksums.CheckSum32(dataRegion);
                if (checksum != dataChecksum)
                    continue;
            }

            validSectors++;
            // Copy the sector to the result
            data.CopyTo(result[(i * SIZE_SECTOR)..]);
        }

        return validSectors == 4 ? Fix3Result.None : Fix3Result.MissingExtraBlocks;
    }

    private static bool IsSectorUninitialized(ReadOnlySpan<byte> sector) => sector.IndexOfAnyExcept<byte>(0, 0xFF) == -1;

    private static bool Contains<T>(this Span<T> arr, T value) where T : Enum
    {
        foreach (var item in arr)
        {
            if (item.Equals(value))
                return true;
        }
        return false;
    }
}