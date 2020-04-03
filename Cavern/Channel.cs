﻿using System;
using System.Runtime.CompilerServices;

using Cavern.Utilities;

namespace Cavern {
    /// <summary>Spatially positioned audio output channel.</summary>
    public sealed class Channel : IEquatable<Channel> {
        /// <summary>Rotation around the vertical axis in degrees: elevation.</summary>
        public float X { get; private set; }
        /// <summary>Rotation around the horizontal axis in degrees: azimuth.</summary>
        public float Y { get; private set; }
        /// <summary>True for channels carrying only Low Frequency Effects.</summary>
        public bool LFE {
            get => lowFrequency;
            set {
                lowFrequency = value;
                SymmetryCheck();
            }
        }
        /// <summary>This channel is part of the screen channels, and should be behind the screen in a theatre.</summary>
        public bool IsScreenChannel => !lowFrequency && Math.Abs(X) < 25 && Math.Abs(Y) <= 45;
        /// <summary>Position on a sphere with the radius of 1.</summary>
        public Vector SphericalPos { get; private set; }
        /// <summary>Position on a cube with a side length of 2.</summary>
        public Vector CubicalPos { get; private set; }
        /// <summary>Position in space. <see cref="SphericalPos"/> in Studio environments, <see cref="CubicalPos"/> otherwise.</summary>
        public Vector SpatialPos { get; private set; }
        /// <summary>The distance from the listener, relative to the center channel. Magnitude of <see cref="SpatialPos"/>.</summary>
        public float Distance { get; private set; }

        /// <summary>True for channels carrying only Low Frequency Effects.</summary>
        bool lowFrequency = false;

        /// <summary>Constructor for a channel with given rotation values.</summary>
        /// <param name="x">Rotation around the vertical axis in degrees: elevation</param>
        /// <param name="y">Rotation around the horizontal axis in degrees: azimuth</param>
        public Channel(float x, float y) => SetPosition(x, y);

        /// <summary>Constructor for a channel with given rotation values and LFE status.</summary>
        /// <param name="x">Rotation around the vertical axis in degrees: elevation</param>
        /// <param name="y">Rotation around the horizontal axis in degrees: azimuth</param>
        /// <param name="LFE">True for channels carrying only Low Frequency Effects</param>
        public Channel(float x, float y, bool LFE) {
            lowFrequency = LFE;
            SetPosition(x, y);
        }

        /// <summary>Move this channel to a new position.</summary>
        /// <param name="x">Rotation around the vertical axis in degrees: elevation</param>
        /// <param name="y">Rotation around the horizontal axis in degrees: azimuth</param>
        public void Move(float x, float y) {
            SetPosition(x, y);
            SymmetryCheck();
        }

        /// <summary>Rotate this channel.</summary>
        /// <param name="x">Rotation around the vertical axis in degrees: height</param>
        /// <param name="y">Rotation around the horizontal axis in degrees</param>
        public void Rotate(float x, float y) => Move(X + x, Y + y);

        /// <summary>Set the position of the channel and do all neccessary processing.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetPosition(float x, float y) {
            X = x;
            Y = y;
            Recalculate();
        }

        /// <summary>Recalculates properties.</summary>
        void Recalculate() {
            float xRad = X * Vector.Deg2Rad, yRad = Y * Vector.Deg2Rad, sinX = (float)Math.Sin(xRad), cosX = (float)Math.Cos(xRad),
                sinY = (float)Math.Sin(yRad), cosY = (float)Math.Cos(yRad);
            SphericalPos = new Vector(sinY * cosX, -sinX, cosY * cosX);
            if (Math.Abs(sinY) > Math.Abs(cosY))
                sinY = Math.Sign(sinY) * Vector.Sqrt2p2;
            else
                cosY = Math.Sign(cosY) * Vector.Sqrt2p2;
            sinY /= Vector.Sqrt2p2;
            cosY /= Vector.Sqrt2p2;
            if (Math.Abs(sinX) >= Vector.Sqrt2p2) {
                sinX = Math.Sign(sinX) * Vector.Sqrt2p2;
                cosX /= Vector.Sqrt2p2;
                sinY *= cosX;
                cosY *= cosX;
            }
            sinX /= Vector.Sqrt2p2;
            CubicalPos = new Vector(sinY, -sinX, cosY);
            CubicalPos = Vector.PlaceInCube(new Vector(X, Y));
            if (Listener.EnvironmentType != Environments.Studio)
                SpatialPos = CubicalPos;
            else
                SpatialPos = SphericalPos;
            Distance = SpatialPos.Magnitude;
        }

        /// <summary>Recalculates symmetry when a channel's position is changed.</summary>
        internal static void SymmetryCheck() {
            if (Listener.Channels == null)
                return;
            Listener.IsSymmetric = true;
            int channelCount = Listener.Channels.Length;
            if (channelCount % 2 == 1) { // If there is an unpaired channel, it must be on the center circle
                --channelCount;
                Listener.IsSymmetric = Listener.Channels[channelCount].Y % 180 == 0;
            }
            for (int i = 0; i < channelCount; i += 2) {
                int Next = i + 1;
                if (Next != channelCount && Listener.Channels[i] != null && Listener.Channels[Next] != null)
                    Listener.IsSymmetric &=
                        Listener.Channels[i].lowFrequency ? Listener.Channels[Next].Y % 180 == 0 || Listener.Channels[Next].lowFrequency :
                        Listener.Channels[Next].lowFrequency ? Listener.Channels[i].Y % 180 == 0 || Listener.Channels[i].lowFrequency :
                        (Listener.Channels[i].X == Listener.Channels[Next].X ? Listener.Channels[i].Y % 180 ==
                        -Listener.Channels[Next].Y % 180 : Listener.Channels[i].Y % 180 == 0 && Listener.Channels[Next].Y % 180 == 0);
            }
        }

        /// <summary>Check if two channels are the same.</summary>
        public bool Equals(Channel other) => X == other.X && Y == other.Y && lowFrequency == other.lowFrequency;
    }
}