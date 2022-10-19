﻿using System;
using System.Runtime.CompilerServices;

namespace Cavern.Format.Common {
    /// <summary>
    /// Functions to read/write various data from/to cached bytestreams.
    /// </summary>
    static class ByteArrayExtensions {
        /// <summary>
        /// Read an ASCII string from an offset until the buffer ends or the character count is reached.
        /// </summary>
        public static string ReadCString(this byte[] source, int offset, int length) {
            char[] buffer = new char[length];
            int realLength = 0;
            for (int i = 0; i < length;) {
                if (source[offset + i] != 0) {
                    buffer[i] = (char)source[offset + i];
                    realLength = ++i;
                } else {
                    break;
                }
            }
            return new string(buffer, 0, realLength);
        }

        /// <summary>
        /// Read a 32-bit signed integer from the bytestream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(this byte[] source, int offset) => // If we had pointers that behaved as they should...
            source[offset] | (source[offset + 1] << 8) | (source[offset + 2] << 16) | (source[offset + 3] << 24);

        /// <summary>
        /// Read a 32-bit unsigned integer from the bytestream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(this byte[] source, int offset) => (uint)source.ReadInt32(offset);

        /// <summary>
        /// Write an ASCII string to an offset until the buffer ends or the character count is reached.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteCString(this byte[] target, string source, int offset, int length) {
            for (int i = 0, c = Math.Min(source.Length, length); i < c; i++) {
                target[offset + i] = (byte)source[i];
            }
        }

        /// <summary>
        /// Write a 16-bit unsigned integer to the bytestream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt16(this byte[] target, ushort value, int offset) {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
        }

        /// <summary>
        /// Write a 32-bit unsigned integer to the bytestream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32(this byte[] target, uint value, int offset) {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }
    }
}