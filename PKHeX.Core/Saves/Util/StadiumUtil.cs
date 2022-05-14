﻿using System;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Core
{
    /// <summary>
    /// Logic pertaining to Pokémon Stadium Save Files.
    /// </summary>
    public static class StadiumUtil
    {
        /// <summary>
        /// Checks if the <see cref="magic"/> value is present either with or without byte-swapping.
        /// </summary>
        public static bool IsMagicPresentEither(ReadOnlySpan<byte> data, int size, uint magic)
        {
            if (IsMagicPresent(data, size, magic))
                return true;

            if (IsMagicPresentSwap(data, size, magic))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if the <see cref="magic"/> value is present without byte-swapping.
        /// </summary>
        public static bool IsMagicPresent(ReadOnlySpan<byte> data, int size, uint magic)
        {
            // Check footers of first few teams to see if the magic value is there.
            for (int i = 0; i < 10; i++)
            {
                var footer = data[(size - 6 + (i * size))..];
                if (ReadUInt32LittleEndian(footer) != magic)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if the <see cref="magic"/> value is present either with byte-swapping.
        /// </summary>
        public static bool IsMagicPresentSwap(ReadOnlySpan<byte> data, int size, uint magic)
        {
            // Check footers of first few teams to see if the magic value is there.
            var right = ReverseEndianness((ushort)(magic >> 16));
            var left = ReverseEndianness((ushort)magic);

            for (int i = 0; i < 10; i++)
            {
                var offset = size - 6 + (i * size);

                if (ReadUInt16LittleEndian(data[(offset + 4)..]) != right) // EK
                    return false;
                if (ReadUInt16LittleEndian(data[(offset - 2)..]) != left) // OP
                    return false;
            }
            return true;
        }

        public static bool IsMagicPresentAbsolute(ReadOnlySpan<byte> data, int offset, uint magic)
        {
            var actual = ReadUInt32LittleEndian(data[offset..]);
            if (actual == magic) // POKE
                return true;

            var right = ReverseEndianness((ushort)(magic >> 16));
            if (ReadUInt16LittleEndian(data[(offset + 4)..]) != right) // EK
                return false;
            var left = ReverseEndianness((ushort)magic);
            if (ReadUInt16LittleEndian(data[(offset - 2)..]) != left) // OP
                return false;

            return true;
        }
    }
}
