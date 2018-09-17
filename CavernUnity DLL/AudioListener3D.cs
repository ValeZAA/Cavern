﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

using Cavern.Utilities;

namespace Cavern {
    [AddComponentMenu("Audio/3D Audio Listener"), RequireComponent(typeof(AudioListener))]
    public partial class AudioListener3D : MonoBehaviour {
        // ------------------------------------------------------------------
        // Internal vars
        // ------------------------------------------------------------------
        /// <summary>Position between the last and current update frame's playback position.</summary>
        internal static float DeltaTime { get; private set; }
        /// <summary>Position between the last and current game frame's playback position.</summary>
        internal static float PulseDelta { get; private set; }
        /// <summary>The cached length of the <see cref="SourceDistances"/> array.</summary>
        internal static int SourceLimit = 128;
        /// <summary>Distances of sources from the listener.</summary>
        internal static float[] SourceDistances = new float[128];
        /// <summary>Cached number of output channels.</summary>
        internal static int ChannelCount { get; private set; }
        /// <summary>Last position of the active listener.</summary>
        internal static Vector3 LastPosition { get; private set; }
        /// <summary>Last rotation of the active listener.</summary>
        internal static Quaternion LastRotation { get; private set; }
        /// <summary>Inverse of the rotation of the active listener.</summary>
        internal static Quaternion LastRotationInverse { get; private set; }

        // ------------------------------------------------------------------
        // Private vars
        // ------------------------------------------------------------------
        /// <summary>List of enabled <see cref="AudioSource3D"/>'s.</summary>
        internal static LinkedList<AudioSource3D> ActiveSources = new LinkedList<AudioSource3D>();

        /// <summary>Cached <see cref="EnvironmentCompensation"/>.</summary>
        static bool CompensationCache;

        /// <summary>Listener normalizer gain.</summary>
        static float Normalization = 1;
        /// <summary>Maximal gain across all channels.</summary>
        static float MaxGain = 0;

        /// <summary>Distance-based gain for each channel.</summary>
        static float[] ChannelGains;

        /// <summary>Output timer.</summary>
        static int Now;
        /// <summary>Output timer in the last frame.</summary>
        static int LastTime;
        /// <summary>Cached <see cref="SampleRate"/> for change detection.</summary>
        static int CachedSampleRate = 0;
        /// <summary>Cached <see cref="UpdateRate"/> for change detection.</summary>
        static int CachedUpdateRate = 0;
        /// <summary>Current time in ticks in the last frame.</summary>
        static long LastTicks = 0;
        /// <summary>Ticks missed by integer division in the last frame. Required for perfect timing.</summary>
        static long AdditionMiss = 0;

        /// <summary>Cached <see cref="Channels"/> for change detection.</summary>
        static Channel[] ChannelCache;
        /// <summary>Lowpass filters for each channel.</summary>
        static Lowpass[] Lowpasses;

        // ------------------------------------------------------------------
        // Internal functions
        // ------------------------------------------------------------------
        /// <summary>Reset the listener after any change.</summary>
        void ResetFunc() {
            ChannelCount = Channels.Length;
            CompensationCache = !EnvironmentCompensation;
            Now = SampleRate;
            LastTime = 0;
            CachedSampleRate = SampleRate;
            CachedUpdateRate = UpdateRate;
            BufferPosition = 0;
            LastTicks = DateTime.Now.Ticks;
            Lowpasses = new Lowpass[ChannelCount];
            FilterOutput = new float[ChannelCount * SampleRate];
            // Optimization arrays
            ChannelGains = new float[ChannelCount];
            ChannelCache = new Channel[ChannelCount];
            for (int i = 0; i < ChannelCount; ++i) {
                ChannelCache[i] = Channels[i].Copy;
                Lowpasses[i] = new Lowpass(120);
            }
        }

        /// <summary>Normalize an array of samples.</summary>
        /// <param name="Target">Samples to normalize</param>
        /// <param name="TargetLength">Target array size</param>
        /// <param name="LastGain">Last normalizer gain (a reserved float with a default of 1 to always pass to this function)</param>
        void Normalize(ref float[] Target, int TargetLength, ref float LastGain) {
            float Max = Math.Abs(Target[0]), AbsSample;
            for (int Sample = 1; Sample < TargetLength; ++Sample) {
                AbsSample = Math.Abs(Target[Sample]);
                if (Max < AbsSample)
                    Max = AbsSample;
            }
            if (Max * LastGain > 1) // Kick in
                LastGain = .9f / Max;
            CavernUtilities.Gain(Target, TargetLength, LastGain); // Normalize last samples
            // Release
            LastGain += Normalizer * UpdateRate / SampleRate;
            if (LimiterOnly && LastGain > 1)
                LastGain = 1;
        }

