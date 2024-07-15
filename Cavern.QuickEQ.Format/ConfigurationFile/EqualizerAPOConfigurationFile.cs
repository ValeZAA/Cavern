﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Cavern.Channels;
using Cavern.Filters;
using Cavern.Filters.Interfaces;
using Cavern.Filters.Utilities;
using Cavern.Format.Common;
using Cavern.Utilities;

namespace Cavern.Format.ConfigurationFile {
    /// <summary>
    /// Parsed single Equalizer APO configuration file.
    /// </summary>
    public sealed class EqualizerAPOConfigurationFile : ConfigurationFile {
        /// <inheritdoc/>
        public override string FileExtension => "txt";

        /// <summary>
        /// Convert an<paramref name="other"/> configuration file to Equalizer APO's format.
        /// </summary>
        public EqualizerAPOConfigurationFile(ConfigurationFile other) : base(other) { }

        /// <summary>
        /// Parse a single Equalizer APO configuration file.
        /// </summary>
        /// <param name="path">Filesystem location of the configuration file</param>
        /// <param name="sampleRate">The sample rate to use for the internally created filters</param>
        public EqualizerAPOConfigurationFile(string path, int sampleRate) : base(Path.GetFileNameWithoutExtension(path), channelLabels) {
            Dictionary<string, FilterGraphNode> lastNodes = InputChannels.ToDictionary(x => x.name, x => x.root);
            List<string> activeChannels = channelLabels.ToList();
            AddConfigFile(path, lastNodes, activeChannels, sampleRate);

            for (int i = 0; i < channelLabels.Length; i++) { // Output markers
                lastNodes[channelLabels[i]].AddChild(new FilterGraphNode(new OutputChannel(channelLabels[i])));
            }
            Optimize();
        }

        /// <inheritdoc/>
        public override void Export(string path) {
            string GetChannelLabel(int channel) { // Convert index to label
                if (channel < 0) {
                    return "V" + -channel;
                } else {
                    return InputChannels[channel].name;
                }
            }

            List<string> result = new List<string>();
            void AppendSelector(string newLine) { // Add this step, and overwrite the previous line if it selected an unfiltered channel
                int last = result.Count - 1;
                if (last != -1 && result[last].StartsWith(channelFilter)) {
                    result[last] = newLine; // No filter comes after this selector, overwrite it
                } else {
                    result.Add(newLine);
                }
            }

            (FilterGraphNode node, int channel)[] exportOrder = GetExportOrder();
            int lastChannel = int.MaxValue;
            for (int i = 0; i < exportOrder.Length; i++) {
                int channel = exportOrder[i].channel;
                int[] parents = GetExportedParents(exportOrder, i);
                if (parents.Length == 0 || (parents.Length == 1 && parents[0] == channel)) {
                    // If there are no parents or the single parent is from the same channel, don't mix
                } else {
                    AppendSelector($"Copy: {GetChannelLabel(channel)}={string.Join('+', parents.Select(GetChannelLabel))}");
                }

                if (channel != lastChannel) { // When the channel has changed, select it
                    AppendSelector(channelFilter + GetChannelLabel(channel));
                    lastChannel = channel;
                }

                Filter baseFilter = exportOrder[i].node.Filter;
                if (baseFilter == null || baseFilter is BypassFilter) {
                    continue;
                }
                if (baseFilter is IEqualizerAPOFilter filter) {
                    filter.ExportToEqualizerAPO(result);
                } else {
                    throw new NotEqualizerAPOFilterException(baseFilter);
                }
            }

            int last = result.Count - 1;
            if (last != -1 && result[last].StartsWith(channelFilter)) {
                result.RemoveAt(last); // A selector of a bypass might remain
            }
            File.WriteAllLines(path, result);
        }

