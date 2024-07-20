﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using Cavern.Filters.Interfaces;
using Cavern.Utilities;

namespace Cavern.Filters {
    /// <summary>
    /// Delays the audio.
    /// </summary>
    public class Delay : Filter, IEqualizerAPOFilter, ISampleRateDependentFilter, ILocalizableToString, IXmlSerializable {
        /// <summary>
        /// If the filter was set up with a time delay, this is the sample rate used to calculate the delay in samples.
        /// </summary>
        [IgnoreDataMember]
        public int SampleRate { get; set; }

        /// <summary>
        /// Delay in samples.
        /// </summary>
        [DisplayName("Delay (samples)")]
        public int DelaySamples {
            get => cache[0].Length;
            set {
                if (cache[0].Length != value) {
                    RecreateCaches(value);
                    delayMs = double.NaN;
                }
            }
        }

        /// <summary>
        /// Delay in milliseconds.
        /// </summary>
        [DisplayName("Delay (ms)")]
        public double DelayMs {
            get {
                if (!double.IsNaN(delayMs)) {
                    return delayMs;
                }
                if (SampleRate == 0) {
                    throw new SampleRateNotSetException();
                }
                return DelaySamples / (double)SampleRate * 1000;
            }

            set {
                if (SampleRate == 0) {
                    throw new SampleRateNotSetException();
                }
                DelaySamples = (int)Math.Round(value * SampleRate * .001);
                delayMs = value;
            }
        }

        /// <summary>
        /// When the filter was created with a precise delay that is not a round value in samples, display this instead.
        /// </summary>
        double delayMs;

        /// <summary>
        /// Cached samples for the next block. Alternates between two arrays to prevent memory allocation.
        /// </summary>
        readonly float[][] cache = new float[2][];

        /// <summary>
        /// The used cache (0 or 1).
        /// </summary>
        int usedCache;

        void RecreateCaches(int size) {
            cache[0] = new float[size];
            cache[1] = new float[size];
        }

        /// <summary>
        /// Create a delay for a given length in samples.
        /// </summary>
        public Delay(int samples) {
            delayMs = double.NaN;
            RecreateCaches(samples);
        }

        /// <summary>
        /// Create a delay for a given length in seconds.
        /// </summary>
        public Delay(double time, int sampleRate) {
            SampleRate = sampleRate;
            delayMs = time;
            RecreateCaches((int)(time * sampleRate * .001 + .5));
        }

        /// <summary>
        /// Parse a Delay line of Equalizer APO to a Cavern <see cref="Delay"/> filter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Delay FromEqualizerAPO(string line, int sampleRate) =>
            FromEqualizerAPO(line.Split(' ', StringSplitOptions.RemoveEmptyEntries), sampleRate);

        /// <summary>
        /// Parse a Delay line of Equalizer APO which was split at spaces to a Cavern <see cref="Delay"/> filter.
        /// </summary>
        public static Delay FromEqualizerAPO(string[] splitLine, int sampleRate) {
            if (splitLine.Length < 3 || !QMath.TryParseDouble(splitLine[1], out double delay)) {
                throw new FormatException(nameof(splitLine));
            }
            return splitLine[2].ToLowerInvariant() switch {
                "ms" => new Delay(delay, sampleRate),
                "samples" => new Delay((int)delay) {
                    SampleRate = sampleRate
                },
                _ => throw new ArgumentOutOfRangeException(splitLine[0]),
            };
        }

        /// <inheritdoc/>
        public override void Process(float[] samples) {
            int delaySamples = cache[0].Length;
            float[] cacheToFill = cache[1 - usedCache], cacheToDrain = cache[usedCache];
            // Sample array can hold the cache
            if (delaySamples <= samples.Length) {
                // Fill cache
                Array.Copy(samples, samples.Length - delaySamples, cacheToFill, 0, delaySamples);
                // Move self
                for (int sample = samples.Length - 1; sample >= delaySamples; --sample) {
                    samples[sample] = samples[sample - delaySamples];
                }
                // Drain cache
                Array.Copy(cacheToDrain, samples, delaySamples);
                usedCache = 1 - usedCache; // Switch caches
            }
            // Cache can hold the sample array
            else {
                // Fill cache
                Array.Copy(samples, cacheToFill, samples.Length);
                // Drain cache
                Array.Copy(cacheToDrain, samples, samples.Length);
                // Move cache
                Array.Copy(cacheToDrain, samples.Length, cacheToDrain, 0, delaySamples - samples.Length);
                // Combine cache
                Array.Copy(cacheToFill, 0, cacheToDrain, delaySamples - samples.Length, samples.Length);
            }
        }

        /// <inheritdoc/>
        public override object Clone() => double.IsNaN(delayMs) ? new Delay(DelaySamples) : new Delay(DelayMs, SampleRate);

        /// <inheritdoc/>
        public XmlSchema GetSchema() => null;

        /// <inheritdoc/>
        public void ReadXml(XmlReader reader) {
            while (reader.MoveToNextAttribute()) {
                switch (reader.Name) {
                    case nameof(SampleRate):
                        SampleRate = int.Parse(reader.Value);
                        break;
                    case nameof(DelaySamples):
                        DelaySamples = int.Parse(reader.Value);
                        break;
                }
            }
        }

        /// <inheritdoc/>
        public void WriteXml(XmlWriter writer) {
            writer.WriteStartElement(nameof(Delay));
            writer.WriteAttributeString(nameof(SampleRate), SampleRate.ToString());
            writer.WriteAttributeString(nameof(DelaySamples), DelaySamples.ToString());
            writer.WriteEndElement();
        }

        /// <inheritdoc/>
        public override string ToString() {
            if (double.IsNaN(delayMs)) {
                return $"Delay: {DelaySamples} samples";
            } else {
                string delay = DelayMs.ToString(CultureInfo.InvariantCulture);
                return $"Delay: {delay} ms";
            }
        }

        /// <inheritdoc/>
        public string ToString(CultureInfo culture) => culture.Name switch {
            "hu-HU" => double.IsNaN(delayMs) ? $"Késleltetés: {DelaySamples} minta" : $"Késleltetés: {DelayMs} ms",
            _ => ToString()
        };

        /// <inheritdoc/>
        public void ExportToEqualizerAPO(List<string> wipConfig) => wipConfig.Add(ToString());
    }
}