        /// <summary>The function to initially call when samples are available, to feed them to the filter.</summary>
        void Finalization() {
            if (!Paused) {
                float[] SourceBuffer = Output;
                int SourceBufferSize = Output.Length;
                if (SystemSampleRate != CachedSampleRate) { // Resample output for system sample rate
                    float[][] ChannelSplit = new float[ChannelCount][];
                    int Channel;
                    for (Channel = 0; Channel < ChannelCount; ++Channel)
                        ChannelSplit[Channel] = new float[UpdateRate];
                    int OutputSample = 0;
                    for (int Sample = 0; Sample < UpdateRate; ++Sample)
                        for (Channel = 0; Channel < ChannelCount; ++Channel)
                            ChannelSplit[Channel][Sample] = Output[OutputSample++];
                    for (Channel = 0; Channel < ChannelCount; ++Channel)
                        ChannelSplit[Channel] = AudioSource3D.Resample(ChannelSplit[Channel], UpdateRate,
                            (int)(UpdateRate * SystemSampleRate / (float)CachedSampleRate));
                    int NewUpdateRate = ChannelSplit[0].Length;
                    SourceBuffer = new float[SourceBufferSize = ChannelCount * NewUpdateRate];
                    OutputSample = 0;
                    for (int Sample = 0; Sample < NewUpdateRate; ++Sample)
                        for (Channel = 0; Channel < ChannelCount; ++Channel)
                            SourceBuffer[OutputSample++] = ChannelSplit[Channel][Sample];
                }
                int End = FilterOutput.Length;
                lock (BufferLock) {
                    int AltEnd = BufferPosition + SourceBufferSize;
                    if (End > AltEnd)
                        End = AltEnd;
                    int OutputPos = 0;
                    for (int BufferWrite = BufferPosition; BufferWrite < End; ++BufferWrite)
                        FilterOutput[BufferWrite] = SourceBuffer[OutputPos++];
                    BufferPosition = End;
                }
            } else
                FilterOutput = new float[ChannelCount * CachedSampleRate];
        }

        void Awake() {
            if (Current) {
                UnityEngine.Debug.LogError("There can be only one 3D audio listener per scene.");
                Destroy(Current);
            }
            Current = this;
            OnOutputAvailable = Finalization; // Call finalization when samples are available
            SystemSampleRate = AudioSettings.GetConfiguration().sampleRate;
            ChannelCount = 0;
            string FileName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Cavern\\Save.dat";
            if (File.Exists(FileName)) {
                string[] Save = File.ReadAllLines(FileName);
                int SavePos = 1;
                int ChannelLength = Convert.ToInt32(Save[0]);
                Channels = new Channel[ChannelLength];
                NumberFormatInfo Format = new NumberFormatInfo {
                    NumberDecimalSeparator = ","
                };
                for (int i = 0; i < ChannelLength; ++i)
                    Channels[i] = new Channel(Convert.ToSingle(Save[SavePos++], Format), Convert.ToSingle(Save[SavePos++], Format),
                        Convert.ToBoolean(Save[SavePos++]));
                _EnvironmentType = (Environments)Convert.ToInt32(Save[SavePos++], Format);
                EnvironmentSize = new Vector3(Convert.ToSingle(Save[SavePos++], Format), Convert.ToSingle(Save[SavePos++], Format),
                    Convert.ToSingle(Save[SavePos++], Format));
                HeadphoneVirtualizer = Save.Length > SavePos ? Convert.ToBoolean(Save[SavePos++]) : false; // Added: 2016.04.24.
                EnvironmentCompensation = Save.Length > SavePos ? Convert.ToBoolean(Save[SavePos++]) : false; // Added: 2017.06.18.
            }
            if (ChannelCount != Channels.Length || CachedSampleRate != SampleRate || CachedUpdateRate != UpdateRate)
                ResetFunc();
        }

