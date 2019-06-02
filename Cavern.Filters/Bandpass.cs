﻿using System;

namespace Cavern.Filters {
    /// <summary>Simple first-order bandpass filter.</summary>
    public class Bandpass : BiquadFilter {
        /// <summary>Simple first-order bandpass filter.</summary>
        /// <param name="sampleRate">Audio sample rate</param>
        /// <param name="centerFreq">Center frequency (-3 dB point) of the filter</param>
        /// <param name="q">Q-factor of the filter</param>
        /// <param name="gain">Gain of the filter in decibels</param>
        public Bandpass(int sampleRate, float centerFreq, float q = .7071067811865475f, float gain = 0) : base(sampleRate, centerFreq, q, gain) { }

        /// <summary>Regenerate the transfer function.</summary>
        /// <param name="centerFreq">Center frequency (-3 dB point) of the filter</param>
        /// <param name="q">Q-factor of the filter</param>
        /// <param name="gain">Gain of the filter in decibels</param>
        public override void Reset(float centerFreq, float q = .7071067811865475f, float gain = 0) {
            base.Reset(centerFreq, q, gain);
            float w0 = (float)(Math.PI * 2 * centerFreq / sampleRate), cos = (float)Math.Cos(w0), alpha = (float)Math.Sin(w0) / (q + q),
                divisor = 1 / (1 + alpha); // 1 / a0
            b1 = 0;
            b2 = -alpha * divisor;
            b0 = -b2 * (float)Math.Pow(10, gain * .05f);
            a1 = -2 * cos * divisor;
            a2 = (1 - alpha) * divisor;
        }
    }
}