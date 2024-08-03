﻿using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using Cavern.Channels;
using Cavern.Filters;
using Cavern.Format.Common;

namespace Cavern.Format.FilterSet {
    /// <summary>
    /// A filter set containing equalization info for each channel of a system.
    /// </summary>
    public abstract class FilterSet : IExportable {
        /// <summary>
        /// Basic information needed for a channel.
        /// </summary>
        public abstract class ChannelData {
            /// <summary>
            /// The reference channel describing this channel or <see cref="ReferenceChannel.Unknown"/> if not applicable.
            /// </summary>
            public ReferenceChannel reference;

            /// <summary>
            /// Custom label for this channel or null if not applicable.
            /// </summary>
            public string name;

            /// <summary>
            /// Delay of this channel in samples.
            /// </summary>
            public int delaySamples;
        }

        /// <summary>
        /// Applied filters for each channel in the configuration file.
        /// </summary>
        public ChannelData[] Channels { get; protected set; }

        /// <summary>
        /// Sample rate of the filter set.
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// The number of channels to EQ.
        /// </summary>
        public int ChannelCount => Channels.Length;

        /// <summary>
        /// Some targets use the user's culture in their exports. These targets should override this value with
        /// the desired export culture, <see cref="CultureInfo.CurrentCulture"/> by default.
        /// </summary>
        public CultureInfo Culture { get; protected set; } = CultureInfo.InvariantCulture;

        /// <inheritdoc/>
        public virtual string FileExtension => "txt";

        /// <summary>
        /// A filter set containing equalization info for each channel of a system on a given sample rate.
        /// </summary>
        protected FilterSet(int sampleRate) => SampleRate = sampleRate;

        /// <inheritdoc/>
        public abstract void Export(string path);

        /// <summary>
        /// Convert the filter set to convolution impulse responses to be used with e.g. a <see cref="MultichannelConvolver"/>.
        /// </summary>
        public abstract MultichannelWaveform GetConvolutionFilter(int sampleRate, int convolutionLength);

        /// <summary>
        /// Create a filter set for the target <paramref name="device"/>.
        /// </summary>
        public static FilterSet Create(FilterSetTarget device, int channels, int sampleRate) {
            return device switch {
                FilterSetTarget.Generic => new IIRFilterSet(channels, sampleRate),
                FilterSetTarget.GenericConvolution => new FIRFilterSet(channels, sampleRate),
                FilterSetTarget.GenericEqualizer => new EqualizerFilterSet(channels, sampleRate),
                FilterSetTarget.EqualizerAPO_EQ => new EqualizerAPOEqualizerFilterSet(channels, sampleRate),
                FilterSetTarget.EqualizerAPO_FIR => new EqualizerAPOFIRFilterSet(channels, sampleRate),
                FilterSetTarget.EqualizerAPO_IIR => new EqualizerAPOIIRFilterSet(channels, sampleRate),
                FilterSetTarget.CamillaDSP => new CamillaDSPFilterSet(channels, sampleRate),
                FilterSetTarget.AUNBandEQ => new AUNBandEQ(channels, sampleRate),
                FilterSetTarget.MiniDSP2x4Advanced => new MiniDSP2x4FilterSet(channels),
                FilterSetTarget.MiniDSP2x4AdvancedLite => new MiniDSP2x4FilterSetLite(channels),
                FilterSetTarget.MiniDSP2x4HD => new MiniDSP2x4HDFilterSet(channels),
                FilterSetTarget.MiniDSP2x4HDLite => new MiniDSP2x4HDFilterSetLite(channels),
                FilterSetTarget.MiniDSPDDRC88A => new MiniDSPDDRC88AFilterSet(channels),
                FilterSetTarget.AcurusMuse => new AcurusMuseFilterSet(channels, sampleRate),
                FilterSetTarget.Emotiva => new EmotivaFilterSet(channels, sampleRate),
                FilterSetTarget.MonolithHTP1 => new MonolithHTP1FilterSet(channels, sampleRate),
                FilterSetTarget.StormAudio => new StormAudioFilterSet(channels, sampleRate),
                FilterSetTarget.BehringerNX => new BehringerNXFilterSet(channels, sampleRate),
                FilterSetTarget.DiracLive => new DiracLiveFilterSet(channels, sampleRate),
                FilterSetTarget.DiracLiveBassControl => new DiracLiveBassControlFilterSet(channels, sampleRate),
                FilterSetTarget.DiracLiveBassControlCombined => new DiracLiveBassControlFilterSet(channels, sampleRate) {
                    CombineHeights = true
                },
                FilterSetTarget.MultEQX => new MultEQXFilterSet(channels, sampleRate),
                FilterSetTarget.MultEQXRaw => new MultEQXRawFilterSet(channels, sampleRate),
                FilterSetTarget.MultEQXTarget => new MultEQXTargetFilterSet(channels, sampleRate),
                FilterSetTarget.YPAO => new YPAOFilterSet(channels, sampleRate),
                FilterSetTarget.YPAOLite => new YPAOLiteFilterSet(channels, sampleRate),
                FilterSetTarget.Multiband31 => new Multiband31FilterSet(channels, sampleRate),
                _ => throw new NotSupportedException()
            };
        }

