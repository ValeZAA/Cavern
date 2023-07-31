﻿using Cavern.Utilities;

namespace Test.Cavern {
    /// <summary>
    /// Tests the <see cref="Measurements"/> class.
    /// </summary>
    [TestClass]
    public class Measurements_Tests {
        /// <summary>
        /// Tests if an FFT produces the expected result and the IFFT inverts it correctly for a Dirac-delta.
        /// </summary>
        [TestMethod, Timeout(1000)]
        public void FFT_IFFT_Dirac() {
            Complex[] signal = new Complex[16];
            signal[0] = new Complex(1, 0);

            using FFTCache cache = new FFTCache(signal.Length);
            signal.InPlaceFFT(cache);
            for (int i = 0; i < signal.Length; i++) {
                Assert.IsTrue(signal[i].Real == 1);
                Assert.IsTrue(signal[i].Imaginary == 0);
            }

            signal.InPlaceIFFT(cache);
            Assert.IsTrue(signal[0].Real == 1);
            Assert.IsTrue(signal[0].Imaginary == 0);
            for (int i = 1; i < signal.Length; i++) {
                Assert.IsTrue(signal[i].Real == 0);
                Assert.IsTrue(signal[i].Imaginary == 0);
            }
        }

        /// <summary>
        /// Tests if an FFT produces the expected result and the IFFT inverts it correctly for a sine wave.
        /// </summary>
        [TestMethod, Timeout(1000)]
        public void FFT_IFFT_Sine() {
            float[] signal = new float[32];
            for (int i = 0; i < signal.Length; i++) {
                signal[i] = MathF.Cos(2 * MathF.PI * i / signal.Length);
            }

            using FFTCache cache = new FFTCache(signal.Length);
            Complex[] fft = signal.FFT(cache);
            for (int i = 0; i < fft.Length; i++) {
                Assert.IsTrue(i == 1 || i == 31 ? fft[i].Real == (signal.Length >> 1) : Math.Abs(fft[i].Real) < .000002f);
                Assert.IsTrue(Math.Abs(fft[i].Imaginary) < .000004f);
            }

            fft.InPlaceIFFT(cache);
            for (int i = 0; i < fft.Length; i++) {
                Assert.IsTrue(Math.Abs(fft[i].Real - signal[i]) < .000002f);
                Assert.IsTrue(Math.Abs(fft[i].Imaginary) < .000002f);
            }
        }

        /// <summary>
        /// Tests an FFT with a length of 4, which is calling a hardcoded FFT variant.
        /// </summary>
        [TestMethod, Timeout(1000)]
        public void FFT4() {
            float[] signal = new float[] { 1, .5f, .25f, 0 };
            Complex[] fft = signal.FFT();
            Complex[] expected = new Complex[] { new Complex(1.75f), new Complex(.75f, -.5f), new Complex(.75f), new Complex(.75f, .5f) };
            CollectionAssert.AreEqual(expected, fft);
        }
    }
}