        void Update() {
            // Change checks
            if (ChannelCount != Channels.Length || CachedSampleRate != SampleRate || CachedUpdateRate != UpdateRate)
                ResetFunc();
            LastPosition = transform.position;
            LastRotationInverse = Quaternion.Inverse(LastRotation = transform.rotation);
            // Timing
            long TicksNow = DateTime.Now.Ticks;
            long TimePassed = (TicksNow - LastTicks) * SampleRate + AdditionMiss;
            long Addition = TimePassed / TimeSpan.TicksPerSecond;
            AdditionMiss = TimePassed % TimeSpan.TicksPerSecond;
            Now += (int)Addition;
            LastTicks = TicksNow;
            // Don't work with wrong settings
            if (SampleRate < 44100 || UpdateRate < 16)
                return;
            int StartTime = LastTime;
            // Pre-optimization and channel volume calculation
            bool Recalculate = CompensationCache != EnvironmentCompensation; // Recalculate volumes if channel positioning or environment compensation changed
            if (CompensationCache = EnvironmentCompensation)
                for (int Channel = 0; Channel < ChannelCount; ++Channel)
                    if (ChannelCache[Channel].x != Channels[Channel].x || ChannelCache[Channel].y != Channels[Channel].y) {
                        ChannelCache[Channel] = Channels[Channel].Copy;
                        Recalculate = true;
                    }
            if (Recalculate) {
                MaxGain = 0;
                for (int Channel = 0; Channel < ChannelCount; ++Channel) {
                    if (!Channels[Channel].LFE) {
                        if (!EnvironmentCompensation)
                            ChannelGains[Channel] = 1;  // Disable this feature when not needed
                        else
                            ChannelGains[Channel] = CavernUtilities.VectorScale(Channels[Channel].SpatialPos, EnvironmentSize).magnitude *
                                Volume * 0.07071067811865475244008443621048f; // 1 / (sqrt(2) * 10)
                        if (MaxGain < ChannelGains[Channel])
                            MaxGain = ChannelGains[Channel];
                    }
                }
                if (MaxGain != 0) {
                    float VolRecip = 1 / MaxGain;
                    CavernUtilities.Gain(ChannelGains, ChannelCount, VolRecip);
                }
            }
            // Output buffer creation
            int OutputLength = ChannelCount * UpdateRate;
            if (Output.Length == OutputLength)
                Array.Clear(Output, 0, OutputLength);
            else
                Output = new float[OutputLength];
            // Source processing
            if (Now - LastTime > SampleRate) // Lag compensation
                LastTime = Now - UpdateRate;
            if (Manual)
                Now = LastTime + UpdateRate;
            // Choose processing functions
            if (AudioQuality >= QualityModes.High) {
                if (AudioQuality != QualityModes.Perfect)
                    AudioSource3D.UsedOutputFunc = !Current.StandingWaveFix ?
                        (AudioSource3D.OutputFunc)AudioSource3D.WriteOutputApproxCP : AudioSource3D.WriteFixedOutputApproxCP;
                else
                    AudioSource3D.UsedOutputFunc = !Current.StandingWaveFix ?
                        (AudioSource3D.OutputFunc)AudioSource3D.WriteOutputCP : AudioSource3D.WriteFixedOutputCP;
            } else
                AudioSource3D.UsedOutputFunc = !Current.StandingWaveFix ?
                    (AudioSource3D.OutputFunc)AudioSource3D.WriteOutput : AudioSource3D.WriteFixedOutput;
            AudioSource3D.UsedAngleMatchFunc = AudioQuality >= QualityModes.High ? // Only calculate accurate arc cosine above high quality
                (AudioSource3D.AngleMatchFunc)AudioSource3D.CalculateAngleMatches : AudioSource3D.LinearizeAngleMatches;
            if (LastTime < Now) {
                // Set up sound collection environment
                for (int Source = 0; Source < MaximumSources; ++Source)
                    SourceDistances[Source] = Range;
                PulseDelta = (Now - LastTime) /(float)SampleRate;
                LinkedListNode<AudioSource3D> Node = ActiveSources.First;
                while (Node != null) {
                    Node.Value.Precalculate();
                    Node = Node.Next;
                }
                while (LastTime < Now) {
                    DeltaTime = (float)(LastTime - StartTime) / (Now - StartTime);
                    if (!Paused || Manual) {
                        // Collect audio data from sources
                        Task<float[]>[] Tasks = new Task<float[]>[ActiveSources.Count];
                        int TaskCount = 0;
                        Node = ActiveSources.First;
                        while (Node != null) {
                            Node.Value.Precollect();
                            LinkedListNode<AudioSource3D> SourceNode = Node;
                            Tasks[TaskCount++] = Task.Run(() => SourceNode.Value.Collect());
                            Node = Node.Next;
                        }
                        Task.WaitAll(Tasks);
                        // Mix sources to output
                        Array.Clear(Output, 0, OutputLength);
                        for (int TaskPos = 0; TaskPos < TaskCount; ++TaskPos)
                            if (Tasks[TaskPos].Result != null)
                                CavernUtilities.Mix(Tasks[TaskPos].Result, Output, OutputLength);
                        // Volume, distance compensation, and subwoofers' lowpass
                        for (int Channel = 0; Channel < ChannelCount; ++Channel) {
                            if (Channels[Channel].LFE) {
                                if (!DirectLFE)
                                    Lowpasses[Channel].Process(Output, Channel, ChannelCount);
                                CavernUtilities.Gain(Output, UpdateRate, LFEVolume * Volume, Channel, ChannelCount); // LFE Volume
                            } else
                                CavernUtilities.Gain(Output, UpdateRate, ChannelGains[Channel] * Volume, Channel, ChannelCount);
                        }
                        if (Normalizer != 0) // Normalize
                            Normalize(ref Output, OutputLength, ref Normalization);
                    }
                    // Finalize
                    OnOutputAvailable();
                    LastTime += UpdateRate;
                }
                Manual = false;
            }
        }

