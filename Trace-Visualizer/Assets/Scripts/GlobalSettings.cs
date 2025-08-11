using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using celerity.visualizer.timeline;


namespace celerity.visualizer
{
    /// <summary>
    /// Class holding global settings for the application. Settings get modified by the SettingsManager (tba).
    /// </summary>
    public class GlobalSettings
    {
        public const float TimestampToSecondsConversionFactor = 1000000f; // 1 mil ys = 1 sec

		public static NodeLayoutPolicy LAYOUT_POLICY = NodeLayoutPolicy.LinearMixed;

        public static readonly bool BENCHMARK = false;
    }
}
