﻿using System;
using System.Runtime.CompilerServices;

namespace Cavern.Filters {
    /// <summary>Simple convolution window filter.</summary>
    public class Convolver : Filter {
        /// <summary>Additional impulse delay in samples.</summary>
        public int Delay {
            get => delay;
            set => future = new float[impulse.Length + (delay = value)];
        }

        /// <summary>Impulse response to convolve with.</summary>
        public float[] Impulse {
            get => impulse;
            set {
                if (future.Length != (impulse = value).Length)
                    future = new float[value.Length + delay];
            }
        }

        /// <summary>Additional impulse delay in samples.</summary>
        protected int delay;
        /// <summary>Impulse response to convolve with.</summary>
        protected float[] impulse;
        /// <summary>Samples to be copied to the beginning of the next output.</summary>
        protected float[] future;

        /// <summary>Construct a convolver for a target impulse response.</summary>
        public Convolver(float[] impulse, int delay) {
            this.impulse = new float[impulse.Length];
            Buffer.BlockCopy(impulse, 0, this.impulse, 0, impulse.Length * sizeof(float));
            Delay = delay;
        }

        /// <summary>Output the result and handle the future.</summary>
        protected void Finalize(float[] samples, float[] convolved) {
            int delayedImpulse = impulse.Length + delay;
            if (samples.Length > delayedImpulse) {
                // Drain cache
                Buffer.BlockCopy(convolved, 0, samples, 0, samples.Length * sizeof(float));
                for (int sample = 0; sample < delayedImpulse; ++sample)
                    samples[sample] += future[sample];
                // Fill cache
                for (int sample = 0; sample < delayedImpulse; ++sample)
                    future[sample] = convolved[sample + samples.Length];
            } else {
                // Drain cache
                for (int sample = 0; sample < samples.Length; ++sample)
                    samples[sample] = convolved[sample] + future[sample];
                // Move cache
                int futureEnd = delayedImpulse - samples.Length;
                for (int sample = 0; sample < futureEnd; ++sample)
                    future[sample] = future[sample + samples.Length];
                Array.Clear(future, futureEnd, samples.Length);
                // Merge cache
                for (int sample = 0; sample < delayedImpulse; ++sample)
                    future[sample] += convolved[sample + samples.Length];
            }
        }

        /// <summary>Perform a convolution.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float[] Convolve(float[] a, float[] b) {
            float[] convolved = new float[a.Length + b.Length];
            for (int i = 0; i < a.Length; ++i)
                for (int j = 0; j < b.Length; ++j)
                    convolved[i + j] += a[i] * b[j];
            return convolved;
        }

        /// <summary>Perform a convolution with a delay.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float[] Convolve(float[] a, float[] b, int delay) {
            float[] convolved = new float[a.Length + b.Length + delay];
            for (int i = 0; i < a.Length; ++i)
                for (int j = 0; j < b.Length; ++j)
                    convolved[i + j + delay] += a[i] * b[j];
            return convolved;
        }

        /// <summary>Apply convolution on an array of samples. One filter should be applied to only one continuous stream of samples.</summary>
        public override void Process(float[] samples) {
            float[] convolved;
            if (delay == 0)
                convolved = Convolve(samples, impulse);
            else
                convolved = Convolve(samples, impulse, delay);
            Finalize(samples, convolved);
        }
    }
}