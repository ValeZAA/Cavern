﻿using Cavern.Filters;
using Cavern.Utilities;
using System;
using System.Numerics;

namespace Cavern.Virtualizer {
    /// <summary>Handles distancing calculations for a single source's two ears.</summary>
    public class DistancerMaster {
        /// <summary>The filtered source.</summary>
        readonly Source source;

        /// <summary>The left ear's convolution impulse.</summary>
        public float[] LeftFilter { get; private set; }

        /// <summary>The right ear's convolution impulse.</summary>
        public float[] RightFilter { get; private set; }

        /// <summary>The maximum length of any of the <see cref="impulses"/>, because if the <see cref="FastConvolver"/> is used, the arrays won't be
        /// reassigned and the filter won't cut out, and if the <see cref="SpikeConvolver"/> is used, the overhead is basically zero.</summary>
        int filterSize;

        /// <summary>Create a distance simulation for a <see cref="Source"/>.</summary>
        public DistancerMaster(Source source) {
            this.source = source;
            source.VolumeRolloff = Rolloffs.Disabled;
            for (int i = 0; i < impulses.Length; ++i)
                for (int j = 0; j < impulses[i].Length; ++j)
                    if (filterSize < impulses[i][j].Length)
                        filterSize = impulses[i][j].Length;
        }

        /// <summary>Generate the left/right ear filters.</summary>
        /// <param name="right">The object is to the right of the <see cref="Listener"/>'s forward vector.</param>
        public void Generate(bool right) {
            float dirMul = -90;
            if (right)
                dirMul = 90;
            Vector3 sourceForward = new Vector3(0, dirMul, 0).RotateInverse(source.listener.Rotation).PlaceInSphere(),
                dir = source.Position - source.listener.Position;
            float distance = dir.Length(),
                rawAngle = (float)Math.Acos(Vector3.Dot(sourceForward, dir) / distance),
                angle = rawAngle * VectorExtensions.Rad2Deg;

            // Find bounding angles with discrete impulses
            int smallerAngle = 0;
            while (smallerAngle < angles.Length && angles[smallerAngle] < angle)
                ++smallerAngle;
            --smallerAngle;
            int largerAngle = smallerAngle + 1;
            if (largerAngle == angles.Length)
                largerAngle = angles.Length - 1;
            float angleRatio = Math.Min(QMath.LerpInverse(angles[smallerAngle], angles[largerAngle], angle), 1);

            // Find bounding distances with discrete impulses
            int smallerDistance = 0;
            while (smallerDistance < distances.Length && distances[smallerDistance] < distance)
                ++smallerDistance;
            --smallerDistance;
            int largerDistance = smallerDistance + 1;
            if (largerDistance == distances.Length)
                largerDistance = distances.Length - 1;
            float distanceRatio = QMath.Clamp(QMath.LerpInverse(distances[smallerDistance], distances[largerDistance], distance), 0, 1);

            // Find impulse candidates and their weight
            float[][] candidates = new float[4][] {
                impulses[smallerAngle][smallerDistance],
                impulses[smallerAngle][largerDistance],
                impulses[largerAngle][smallerDistance],
                impulses[largerAngle][largerDistance]
            };
            float[] gains = new float[4] {
                (float)Math.Sqrt((1 - angleRatio) * (1 - distanceRatio)),
                (float)Math.Sqrt((1 - angleRatio) * distanceRatio),
                (float)Math.Sqrt(angleRatio * (1 - distanceRatio)),
                (float)Math.Sqrt(angleRatio * distanceRatio)
            };

            // Create the main filter
            float[] filterImpulse = new float[filterSize];
            for (int candidate = 0; candidate < candidates.Length; ++candidate)
                WaveformUtils.Mix(candidates[candidate], filterImpulse, gains[candidate]);

            // Create a delay for the other ear
            int delay = 1;
            while (delay < filterImpulse.Length && filterImpulse[delay] == 0)
                ++delay;
            float[] delayImpulse = new float[delay];

            // Find the gain difference and apply it for the delay
            float angleDiff = (float)(Math.Sin(rawAngle) * .097f);
            float ratioDiff = (distance + angleDiff) * (VirtualizerFilter.referenceDistance - angleDiff) /
                             ((distance - angleDiff) * (VirtualizerFilter.referenceDistance + angleDiff));
            if (ratioDiff > 1)
                delayImpulse[delay - 1] = 1 / ratioDiff;
            else
                delayImpulse[delay - 1] = ratioDiff;

            // Extract the filter
            if (right) {
                LeftFilter = delayImpulse;
                RightFilter = filterImpulse;
            } else {
                LeftFilter = filterImpulse;
                RightFilter = delayImpulse;
            }
        }

