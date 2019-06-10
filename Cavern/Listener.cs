﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Cavern.Filters;
using Cavern.Utilities;

namespace Cavern {
    /// <summary>Center of a listening space.</summary>
    public class Listener {
        // ------------------------------------------------------------------
        // Renderer settings
        // ------------------------------------------------------------------
        /// <summary>Absolute spatial position.</summary>
        public Vector Position;

        /// <summary>Rotation in Euler angles (degrees).</summary>
        public Vector Rotation;

        /// <summary>3D environment type.</summary>
        /// <remarks>Set by the user and applied when a <see cref="Listener"/> is created. Don't override without user interaction.</remarks>
        public static Environments EnvironmentType = Environments.Home;

        /// <summary>Virtual surround effect for headphones. This will replace the active <see cref="Channels"/>.</summary>
        /// <remarks>Set by the user and applied when a <see cref="Listener"/> is created. Don't override without user interaction.</remarks>
        public static bool HeadphoneVirtualizer = false;

        /// <summary>Output channel layout. The default setup is the standard 5.1.</summary>
        /// <remarks>Set by the user and applied when a <see cref="Listener"/> is created. Don't override without user interaction.</remarks>
        public static Channel[] Channels = { new Channel(0, -45), new Channel(0, 45), new Channel(0, 0),
                                             new Channel(15, 15, true), new Channel(0, -110), new Channel(0, 110) };

        /// <summary>Is the user's speaker layout symmetrical?</summary>
        public static bool IsSymmetric { get; internal set; }

        /// <summary>
        /// The single most important variable defining sound space in symmetric mode, the environment scaling. Originally set by the
        /// user and applied when a <see cref="Listener"/> is created, however, overriding it in specific applications can make a huge
        /// difference. Objects inside a box this size are positioned inside the room, and defines the range of balance between
        /// left/right, front/rear, and top/bottom speakers. Does not effect directional rendering. The user's settings should be
        /// respected, thus this vector should be scaled, not completely overridden.
        /// </summary>
        public static Vector EnvironmentSize = new Vector(10, 7, 10);

        /// <summary>How many sources can be played at the same time.</summary>
        public int MaximumSources {
            get => sourceLimit;
            set => sourceDistances = new float[sourceLimit = value];
        }

        // ------------------------------------------------------------------
        // Listener settings
        // ------------------------------------------------------------------
        /// <summary>Global playback volume.</summary>
        public float Volume = 1;
        /// <summary>LFE channels' volume.</summary>
        public float LFEVolume = 1;
        /// <summary>Hearing distance.</summary>
        public float Range = 100;

        // ------------------------------------------------------------------
        // Normalizer settings
        // ------------------------------------------------------------------
        /// <summary>Adaption speed of the normalizer. 0 means disabled.</summary>
        public float Normalizer = 1;
        /// <summary>If active, the normalizer won't increase the volume above 100%.</summary>
        public bool LimiterOnly = true;

        // ------------------------------------------------------------------
        // Advanced settings
        // ------------------------------------------------------------------
        /// <summary>Project sample rate (min. 44100). It's best to have all your audio clips in this sample rate for maximum performance.</summary>
        public int SampleRate = 48000;
        /// <summary>Update interval in audio samples (min. 16). Lower values mean better interpolation, but require more processing power.</summary>
        public int UpdateRate = 240;
        /// <summary>Maximum audio delay, defined in this FPS value. This is the minimum frame rate required to render continuous audio.</summary>
        public int DelayTarget = 12;
        /// <summary>Lower qualities increase performance for many sources.</summary>
        public QualityModes AudioQuality = QualityModes.High;
        /// <summary>Only mix LFE tagged sources to subwoofers.</summary>
        public bool LFESeparation = false;
        /// <summary>Disable lowpass on the LFE channel.</summary>
        public bool DirectLFE = false;

        // ------------------------------------------------------------------
        // Handlers
        // ------------------------------------------------------------------
        /// <summary>Attached <see cref="Source"/>s.</summary>
        public IReadOnlyCollection<Source> ActiveSources => activeSources;

        // ------------------------------------------------------------------
        // Public functions
        // ------------------------------------------------------------------
        /// <summary>Attach a source to this listener.</summary>
        public void AttachSource(Source source) {
            if (source.listener) // TODO: node caching for removal
                source.listener.DetachSource(source);
            source.listenerNode = activeSources.AddLast(source);
            source.listener = this;
        }

        /// <summary>Detach a source from this listener.</summary>
        public void DetachSource(Source source) {
            if (source == this) {
                activeSources.Remove(source.listenerNode);
                source.listener = null;
            }
        }

        /// <summary>Current speaker layout name in the format of &lt;main&gt;.&lt;LFE&gt;.&lt;height&gt;.&lt;floor&gt;, or simply
        /// "Virtualization".</summary>
        public static string GetLayoutName() {
            if (HeadphoneVirtualizer)
                return "Virtualization";
            else {
                int regular = 0, sub = 0, ceiling = 0, floor = 0;
                for (int channel = 0, channelCount = Channels.Length; channel < channelCount; ++channel)
                    if (Channels[channel].LFE) ++sub;
                    else if (Channels[channel].X == 0) ++regular;
                    else if (Channels[channel].X < 0) ++ceiling;
                    else if (Channels[channel].X > 0) ++floor;
                StringBuilder layout = new StringBuilder(regular.ToString()).Append('.').Append(sub);
                if (ceiling > 0 || floor > 0) layout.Append('.').Append(ceiling);
                if (floor > 0) layout.Append('.').Append(floor);
                return layout.ToString();
            }
        }