        // ------------------------------------------------------------------
        // Filter output
        // ------------------------------------------------------------------
        /// <summary>Filter buffer position, samples currently cached for output.</summary>
        static int BufferPosition = 0;
        /// <summary>Samples to play with the filter.</summary>
        static float[] FilterOutput;
        /// <summary>Lock for the <see cref="BufferPosition"/>, which is set in multiple threads.</summary>
        static object BufferLock = new object();
        /// <summary>Filter normalizer gain.</summary>
        static float FilterNormalizer = 1;
        /// <summary>Cached system sample rate.</summary>
        static int SystemSampleRate;

        /// <summary>Output Cavern's generated audio as a filter.</summary>
        /// <param name="UnityBuffer">Output buffer</param>
        /// <param name="UnityChannels">Output channel count</param>
        void OnAudioFilterRead(float[] UnityBuffer, int UnityChannels) {
            if (BufferPosition == 0)
                return;
            int Samples = UnityBuffer.Length / UnityChannels;
            int End = BufferPosition;
            int AltEnd = Samples * ChannelCount;
            if (End > AltEnd)
                End = AltEnd;
            int BufferPos = 0, DataPos = 0;
            // Output audio
            for (BufferPos = 0; BufferPos < End;) {
                if (UnityChannels <= 4) { // For non-surround setups, downmix properly
                    for (int Channel = 0; Channel < 2; ++Channel)
                        UnityBuffer[DataPos + Channel] += FilterOutput[BufferPos++];
                    int MaxMonoChannel = ChannelCount;
                    if (MaxMonoChannel > 4)
                        MaxMonoChannel = 4;
                    for (int Channel = 2; Channel < MaxMonoChannel; ++Channel) {
                        float Sample = FilterOutput[BufferPos++];
                        UnityBuffer[DataPos] += Sample;
                        UnityBuffer[DataPos + 1] += Sample;
                    }
                    for (int Channel = 4; Channel < ChannelCount; ++Channel)
                        UnityBuffer[DataPos + Channel % UnityChannels] += FilterOutput[BufferPos++];
                } else for (int Channel = 0; Channel < ChannelCount; ++Channel)
                        UnityBuffer[DataPos + Channel % UnityChannels] += FilterOutput[BufferPos++];
                DataPos += UnityChannels;
            }
            if (Normalizer != 0) // Normalize
                Normalize(ref UnityBuffer, UnityBuffer.Length, ref FilterNormalizer);
            // Remove used samples
            DataPos = 0;
            lock (BufferLock) {
                for (; BufferPos < BufferPosition; ++BufferPos)
                    FilterOutput[DataPos++] = FilterOutput[BufferPos];
                int MaxLatency = ChannelCount * CachedSampleRate / DelayTarget;
                if (BufferPosition < MaxLatency)
                    BufferPosition -= End;
                else
                    BufferPosition = 0;
            }
        }
    }
}