        /// <summary>All the angles that have their own impulse responses.</summary>
        static readonly float[] angles = new float[7] { 0, 15, 30, 45, 60, 75, 90 };
        /// <summary>All the distances that have their own impulse responses for each angle in meters.</summary>
        static readonly float[] distances = new float[4] { .1f, .5f, 1, 2 };

        /// <summary>Ear canal distortion impulse responses for given angles and distances. The first dimension is the angle,
        /// provided in <see cref="angles"/>, and the second dimension is the distance, provided in <see cref="distances"/>.</summary>
        static readonly float[][][] impulses = new float[7][][] {
            new float[4][] {
                new float[23] { 0, 0, 0, 0, 0, 0, .005060405f, .370165f, .7765994f, .963137f, .8465202f, .8024259f, .7807245f, .7166646f, .6186926f, .4751698f, .2959671f, .2170968f, .1505986f, .08992894f, .03626856f, .009816867f, .0009188529f },
                new float[83] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .2254526f, .489379f, .5419078f, .4280191f, .384966f, .3561029f, .3694531f, .273842f, .2655759f, .2650507f, .215067f, .2208568f, .1741736f, .1184689f, .08905829f, .04599712f, .01989763f, .006106794f },
                new float[167] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .1021404f, .2940448f, .4853287f, .4306912f, .3643141f, .3160195f, .3214442f, .2198477f, .1780103f, .2132602f, .1656031f, .1298992f, .117907f, .09417684f, .09399163f, .02347218f, .02343179f, .01169635f },
                new float[364] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .2555006f, .4465739f, .4672332f, .4241904f, .3601693f, .317319f, .2957991f, .2743649f, .2318606f, .06318209f, .1051346f, .147005f, .1468251f, .06285588f, .04186194f, .02089582f, .02086996f, .02084631f }
            },
            new float[4][] {
                new float[16] { 0, 0, 0, 0, 0, .2540428f, .618826f, .7922781f, .5469493f, .3315574f, .199326f, .121843f, .1018899f, .06081806f, .01992492f, .002486186f },
                new float[66] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .09916351f, .2840413f, .4795852f, .544922f, .4341253f, .470324f, .4003018f, .4059214f, .2930336f, .2253603f, .1818558f, .1448934f, .1096459f, .07136548f, .03641209f, .02053734f, .003155366f },
                new float[134] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .1586088f, .4180621f, .5375387f, .4103165f, .4663895f, .408951f, .3579919f, .2946973f, .2628989f, .2311984f, .2120828f, .1431974f, .1616223f, .1117149f, .07432533f, .06183909f, .01852315f },
                new float[304] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .0460467f, .2530732f, .5514097f, .3900569f, .4812818f, .3661653f, .3656398f, .2054189f, .2963142f, .3186648f, .1818931f, .09080185f, .09070366f, .1584972f, .09046178f, .158059f, .06765252f, 0, .02250617f }
            },
            new float[4][] {
                new float[13] { 0, 0, 0, .1050854f, .5309059f, .8102948f, .7725348f, .3768995f, .1718067f, .1600976f, .1167709f, .04386054f, .01000423f },
                new float[50] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .2106925f, .5591921f, .5947716f, .5670011f, .5475857f, .5051002f, .4016184f, .3265782f, .2304685f, .1905839f, .1377254f, .1031048f, .06696148f, .02119071f, .01139209f },
                new float[100] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .0805884f, .40914f, .669571f, .6015406f, .5137377f, .4794751f, .3456331f, .2786785f, .3113221f, .310726f, .2969636f, .2041848f, .1709308f, .0853182f, .05896522f },
                new float[240] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .1761677f, .4523828f, .451845f, .5764098f, .4755346f, .4498033f, .4241526f, .2990268f, .3732281f, .3229832f, .1985206f, .1486002f, .1978573f, .172844f, .04932022f, .07387928f, 0, .04910687f }
            },
            new float[4][] {
                new float[12] { 0, .01470866f, .2668808f, .6709758f, .8696038f, .6238111f, .2982592f, .1819701f, .1314542f, .06732406f, .02134668f, .002193517f },
                new float[36] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .07025311f, .2891384f, .4695353f, .8894784f, .6768143f, .587148f, .4674066f, .3971567f, .3170993f, .1885206f, .1125487f, .04694382f, .005021037f },
                new float[72] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .3047225f, .3182212f, .5082004f, .6551769f, .5977036f, .5544696f, .4903019f, .4404049f, .3140291f, .3691338f, .2502432f, .159578f, .04848383f, .02073469f, .006904373f },
                new float[175] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .1666749f, .3884387f, .2770164f, .5254929f, .7177687f, .3032225f, .5229709f, .1923689f, .5486845f, .2191968f, .2734259f, .2457759f, .354367f, .05441149f, .1086966f, .0813694f, .05420946f }
            },
            new float[4][] {
                new float[12] { .009613713f, .1906747f, .6507455f, .8635021f, .6925584f, .3429887f, .2285819f, .1412279f, .06935507f, .03271831f, .01224353f, .00156687f },
                new float[28] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .1610606f, .5619036f, .7142545f, .9916827f, 1f, .718495f, .448316f, .2595796f, .04602725f, .01360753f },
                new float[50] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .02210077f, .2060175f, .4772561f, .6082825f, .8775923f, 1f, .8450313f, .6543332f, .4643365f, .3259043f, .1445349f, .07934242f, .007204685f },
                new float[120] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .1826799f, .4258f, .4857594f, 1f, .6956412f, .6643257f, .6630311f, .4513865f, .360483f, .4198555f, .1197096f, .1792739f, .11933f, 0, 0, .02969324f }
            },
            new float[4][] {
                new float[15] { .04263178f, .2895508f, .6382924f, .747534f, .6445036f, .4407095f, .2630998f, .157976f, .09709516f, .06700681f, .03422115f, .009398301f, .005941324f, .001247929f, .0006229539f },
                new float[44] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .005174049f, .07228132f, .2130509f, .2674596f, .3507443f, .4132414f, .4498594f, .4217311f, .3818137f, .3624241f, .2856064f, .2833472f, .2490722f, .2032441f, .1558968f, .173982f, .1218763f, .1283033f, .06484295f, .05974399f, .03808018f, .03800479f, .01978673f, .02469312f },
                new float[54] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .08108839f, .2795981f, .3671861f, .3444276f, .526583f, .5328071f, .5682167f, .5524712f, .457112f, .3548413f, .3035131f, .1875393f, .2159518f, .107756f, .1147124f, .07157325f, .02855934f },
                new float[92] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .0959605f, .3513282f, .3825811f, .636355f, .3811103f, .6657124f, .6962913f, .7582012f, .504584f, .5665219f, .2827532f, .3136709f, .3130487f, .03126271f, .09355384f }
            },
            new float[4][] {
                new float[83] { .0326911f, .1708663f, .4240082f, .7114658f, .9169419f, 1f, .9532143f, .9104362f, .7896323f, .7725042f, .6412572f, .6558433f, .5694332f, .5304774f, .4910967f, .4241953f, .4270413f, .3846796f, .3662941f, .3522813f, .2823929f, .3140138f, .2383744f, .2910275f, .2038697f, .256068f, .1547865f, .1868702f, .1248239f, .1263866f, .07705943f, .08231364f, .03987032f, .05475506f, .02686741f, .04410019f, .0142729f, .02819568f, .01006775f, .0224639f, .008259573f, .0167803f, .005583021f, .01495478f, .004974272f, .01022128f, .003496938f, .01134514f, .002613071f, .009560701f, .00375808f, .009235928f, .0008645395f, .006324044f, 0, .004867199f, 0, .005418296f, .0002848506f, .00369269f, 0, .003395122f, .0002826647f, .004227776f, 0, .003931014f, 0, .003635701f, 0, .006963848f, 0, .002497675f, .0005547092f, .006081558f, .001105101f, .001927557f, 0, .002194788f, .00137125f, .000273304f, .0002732188f, .0008167807f, .0005443394f },
                new float[86] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .003225689f, .004829585f, .01285952f, .01123487f, .008007418f, .004796268f, .009570844f, .009554436f, .01589041f, .01268628f, .007914538f, .006322234f, .01261584f, .01258708f, .01256413f, .01254193f, .01251708f, .007809167f, .01091089f, .009334469f, .01552888f, .02325264f, .00464179f, .01389384f, .02312111f, .01230752f, .006140114f, .00306622f, .01682533f, .003055333f, .0030476f, 0, .007590377f },
                new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 },
                new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }
            }
        };
    }
}