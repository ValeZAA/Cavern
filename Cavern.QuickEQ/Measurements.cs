﻿using System;
using System.Runtime.CompilerServices;

using Cavern.QuickEQ.Utilities;
using Cavern.Utilities;

namespace Cavern.QuickEQ {
    /// <summary>Tools for measuring frequency response.</summary>
    public static class Measurements {
        /// <summary>Actual FFT processing, somewhat in-place.</summary>
        static void ProcessFFT(Complex[] samples, FFTCache cache) {
            Complex[] source = samples, target = cache.temp;
            for (int depth = 0, maxDepth = QMath.Log2(samples.Length) - 1; depth <= maxDepth; ++depth) {
                int halfLength = 1 << depth,
                    step = 1 << (maxDepth - depth),
                    fullStep = step << 1;

                for (int offset = 0; offset < step; ++offset) {
                    for (int i = 0,
                            cachePos = 0,
                            targetPos = offset,
                            targetEnd = offset + halfLength * step,
                            even = offset,
                            odd = offset + step;
                        i < halfLength;
                        ++i,
                            cachePos += step,
                            targetPos += step,
                            targetEnd += step,
                            even += fullStep,
                            odd += fullStep) {
                        float oddReal = source[odd].Real * cache.cos[cachePos] - source[odd].Imaginary * cache.sin[cachePos],
                            oddImag = source[odd].Real * cache.sin[cachePos] + source[odd].Imaginary * cache.cos[cachePos];
                        target[targetPos].Real = source[even].Real + oddReal;
                        target[targetPos].Imaginary = source[even].Imaginary + oddImag;
                        target[targetEnd].Real = source[even].Real - oddReal;
                        target[targetEnd].Imaginary = source[even].Imaginary - oddImag;
                    }
                }

                (source, target) = (target, source);
            }

            if (target == samples)
                Array.Copy(source, samples, samples.Length);
        }

        /// <summary>Fourier-transform a signal in 1D. The result is the spectral power.</summary>
        static void ProcessFFT(float[] samples, FFTCache cache) {
            int halfLength = samples.Length / 2;
            if (samples.Length == 1)
                return;
            Complex[] even = cache.even, odd = cache.odd;
            for (int sample = 0, pair = 0; sample < halfLength; ++sample, pair += 2) {
                even[sample].Real = samples[pair];
                odd[sample].Real = samples[pair + 1];
            }
            ProcessFFT(even, cache);
            ProcessFFT(odd, cache);
            int stepMul = cache.cos.Length / halfLength;
            for (int i = 0; i < halfLength; ++i) {
                float oddReal = odd[i].Real * cache.cos[i * stepMul] - odd[i].Imaginary * cache.sin[i * stepMul],
                    oddImag = odd[i].Real * cache.sin[i * stepMul] + odd[i].Imaginary * cache.cos[i * stepMul];
                float real = even[i].Real + oddReal, imaginary = even[i].Imaginary + oddImag;
                samples[i] = (float)Math.Sqrt(real * real + imaginary * imaginary);
                real = even[i].Real - oddReal;
                imaginary = even[i].Imaginary - oddImag;
                samples[i + halfLength] = (float)Math.Sqrt(real * real + imaginary * imaginary);
            }
        }

        /// <summary>Fast Fourier transform a 2D signal.</summary>
        public static Complex[] FFT(Complex[] samples, FFTCache cache = null) {
            samples = (Complex[])samples.Clone();
            InPlaceFFT(samples, cache);
            return samples;
        }

        /// <summary>Fast Fourier transform a 1D signal.</summary>
        public static Complex[] FFT(float[] samples, FFTCache cache = null) {
            Complex[] complexSignal = new Complex[samples.Length];
            for (int sample = 0; sample < samples.Length; ++sample)
                complexSignal[sample].Real = samples[sample];
            InPlaceFFT(complexSignal, cache);
            return complexSignal;
        }

        /// <summary>Fast Fourier transform a 2D signal while keeping the source array allocation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InPlaceFFT(Complex[] samples, FFTCache cache = null) {
            if (CavernAmp.Available)
                CavernQuickEQAmp.InPlaceFFT(samples, cache);
            else {
                if (cache == null)
                    cache = new FFTCache(samples.Length);
                ProcessFFT(samples, cache);
            }
        }

