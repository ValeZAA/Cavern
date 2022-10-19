﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Cavern.Format.Common;

namespace Cavern.Format.Transcoders {
    /// <summary>
    /// Transcodes Dolby audio Metadata chunks.
    /// </summary>
    public class DolbyMetadata {
        /// <summary>
        /// Version of this metadata. The bytes are major, minor, revision, and build version numbers.
        /// </summary>
        public uint Version { get; }

        /// <summary>
        /// Software used for creating this DBMD, 2 ASCII strings, 32 characters max.
        /// </summary>
        public string[] CreationInfo { get; } = new string[2];

        /// <summary>
        /// Unknown metadata at the beginning of the <see cref="objectMetadata"/> segment.
        /// </summary>
        public uint ObjectMetadataPreamble { get; }

        /// <summary>
        /// Number of audio objects present in the audio stream.
        /// </summary>
        public byte ObjectCount { get; }

        /// <summary>
        /// Reads a Dolby audio Metadata chunk from a stream.
        /// </summary>
        public DolbyMetadata(Stream reader, long length, bool checkChecksums = false) {
            Version = reader.ReadUInt32(); // each byte is one dotted value -> to/from string
            long endPosition = reader.Position + length;

            byte segmentID;
            byte[] segment = new byte[0];
            while ((segmentID = (byte)reader.ReadByte()) != 0) {
                ushort segmentLength = reader.ReadUInt16();
                if (segment.Length < segmentLength) {
                    segment = new byte[segmentLength];
                }
                reader.Read(segment, 0, segmentLength);

                if (checkChecksums) {
                    if (reader.ReadByte() != CalculateChecksum(segment, segmentLength)) {
                        throw new CorruptionException("dbmd segment " + segmentID);
                    }
                } else {
                    ++reader.Position;
                }

                switch (segmentID) {
                    case DolbyAtmosMetadata:
                        CreationInfo[0] = segment.ReadCString(0, creationInfoFieldSize);
                        CreationInfo[1] = segment.ReadCString(creationInfoFieldSize, creationInfoFieldSize);
                        // Find out the following bytes if needed
                        break;
                    case objectMetadata:
                        ObjectMetadataPreamble = segment.ReadUInt32(0);
                        ObjectCount = segment[4];
                        // Find out the following bytes if needed
                        break;
                    default:
                        break;
                }
            }
            reader.Position = endPosition;
        }

        /// <summary>
        /// Creates a Dolby Metadata that can be written to a bytestream.
        /// </summary>
        public DolbyMetadata(byte objectCount) {
            Version = version;
            CreationInfo[0] = defaultCreationInfo;
            CreationInfo[1] = Listener.Info[..(Listener.Info.IndexOf('(') - 1)];
            ObjectMetadataPreamble = objectMetadataPreamble;
            ObjectCount = objectCount;
        }

        /// <summary>
        /// Gets the checksum value for a metadata segment.
        /// </summary>
        static byte CalculateChecksum(byte[] segment, ushort segmentLength) {
            int checksum = segmentLength;
            for (int i = 0; i < segmentLength; i++) {
                checksum += segment[i];
            }
            return (byte)(~checksum + 1);
        }

        /// <summary>
        /// Create the output bytestream.
        /// </summary>
        public byte[] Serialize() {
            Dictionary<byte, byte[]> segments = new Dictionary<byte, byte[]>();
            if (CreationInfo[0] != null) {
                segments.Add(DolbyAtmosMetadata, CreateDolbyAtmosMetadata());
            }
            if (ObjectCount != 0) {
                segments.Add(objectMetadata, CreateObjectMetadata());
            }

            byte[] result = new byte[6 + 4 * segments.Count + segments.Sum(x => x.Value.Length)];
            result.WriteUInt32(Version, 0);
            int offset = 4;
            foreach (KeyValuePair<byte, byte[]> segment in segments) {
                result[offset++] = segment.Key;
                result.WriteUInt16((ushort)segment.Value.Length, offset);
                Array.Copy(segment.Value, 0, result, offset += 2, segment.Value.Length);
                offset += segment.Value.Length;
                result[offset++] = CalculateChecksum(segment.Value, (ushort)segment.Value.Length);
            }
            return result;
        }

        /// <summary>
        /// Create the bytestream of a <see cref="DolbyAtmosMetadata"/> block.
        /// </summary>
        byte[] CreateDolbyAtmosMetadata() {
            byte[] result = new byte[DolbyAtmosMetadataLength];
            result.WriteCString(CreationInfo[0], 0, creationInfoFieldSize);
            result.WriteCString(CreationInfo[1], creationInfoFieldSize, creationInfoFieldSize);
            return result;
        }

        /// <summary>
        /// Create the bytestream of an <see cref="objectMetadata"/> block.
        /// </summary>
        byte[] CreateObjectMetadata() {
            byte[] result = new byte[5 + objectMetadataTrashLength + ObjectCount];
            result.WriteUInt32(ObjectMetadataPreamble, 0);
            result[4] = ObjectCount;
            for (int i = result.Length - ObjectCount; i < result.Length; i++) {
                result[i] = defaultObjectMetadata;
            }
            return result;
        }

        /// <summary>
        /// Version used for writing DBMDs.
        /// </summary>
        const uint version = 0x01000006;

        /// <summary>
        /// Dolby Atmos metadata segment identifier.
        /// </summary>
        const byte DolbyAtmosMetadata = 9;

        /// <summary>
        /// Default length of a <see cref="DolbyAtmosMetadata"/> segment.
        /// </summary>
        const ushort DolbyAtmosMetadataLength = 248;

        /// <summary>
        /// Creation info used for writing DBMDs.
        /// </summary>
        const string defaultCreationInfo = "Created with Cavern";

        /// <summary>
        /// Maximum number of characters for each creation info entry.
        /// </summary>
        const byte creationInfoFieldSize = 32;

        /// <summary>
        /// Object-related metadata.
        /// </summary>
        const byte objectMetadata = 10;

        /// <summary>
        /// Unknown values that were the same for every checked <see cref="objectMetadata"/> segment.
        /// </summary>
        const uint objectMetadataPreamble = 0xf8726fbd;

        /// <summary>
        /// Fixed length of skipped fields in <see cref="objectMetadata"/>.
        /// </summary>
        const ushort objectMetadataTrashLength = 262;

        /// <summary>
        /// Default value of a single object's metadata.
        /// </summary>
        const byte defaultObjectMetadata = 0x84;
    }
}