﻿using System;

namespace Cavern.Filters.Utilities {
    /// <summary>Q-factor conversion utilities.</summary>
    public static class QFactor {
        /// <summary>Sqrt(2)/2, the reference Q factor.</summary>
        public const double reference = .7071067811865475;

        /// <summary>Convert bandwidth to Q-factor.</summary>
        public static double FromBandwidth(double centerFreq, double startFreq, double endFreq) => centerFreq / (endFreq - startFreq);

        /// <summary>Convert bandwidth to Q-factor.</summary>
        public static double FromBandwidth(double centerFreq, double freqRange) => centerFreq / freqRange;

        /// <summary>Convert bandwidth to Q-factor.</summary>
        public static double FromBandwidth(double octaves) {
            double pow = Math.Pow(2, octaves);
            return Math.Sqrt(pow) / (pow - 1);
        }
    }
}