        /// <summary>
        /// Read a configuration file and append it to the previously parsed configuration.
        /// </summary>
        void AddConfigFile(string path, Dictionary<string, FilterGraphNode> lastNodes, List<string> activeChannels, int sampleRate) {
            foreach (string line in File.ReadLines(path)) {
                string[] split = line.Split(new[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length <= 1) {
                    continue;
                }

                switch (split[0].ToLowerInvariant()) {
                    // Control
                    case "include":
                        string included = Path.Combine(Path.GetDirectoryName(path), string.Join(' ', split, 1, split.Length - 1));
                        CreateSplit(Path.GetFileNameWithoutExtension(included), lastNodes);
                        AddConfigFile(included, lastNodes, activeChannels, sampleRate);
                        break;
                    case "channel":
                        activeChannels.Clear();
                        if (split.Length == 2 && split[1].ToLowerInvariant() == "all") {
                            activeChannels.AddRange(channelLabels);
                            continue;
                        }

                        for (int i = 1; i < split.Length; i++) {
                            if (lastNodes.ContainsKey(split[i])) {
                                activeChannels.Add(split[i]);
                            } else {
                                throw new InvalidChannelException(split[i]);
                            }
                        }
                        break;
                    // Basic filters
                    case "preamp":
                        double gain = double.Parse(split[1].Replace(',', '.'), CultureInfo.InvariantCulture);
                        AddFilter(lastNodes, activeChannels, new Gain(gain));
                        break;
                    case "delay":
                        AddFilter(lastNodes, activeChannels, Delay.FromEqualizerAPO(split, sampleRate));
                        break;
                    case "copy":
                        Dictionary<string, FilterGraphNode> oldLastNodes = lastNodes.ToDictionary(x => x.Key, x => x.Value);
                        for (int i = 1; i < split.Length; i++) {
                            string[] copy = split[i].Split(new[] { '=', '+' });
                            FilterGraphNode target = new FilterGraphNode(null);
                            for (int j = 1; j < copy.Length; j++) {
                                string channel;
                                int mul = copy[j].IndexOf('*');
                                if (mul != -1) {
                                    channel = copy[j][(mul + 1)..];
                                    double copyGain = double.Parse(copy[j][..mul].Replace(',', '.'), CultureInfo.InvariantCulture),
                                        gainDb = QMath.GainToDb(Math.Abs(copyGain));
                                    Gain gainFilter = new Gain(gainDb) {
                                        Invert = gainDb >= 0
                                    };
                                    FilterGraphNode gainNode = new FilterGraphNode(gainFilter);
                                    gainNode.AddParent(oldLastNodes[channel]);
                                    target.AddParent(gainNode);
                                } else {
                                    channel = copy[j];
                                    target.AddParent(oldLastNodes[channel]);
                                }
                            }
                            if (!lastNodes.ContainsKey(copy[0])) {
                                target.Filter = new BypassFilter(copy[0]);
                            }
                            lastNodes[copy[0]] = target;
                        }
                        break;
                    // Parametric filters
                    case "filter":
                        AddFilter(lastNodes, activeChannels, BiquadFilter.FromEqualizerAPO(split, sampleRate));
                        break;
                    // Graphic equalizers
                    case "graphiceq":
                        AddFilter(lastNodes, activeChannels, GraphicEQ.FromEqualizerAPO(split, sampleRate));
                        break;
                    case "convolution":
                        string convolution = Path.Combine(Path.GetDirectoryName(path), line[(line.IndexOf(' ') + 1)..]);
                        AddFilter(lastNodes, activeChannels, new FastConvolver(AudioReader.Open(convolution).Read()));
                        break;
                }
            }
        }

        /// <summary>
        /// Add a filter to the currently active channels.
        /// </summary>
        void AddFilter(Dictionary<string, FilterGraphNode> lastNodes, List<string> channels, Filter filter) {
            List<FilterGraphNode> addedTo = new List<FilterGraphNode>();
            bool clone = false; // Filters have to be individually editable on different paths = make copies after the first was set
            for (int i = 0, c = channels.Count; i < c; i++) {
                FilterGraphNode oldLastNode = lastNodes[channels[i]];
                if (addedTo.Contains(oldLastNode)) {
                    lastNodes[channels[i]] = oldLastNode.Children[^1]; // The channel pipelines were merged with a Copy filter
                } else {
                    lastNodes[channels[i]] = oldLastNode.AddChild(clone ? (Filter)filter.Clone() : filter);
                    clone = true;
                    addedTo.Add(oldLastNode);
                }
            }
        }

        /// <summary>
        /// Mark the current point of the configuration as the beginning of the next section of filters or next pipeline step.
        /// </summary>
        void CreateSplit(string name, Dictionary<string, FilterGraphNode> lastNodes) {
            KeyValuePair<string, FilterGraphNode>[] outputs =
                lastNodes.Where(x => ReferenceChannelExtensions.FromStandardName(x.Key) != ReferenceChannel.Unknown).ToArray();
            for (int i = 0; i < outputs.Length; i++) {
                lastNodes[outputs[i].Key] = lastNodes[outputs[i].Key].AddChild(new OutputChannel(outputs[i].Key));
            }
            CreateNewSplitPoint(name);
            for (int i = 0; i < outputs.Length; i++) {
                lastNodes[outputs[i].Key] = lastNodes[outputs[i].Key].Children[0];
            }
        }

        /// <summary>
        /// Default initial channels in Equalizer APO.
        /// </summary>
        static readonly string[] channelLabels = { "L", "R", "C", "SUB", "RL", "RR", "SL", "SR" };

        /// <summary>
        /// Prefix for channel selection lines in an Equalizer APO configuration file.
        /// </summary>
        const string channelFilter = "Channel: ";
    }
}