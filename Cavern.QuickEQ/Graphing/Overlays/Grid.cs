﻿namespace Cavern.QuickEQ.Graphing.Overlays {
    /// <summary>
    /// Draws a grid over the graph.
    /// </summary>
    public class Grid : Frame {
        /// <summary>
        /// Inner line stroke width.
        /// </summary>
        readonly int gridWidth;

        /// <summary>
        /// Number of columns drawn, including the frame lines.
        /// </summary>
        readonly int xSteps;

        /// <summary>
        /// Number of rows drawn, including the frame lines.
        /// </summary>
        readonly int ySteps;

        /// <summary>
        /// Draws a grid over the graph.
        /// </summary>
        /// <param name="borderWidth">Border line stroke width</param>
        /// <param name="gridWidth">Inner line stroke width</param>
        /// <param name="color">RGBA color of the line</param>
        /// <param name="xSteps">Number of columns drawn, including the frame lines</param>
        /// <param name="ySteps">Number of rows drawn, including the frame lines</param>
        public Grid(int borderWidth, int gridWidth, uint color, int xSteps, int ySteps) : base(borderWidth, color) {
            this.gridWidth = gridWidth;
            this.xSteps = xSteps;
            this.ySteps = ySteps;
        }

        /// <summary>
        /// Adds the overlay to a graph.
        /// </summary>
        public override void DrawOn(GraphRenderer target) {
            base.DrawOn(target);
            uint[] pixels = target.Pixels;
            int xGap = target.Width / xSteps,
                yGap = target.Height / ySteps,
                yMax = target.Height - width;
            for (int x = 1; x < xSteps; x++) {
                int xPos = x * xGap;
                for (int y = width; y < yMax; y++) {
                    int start = y * target.Width + xPos;
                    for (int w = 0; w < gridWidth; w++) {
                        pixels[start + w] = color;
                    }
                }
            }

            int xMax = target.Width - width;
            for (int y = 1; y < ySteps; y++) {
                int yPos = y * yGap;
                for (int w = 0; w < gridWidth; w++) {
                    int start = (yPos + w) * target.Width;
                    for (int x = width; x < xMax; x++) {
                        pixels[start + x] = color;
                    }
                }
            }
        }
    }
}