        /// <summary>
        /// Create a filter set for the target <paramref name="device"/>.
        /// </summary>
        public static FilterSet Create(FilterSetTarget device, ReferenceChannel[] channels, int sampleRate) {
            return device switch {
                FilterSetTarget.Generic => new IIRFilterSet(channels, sampleRate),
                FilterSetTarget.GenericConvolution => new FIRFilterSet(channels, sampleRate),
                FilterSetTarget.GenericEqualizer => new EqualizerFilterSet(channels, sampleRate),
                FilterSetTarget.EqualizerAPO_EQ => new EqualizerAPOEqualizerFilterSet(channels, sampleRate),
                FilterSetTarget.EqualizerAPO_FIR => new EqualizerAPOFIRFilterSet(channels, sampleRate),
                FilterSetTarget.EqualizerAPO_IIR => new EqualizerAPOIIRFilterSet(channels, sampleRate),
                FilterSetTarget.CamillaDSP => new CamillaDSPFilterSet(channels, sampleRate),
                FilterSetTarget.AUNBandEQ => new AUNBandEQ(channels, sampleRate),
                FilterSetTarget.MiniDSP2x4Advanced => new MiniDSP2x4FilterSet(channels),
                FilterSetTarget.MiniDSP2x4AdvancedLite => new MiniDSP2x4FilterSetLite(channels),
                FilterSetTarget.MiniDSP2x4HD => new MiniDSP2x4HDFilterSet(channels),
                FilterSetTarget.MiniDSP2x4HDLite => new MiniDSP2x4HDFilterSetLite(channels),
                FilterSetTarget.MiniDSPDDRC88A => new MiniDSPDDRC88AFilterSet(channels),
                FilterSetTarget.AcurusMuse => new AcurusMuseFilterSet(channels, sampleRate),
                FilterSetTarget.Emotiva => new EmotivaFilterSet(channels, sampleRate),
                FilterSetTarget.MonolithHTP1 => new MonolithHTP1FilterSet(channels, sampleRate),
                FilterSetTarget.StormAudio => new StormAudioFilterSet(channels, sampleRate),
                FilterSetTarget.BehringerNX => new BehringerNXFilterSet(channels, sampleRate),
                FilterSetTarget.DiracLive => new DiracLiveFilterSet(channels, sampleRate),
                FilterSetTarget.DiracLiveBassControl => new DiracLiveBassControlFilterSet(channels, sampleRate),
                FilterSetTarget.DiracLiveBassControlCombined => new DiracLiveBassControlFilterSet(channels, sampleRate) {
                    CombineHeights = true
                },
                FilterSetTarget.MultEQX => new MultEQXFilterSet(channels, sampleRate),
                FilterSetTarget.MultEQXRaw => new MultEQXRawFilterSet(channels, sampleRate),
                FilterSetTarget.MultEQXTarget => new MultEQXTargetFilterSet(channels, sampleRate),
                FilterSetTarget.YPAO => new YPAOFilterSet(channels, sampleRate),
                FilterSetTarget.YPAOLite => new YPAOLiteFilterSet(channels, sampleRate),
                FilterSetTarget.Multiband31 => new Multiband31FilterSet(channels, sampleRate),
                _ => throw new NotSupportedException()
            };
        }

        /// <summary>
        /// Set the <paramref name="delay"/> in samples for a given <paramref name="channel"/>.
        /// </summary>
        public void OverrideDelay(int channel, int delay) => Channels[channel].delaySamples = delay;

        /// <summary>
        /// Get the short name of a channel written to the configuration file to select that channel for setup.
        /// </summary>
        protected virtual string GetLabel(int channel) => Channels[channel].name ?? "CH" + (channel + 1);

        /// <summary>
        /// Add extra information for a channel that can't be part of the filter files to be written in the root file.
        /// </summary>
        protected virtual void RootFileExtension(int channel, StringBuilder result) { }

        /// <summary>
        /// Initialize the data holders of <see cref="Channels"/> with the default <see cref="ReferenceChannel"/>s.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Initialize<T>(int channels) where T : ChannelData, new() {
            Channels = new T[channels];
            ReferenceChannel[] matrix = ChannelPrototype.GetStandardMatrix(channels);
            for (int i = 0; i < channels; i++) {
                Channels[i] = new T {
                    reference = matrix[i]
                };
            }
        }

        /// <summary>
        /// Initialize the data holders of <see cref="Channels"/> with the correct <see cref="ReferenceChannel"/>s.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Initialize<T>(ReferenceChannel[] channels) where T : ChannelData, new() {
            Channels = new T[channels.Length];
            for (int i = 0; i < channels.Length; i++) {
                Channels[i] = new T {
                    reference = channels[i]
                };
            }
        }

        /// <summary>
        /// Get the delay for a given channel in milliseconds instead of samples.
        /// </summary>
        protected double GetDelay(int channel) => Channels[channel].delaySamples * 1000.0 / SampleRate;

        /// <summary>
        /// Create the file with gain/delay/polarity info as the root document that's saved in the save dialog.
        /// </summary>
        protected void CreateRootFile(string path, string filterFileExtension) {
            string fileNameBase = Path.GetFileName(path);
            fileNameBase = fileNameBase[..fileNameBase.LastIndexOf('.')];
            StringBuilder result = new StringBuilder();
            bool hasAnything = false,
                hasDelays = false;
            for (int i = 0, c = Channels.Length; i < c; i++) {
                result.AppendLine(string.Empty);
                result.AppendLine("Channel: " + GetLabel(i));
                if (Channels[i].delaySamples != 0) {
                    result.AppendLine("Delay: " + GetDelay(i).ToString("0.0 ms"));
                    hasAnything = true;
                    hasDelays = true;
                }
                int before = result.Length;
                RootFileExtension(i, result);
                hasAnything |= result.Length != before;
            }
            if (hasAnything) {
                File.WriteAllText(path, (hasDelays ?
                    $"Set up levels and delays by this file. Load \"{fileNameBase} <channel>.{filterFileExtension}\" files as EQ." :
                    $"Set up levels by this file. Load \"{fileNameBase} <channel>.{filterFileExtension}\" files as EQ.") +
                    result);
            }
        }
    }
}