﻿using System;
using System.Numerics;

namespace Cavern.Utilities {
    partial class QMath {
        /// <summary>
        /// Multiply the values of both arrays together and add these multiples together.
        /// </summary>
        public static unsafe float MultiplyAndAdd(float* lhs, float* rhs, int count) {
            int i = 0;
            float sum = 0;
            for (int vc = Vector<float>.Count, c = count - vc; i < c; i += vc) {
                sum += Vector.Dot(new Vector<float>(new Span<float>(lhs + i, vc)) *
                    new Vector<float>(new Span<float>(rhs + i, vc)), Vector<float>.One);
            }
            for (; i < count; i++) {
                sum += lhs[i] * rhs[i];
            }
            return sum;
        }

        /// <summary>
        /// Multiply the values of both arrays together to the corresponding element of the <paramref name="target"/>.
        /// </summary>
        public static unsafe void MultiplyAndAdd(float* lhs, float* rhs, float[] target, int count) {
            int i = 0;
            for (int vc = Vector<float>.Count, c = count - vc; i < c; i += vc) {
                (new Vector<float>(target, i) + new Vector<float>(new Span<float>(lhs + i, vc)) *
                    new Vector<float>(new Span<float>(rhs + i, vc))).CopyTo(target, i);
            }
            for (; i < count; i++) {
                target[i] += lhs[i] * rhs[i];
            }
        }

        /// <summary>
        /// Multiply the values of an array with a constant to the corresponding element of the <paramref name="target"/>.
        /// </summary>
        public static unsafe void MultiplyAndAdd(float* lhs, float rhs, float[] target, int count) {
            int i = 0;
            for (int vc = Vector<float>.Count, c = count - vc; i < c; i += vc) {
                (new Vector<float>(target, i) + new Vector<float>(new Span<float>(lhs + i, vc)) * rhs).CopyTo(target, i);
            }
            for (; i < count; i++) {
                target[i] += lhs[i] * rhs;
            }
        }

        /// <summary>
        /// Do <see cref="MultiplyAndAdd(float*, float*, float[], int)"/> simultaneously for two different pairs of arrays.
        /// </summary>
        public static unsafe void MultiplyAndAdd(float* lhs1, float* rhs1, float* lhs2, float* rhs2, float[] target, int count) {
            int i = 0;
            for (int vc = Vector<float>.Count, c = count - vc; i < c; i += vc) {
                (new Vector<float>(target, i) +
                    new Vector<float>(new Span<float>(lhs1 + i, vc)) * new Vector<float>(new Span<float>(rhs1 + i, vc)) +
                    new Vector<float>(new Span<float>(lhs2 + i, vc)) * new Vector<float>(new Span<float>(rhs2 + i, vc))).CopyTo(target, i);
            }
            for (; i < count; i++) {
                target[i] += lhs1[i] * rhs1[i] + lhs2[i] * rhs2[i];
            }
        }

        /// <summary>
        /// Do <see cref="MultiplyAndAdd(float*, float, float[], int)"/> simultaneously for two different arrays.
        /// </summary>
        public static unsafe void MultiplyAndAdd(float* lhs1, float rhs1, float* lhs2, float rhs2, float[] target, int count) {
            int i = 0;
            for (int vc = Vector<float>.Count, c = count - vc; i < c; i += vc) {
                (new Vector<float>(target, i) + new Vector<float>(new Span<float>(lhs1 + i, vc)) * rhs1 +
                    new Vector<float>(new Span<float>(lhs2 + i, vc)) * rhs2).CopyTo(target, i);
            }
            for (; i < count; i++) {
                target[i] += lhs1[i] * rhs1 + lhs2[i] * rhs2;
            }
        }

        /// <summary>
        /// Clear the <paramref name="target"/>, then do <see cref="MultiplyAndAdd(float*, float*, float[], int)"/>.
        /// </summary>
        public static unsafe void MultiplyAndSet(float* lhs, float* rhs, float[] target, int count) {
            int i = 0;
            for (int vc = Vector<float>.Count, c = count - vc; i < c; i += vc) {
                (new Vector<float>(new Span<float>(lhs + i, vc)) * new Vector<float>(new Span<float>(rhs + i, vc))).CopyTo(target, i);
            }
            for (; i < count; i++) {
                target[i] = lhs[i] * rhs[i];
            }
        }

        /// <summary>
        /// Clear the <paramref name="target"/>, then do <see cref="MultiplyAndAdd(float*, float*, float*, float*, float[], int)"/>.
        /// </summary>
        public static unsafe void MultiplyAndSet(float* lhs1, float* rhs1, float* lhs2, float* rhs2, float[] target, int count) {
            int i = 0;
            for (int vc = Vector<float>.Count, c = count - vc; i < c; i += vc) {
                (new Vector<float>(new Span<float>(lhs1 + i, vc)) * new Vector<float>(new Span<float>(rhs1 + i, vc)) +
                    new Vector<float>(new Span<float>(lhs2 + i, vc)) * new Vector<float>(new Span<float>(rhs2 + i, vc))).CopyTo(target, i);
            }
            for (; i < count; i++) {
                target[i] = lhs1[i] * rhs1[i] + lhs2[i] * rhs2[i];
            }
        }

        /// <summary>
        /// Clear the <paramref name="target"/>, then do <see cref="MultiplyAndAdd(float*, float, float*, float, float[], int)"/>.
        /// </summary>
        public static unsafe void MultiplyAndSet(float* lhs1, float rhs1, float* lhs2, float rhs2, float[] target, int count) {
            int i = 0;
            for (int vc = Vector<float>.Count, c = count - vc; i < c; i += vc) {
                (new Vector<float>(new Span<float>(lhs1 + i, vc)) * rhs1 +
                    new Vector<float>(new Span<float>(lhs2 + i, vc)) * rhs2).CopyTo(target, i);
            }
            for (; i < count; i++) {
                target[i] = lhs1[i] * rhs1 + lhs2[i] * rhs2;
            }
        }
    }
}