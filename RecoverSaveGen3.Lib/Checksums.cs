using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RecoverSaveGen3.Lib;

public static class Checksums
{
    public static ushort CheckSum32(ReadOnlySpan<byte> data, [ConstantExpected] uint initial = 0)
    {
        uint chk = initial;
        foreach (var u32 in MemoryMarshal.Cast<byte, uint>(data))
        {
            if (BitConverter.IsLittleEndian)
                chk += u32;
            else
                chk += BinaryPrimitives.ReverseEndianness(u32);
        }
        return (ushort)(chk + (chk >> 16));
    }
}