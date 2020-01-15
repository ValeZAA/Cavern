﻿using System;
using System.Collections.Generic;

using Cavern.Utilities;

namespace Cavern.QuickEQ {
    /// <summary>Contains an impulse response and data that can be calculated from it.</summary>
    public sealed class VerboseImpulseResponse {
        /// <summary>Raw impulse response on the complex plane.</summary>
        public readonly Complex[] ComplexResponse;
        /// <summary>Raw impulse response samples.</summary>
        public float[] Response {
            get {
                if (response != null)
                    return response;
                return response = Measurements.GetRealPart(ComplexResponse);
            }
        }
        /// <summary>Impulse polarity, true if positive.</summary>
        public bool Polarity => Response[Delay] >= 0;
        /// <summary>Response delay in samples relative to the reference it was calculated from.</summary>
        public int Delay {
            get {
                if (delay != -1)
                    return delay;
                float absPeak = float.NegativeInfinity;
                for (int pos = 0; pos < Response.Length; ++pos) {
                    float absHere = Math.Abs(response[pos]);
                    if (absPeak < absHere) {
                        absPeak = absHere;
                        delay = pos;
                    }
                }
                return delay;
            }
        }

        /// <summary>Response delay in samples relative to the reference it was calculated from.</summary>
        int delay = -1;
        /// <summary>Raw impulse response on the complex plane.</summary>
        float[] response = null;
        /// <summary>Peaks in the impulse response.</summary>
        /// <remarks>Calculated when <see cref="GetPeak(int)"/> is called.</remarks>
        Peak[] peaks = null;

        /// <summary>Create a verbose impulse response from a precalculated impulse response.</summary>
        public VerboseImpulseResponse(Complex[] impulseResponse) => ComplexResponse = impulseResponse;

        /// <summary>Create a verbose impulse response from a reference signal and a recorded response.</summary>
        public VerboseImpulseResponse(float[] reference, float[] response) : this(Measurements.IFFT(Measurements.GetFrequencyResponse(reference, response))) { }

        /// <summary>Representation of a peak in the impulse response.</summary>
        public struct Peak {
            /// <summary>Peak time offset in samples.</summary>
            public int Position;
            /// <summary>Gain at that position.</summary>
            public float Size;

            /// <summary>Representation of a peak in the impulse response.</summary>
            /// <param name="position">Peak time offset in samples.</param>
            /// <param name="size">Gain at that position.</param>
            public Peak(int position, float size) {
                Position = position;
                Size = size;
            }

            /// <summary>Returns if a peak is <see cref="Null"/>.</summary>
            public bool IsNull => Position == -1;

            /// <summary>Represents a nonexisting peak.</summary>
            public static Peak Null = new Peak(-1, 0);
        }

        /// <summary>Get the <paramref name="position"/>th peak in the impulse response.</summary>
        public Peak GetPeak(int position) {
            if (peaks == null) {
                List<Peak> peakList = new List<Peak>();
                float[] response = Response;
                float last = Math.Abs(response[0]), abs = Math.Abs(response[1]);
                for (int pos = 2; pos < response.Length; ++pos) {
                    float next = Math.Abs(response[pos]);
                    if (abs > last && abs > next)
                        peakList.Add(new Peak(pos - 1, abs));
                    last = abs;
                    abs = next;
                }
                peakList.Sort((a, b) => b.Size.CompareTo(a.Size));
                peaks = peakList.ToArray();
            }
            return position < peaks.Length ? peaks[position] : Peak.Null;
        }
    }
}