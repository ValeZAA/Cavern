﻿using UnityEngine;
using Cavern.Helpers;

namespace Cavern.Debug {
    [AddComponentMenu("Audio/Debug/Seat Adaptation Demo")]
    public class SeatAdaptationDemo : WindowBase {
        [Tooltip("The Seat Adaptation component to configure.")]
        public SeatAdaptation Adaptor;

        Texture2D Gray;

        protected override void Setup() {
            Width = Adaptor.Columns * 20 + 5;
            Height = Adaptor.Rows * 20 + 25;
            Title = "Seat Adaptation";
            Gray = new Texture2D(1, 1);
            Gray.SetPixel(0, 0, new Color(0, 0, 0, .5f));
            Gray.Apply();
        }

        protected override void Draw(int ID) {
            GUI.DrawTexture(new Rect(5, 20, Adaptor.Columns * 20 - 5, 2), Gray);
            for (int Z = 0; Z < Adaptor.Rows; ++Z)
                for (int X = 0; X < Adaptor.Columns; ++X)
                    Adaptor.SeatsOccupied[Z, X] = GUI.Toggle(new Rect(X * 20 + 5, Z * 20 + 22, 15, 15), Adaptor.SeatsOccupied[Z, X], string.Empty);
            GUI.DragWindow();
        }
    }
}