        /// <summary>Implicit null check.</summary>
        public static implicit operator bool(Listener listener) => listener != null;

        /// <summary>Center of a listening space.</summary>
        public Listener() {
            string fileName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Cavern\\Save.dat";
            if (File.Exists(fileName)) {
                string[] save = File.ReadAllLines(fileName);
                int savePos = 1;
                int channelCount = Convert.ToInt32(save[0]);
                Channels = new Channel[channelCount];
                NumberFormatInfo format = new NumberFormatInfo {
                    NumberDecimalSeparator = ","
                };
                for (int i = 0; i < channelCount; ++i)
                    Channels[i] = new Channel(Convert.ToSingle(save[savePos++], format), Convert.ToSingle(save[savePos++], format),
                        Convert.ToBoolean(save[savePos++]));
                EnvironmentType = (Environments)Convert.ToInt32(save[savePos++], format);
                EnvironmentSize = new Vector(Convert.ToSingle(save[savePos++], format), Convert.ToSingle(save[savePos++], format),
                    Convert.ToSingle(save[savePos++], format));
                HeadphoneVirtualizer = save.Length > savePos ? Convert.ToBoolean(save[savePos++]) : false; // Added: 2016.04.24.
                ++savePos; // Environment compensation (bool), added: 2017.06.18, removed: 2019.06.06.
            }
        }

        /// <summary>Ask for update ticks.</summary>
        public float[] Render(int frames = 1) {
            if (SampleRate < 44100 || UpdateRate < 16) // Don't work with wrong settings
                return null;
            for (int source = 0; source < sourceLimit; ++source)
                sourceDistances[source] = Range;
            pulseDelta = (frames * UpdateRate) / (float)SampleRate;
            LinkedListNode<Source> node = activeSources.First;
            while (node != null) {
                node.Value.Precalculate();
                node = node.Next;
            }
            if (frames == 1) return Frame();
            else {
                int sampleCount = frames * Channels.Length * UpdateRate;
                if (multiframeBuffer.Length != sampleCount)
                    multiframeBuffer = new float[sampleCount];
                for (int frame = 0; frame < frames; ++frame) {
                    float[] frameBuffer = Frame();
                    for (int sample = 0, samples = frameBuffer.Length, offset = frame * samples; sample < samples; ++sample)
                        multiframeBuffer[sample + offset] = frameBuffer[sample];
                }
                return multiframeBuffer;
            }
        }

        // ------------------------------------------------------------------
        // Private variables
        // ------------------------------------------------------------------
        /// <summary>Default value of <see cref="sourceLimit"/> and <see cref="MaximumSources"/>.</summary>
        const int defaultSourceLimit = 128;
        /// <summary>Position between the last and current game frame's playback position.</summary>
        internal float pulseDelta;
        /// <summary>Distances of sources from the listener.</summary>
        internal float[] sourceDistances = new float[defaultSourceLimit];
        /// <summary>The cached length of the <see cref="sourceDistances"/> array.</summary>
        internal int sourceLimit = defaultSourceLimit;

        /// <summary>Listener normalizer gain.</summary>
        float normalization = 1;
        /// <summary>Result of the last update. Size is [<see cref="Channels"/>.Length * <see cref="UpdateRate"/>].</summary>
        float[] renderBuffer;
        /// <summary>Same as <see cref="renderBuffer"/>, for multiple frames.</summary>
        float[] multiframeBuffer = new float[0];
        /// <summary>Optimization variables.</summary>
        int channelCount, lastSampleRate, lastUpdateRate;
        /// <summary>Attached <see cref="Source"/>s.</summary>
        LinkedList<Source> activeSources = new LinkedList<Source>();
        /// <summary>Lowpass filters for each channel.</summary>
        Lowpass[] lowpasses;

        /// <summary>Recreate optimization arrays.</summary>
        void Reoptimize() {
            channelCount = Channels.Length;
            lastSampleRate = SampleRate;
            lastUpdateRate = UpdateRate;
            int outputLength = channelCount * UpdateRate;
            renderBuffer = new float[outputLength];
            lowpasses = new Lowpass[channelCount];
            for (int i = 0; i < channelCount; ++i)
                lowpasses[i] = new Lowpass(SampleRate, 120);
        }

        /// <summary>A single update.</summary>
        float[] Frame() {
            if (channelCount != Channels.Length || lastSampleRate != SampleRate || lastUpdateRate != UpdateRate)
                Reoptimize();
            // Collect audio data from sources
            LinkedListNode<Source> node = activeSources.First;
            List<float[]> results = new List<float[]>();
            while (node != null) {
                if (node.Value.Precollect())
                    results.Add(node.Value.Collect());
                node = node.Next;
            }
            // Mix sources to output
            Array.Clear(renderBuffer, 0, renderBuffer.Length);
            for (int result = 0, resultCount = results.Count; result < resultCount; ++result)
                Utils.Mix(results[result], renderBuffer);
            // Volume, distance compensation, and subwoofers' lowpass
            for (int channel = 0; channel < channelCount; ++channel) {
                if (Channels[channel].LFE) {
                    if (!DirectLFE)
                        lowpasses[channel].Process(renderBuffer, channel, channelCount);
                    Utils.Gain(renderBuffer, LFEVolume * Volume, channel, channelCount); // LFE Volume
                } else
                    Utils.Gain(renderBuffer, Volume, channel, channelCount);
            }
            if (Normalizer != 0) // Normalize
                Utils.Normalize(ref renderBuffer, Normalizer * UpdateRate / SampleRate, ref normalization, LimiterOnly);
            return renderBuffer;
        }
    }
}