        /// <summary>Spectrum of a signal's FFT.</summary>
        public static float[] FFT1D(float[] samples, FFTCache cache = null) {
            samples = (float[])samples.Clone();
            InPlaceFFT(samples, cache);
            return samples;
        }

        /// <summary>Spectrum of a signal's FFT while keeping the source array allocation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InPlaceFFT(float[] samples, FFTCache cache = null) {
            if (CavernAmp.Available)
                CavernQuickEQAmp.InPlaceFFT(samples, cache);
            else {
                if (cache == null)
                    cache = new FFTCache(samples.Length);
                ProcessFFT(samples, cache);
            }
        }

        /// <summary>Outputs IFFT(X) * N.</summary>
        static void ProcessIFFT(Complex[] samples, FFTCache cache) {
            Complex[] source = samples, target = cache.temp;
            for (int depth = 0, maxDepth = QMath.Log2(samples.Length) - 1; depth <= maxDepth; ++depth) {
                int halfLength = 1 << depth,
                    step = 1 << (maxDepth - depth),
                    fullStep = step << 1;

                for (int offset = 0; offset < step; ++offset) {
                    for (int i = 0,
                            cachePos = 0,
                            targetPos = offset,
                            targetEnd = offset + halfLength * step,
                            even = offset,
                            odd = offset + step;
                        i < halfLength;
                        ++i,
                            cachePos += step,
                            targetPos += step,
                            targetEnd += step,
                            even += fullStep,
                            odd += fullStep) {
                        float oddReal = source[odd].Real * cache.cos[cachePos] - source[odd].Imaginary * -cache.sin[cachePos],
                            oddImag = source[odd].Real * -cache.sin[cachePos] + source[odd].Imaginary * cache.cos[cachePos];
                        target[targetPos].Real = source[even].Real + oddReal;
                        target[targetPos].Imaginary = source[even].Imaginary + oddImag;
                        target[targetEnd].Real = source[even].Real - oddReal;
                        target[targetEnd].Imaginary = source[even].Imaginary - oddImag;
                    }
                }

                (source, target) = (target, source);
            }

            if (target == samples)
                Array.Copy(source, samples, samples.Length);
        }

        /// <summary>Inverse Fast Fourier Transform of a transformed signal.</summary>
        public static Complex[] IFFT(Complex[] samples, FFTCache cache = null) {
            samples = (Complex[])samples.Clone();
            if (CavernAmp.Available)
                CavernQuickEQAmp.InPlaceIFFT(samples, cache);
            else {
                if (cache == null)
                    cache = new FFTCache(samples.Length);
                InPlaceIFFT(samples, cache);
            }
            return samples;
        }

        /// <summary>Inverse Fast Fourier Transform of a transformed signal, while keeping the source array allocation.</summary>
        public static void InPlaceIFFT(Complex[] samples, FFTCache cache = null) {
            if (CavernAmp.Available) {
                CavernQuickEQAmp.InPlaceIFFT(samples, cache);
                return;
            }
            if (cache == null)
                cache = new FFTCache(samples.Length);
            ProcessIFFT(samples, cache);
            float multiplier = 1f / samples.Length;
            for (int i = 0; i < samples.Length; ++i) {
                samples[i].Real *= multiplier;
                samples[i].Imaginary *= multiplier;
            }
        }

        /// <summary>Minimizes the phase of a spectrum.</summary>
        /// <remarks>This function does not handle zeros in the spectrum. Make sure there is a threshold before using this function.</remarks>
        public static void MinimumPhaseSpectrum(Complex[] response, FFTCache cache = null) {
            bool customCache = false;
            if (cache == null) {
                cache = new FFTCache(response.Length);
                customCache = true;
            }
            int halfLength = response.Length / 2;
            for (int i = 0; i < response.Length; ++i) {
                response[i].Real = (float)Math.Log(response[i].Real);
                response[i].Imaginary = 0;
            }
            if (CavernAmp.Available)
                CavernQuickEQAmp.InPlaceIFFT(response, cache);
            else
                InPlaceIFFT(response, cache);
            for (int i = 1; i < halfLength; ++i) {
                response[i].Real += response[response.Length - i].Real;
                response[i].Imaginary -= response[response.Length - i].Imaginary;
                response[response.Length - i].Real = 0;
                response[response.Length - i].Imaginary = 0;
            }
            response[halfLength].Imaginary = -response[halfLength].Imaginary;
            if (CavernAmp.Available)
                CavernQuickEQAmp.InPlaceFFT(response, cache);
            else
                InPlaceFFT(response, cache);
            for (int i = 0; i < response.Length; ++i) {
                double exp = Math.Exp(response[i].Real);
                response[i].Real = (float)(exp * Math.Cos(response[i].Imaginary));
                response[i].Imaginary = (float)(exp * Math.Sin(response[i].Imaginary));
            }
            if (customCache)
                cache.Dispose();
        }

        /// <summary>Add gain to every frequency except a given band.</summary>
        public static void OffbandGain(Complex[] samples, double startFreq, double endFreq, double sampleRate, double dBgain) {
            int startPos = (int)(samples.Length * startFreq / sampleRate),
                endPos = (int)(samples.Length * endFreq / sampleRate);
            float gain = (float)Math.Pow(10, dBgain * .05);
            samples[0] *= gain;
            for (int i = 1; i < startPos; ++i) {
                samples[i] *= gain;
                samples[samples.Length - i] *= gain;
            }
            for (int i = endPos + 1, half = samples.Length / 2; i <= half; ++i) {
                samples[i] *= gain;
                samples[samples.Length - i] *= gain;
            }
        }

        /// <summary>Get the real part of a signal's FFT.</summary>
        public static float[] GetRealPart(Complex[] samples) {
            float[] output = new float[samples.Length];
            for (int sample = 0; sample < samples.Length; ++sample)
                output[sample] = samples[sample].Real;
            return output;
        }

        /// <summary>Get half of the real part of a signal's FFT.</summary>
        public static float[] GetRealPartHalf(Complex[] samples) {
            int half = samples.Length / 2;
            float[] output = new float[half];
            for (int sample = 0; sample < half; ++sample)
                output[sample] = samples[sample].Real;
            return output;
        }

        /// <summary>Get the imaginary part of a signal's FFT.</summary>
        public static float[] GetImaginaryPart(Complex[] samples) {
            float[] output = new float[samples.Length];
            for (int sample = 0; sample < samples.Length; ++sample)
                output[sample] = samples[sample].Imaginary;
            return output;
        }

        /// <summary>Get the gains of frequencies in a signal after FFT.</summary>
        public static float[] GetSpectrum(Complex[] samples) {
            int end = samples.Length / 2;
            float[] output = new float[end];
            for (int sample = 0; sample < end; ++sample)
                output[sample] = samples[sample].Magnitude;
            return output;
        }

        /// <summary>Get the gains of frequencies in a signal after FFT.</summary>
        public static float[] GetPhase(Complex[] samples) {
            int end = samples.Length / 2;
            float[] output = new float[end];
            for (int sample = 0; sample < end; ++sample)
                output[sample] = samples[sample].Phase;
            return output;
        }

        /// <summary>Get the frequency response using the original sweep signal's FFT as reference.</summary>
        public static Complex[] GetFrequencyResponse(Complex[] referenceFFT, Complex[] responseFFT) {
            for (int sample = 0; sample < responseFFT.Length; ++sample)
                responseFFT[sample].Divide(ref referenceFFT[sample]);
            return responseFFT;
        }

        /// <summary>Get the frequency response using the original sweep signal's FFT as reference.</summary>
        public static Complex[] GetFrequencyResponse(Complex[] referenceFFT, float[] response, FFTCache cache = null) =>
            GetFrequencyResponse(referenceFFT, FFT(response, cache));

        /// <summary>Get the frequency response using the original sweep signal as reference.</summary>
        public static Complex[] GetFrequencyResponse(float[] reference, float[] response, FFTCache cache = null) {
            if (cache == null)
                using (cache = new FFTCache(reference.Length))
                    return GetFrequencyResponse(FFT(reference, cache), FFT(response, cache));
            return GetFrequencyResponse(FFT(reference, cache), FFT(response, cache));
        }

        /// <summary>Get the complex impulse response using a precalculated frequency response.</summary>
        public static Complex[] GetImpulseResponse(Complex[] frequencyResponse, FFTCache cache = null) => IFFT(frequencyResponse, cache);

        /// <summary>Get the complex impulse response using the original sweep signal as a reference.</summary>
        public static Complex[] GetImpulseResponse(float[] reference, float[] response, FFTCache cache = null) =>
            IFFT(GetFrequencyResponse(reference, response), cache);
    }
}