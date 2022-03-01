﻿using System.IO;

using Cavern.Utilities;

namespace Cavern.Format.Common {
    /// <summary>
    /// Variable-size integer (VINT).
    /// </summary>
    internal static class VarInt {
        const int byteShiftMultiplier = 256;

        /// <summary>
        /// Reads the next VINT from a stream, does not cut the leading 1.
        /// </summary>
        public static long ReadTag(BinaryReader reader) {
            byte first = reader.ReadByte();
            int extraBytes = QMath.LeadingZeros(first);
            long value = first;
            for (int i = 0; i < extraBytes; ++i)
                value = (value * byteShiftMultiplier) | reader.ReadByte();
            return value;
        }

        /// <summary>
        /// Reads the next VINT from a stream, cuts the leading 1, reads the correct value.
        /// </summary>
        public static int ReadValue(BinaryReader reader) { // TODO: has to be long
            int value = (int)ReadTag(reader);
            return value - (1 << QMath.BitsAfterMSB(value));
        }

        /// <summary>
        /// Reads a fixed length VINT (the actual value field from a <see cref="KeyLengthValue"/>).
        /// </summary>
        public static long ReadValue(BinaryReader reader, int length) {
            long value = 0;
            for (int i = 0; i < length; ++i)
                value = (value * byteShiftMultiplier) | reader.ReadByte();
            return value;
        }
    }
}