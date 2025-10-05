using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Globalization;

namespace celerity.visualizer
{
	public class PerformanceLogger : MonoBehaviour
	{
		private const bool LOG_DATA = false;
		private readonly string logFilesFolder = @"./performance_logs/";
		private readonly string logFileName = "frametimes_" + DateTime.Now.ToString("HH-mm-ss") + ".csv";


		private readonly List<float> frameTimes = new();
		

		void Awake()
		{
			// create logs folder if it doesn't exist
			Directory.CreateDirectory(logFilesFolder);
		}

		void Update()
		{
			if (LOG_DATA)
			{
#pragma warning disable CS0162 // Unreachable code detected
				frameTimes.Add(Time.deltaTime);
#pragma warning restore CS0162 // Unreachable code detected
			}
		}

		// called when the application is quit or the play mode is stopped
		private void OnDestroy()
		{
			if (LOG_DATA)
			{
#pragma warning disable CS0162 // Unreachable code detected
				using var file = File.CreateText(logFilesFolder + logFileName);
				file.WriteLine(String.Join(",", frameTimes.ToArray().Select(x => x.ToString(CultureInfo.InvariantCulture))));
#pragma warning restore CS0162 // Unreachable code detected
			}
		}

		
		/// <summary>
		/// Wrapper for measuring the execution time of a given function.
		/// Prints the measured time out in the console.
		/// </summary>
		/// <typeparam name="T">Return type of the function to be measured.</typeparam>
		/// <param name="identifier">String name of the function for identification.</param>
		/// <param name="func">Delegate function to execute. Recommended use is with a lambda function.</param>
		/// <returns>The return value of <paramref name="func"/>.</returns>
		public static T MeasureExecTime<T>(string identifier, Func<T> func)
		{
			// invoke C# GC in order to prevent it running during the measurement
			System.GC.Collect();

			var watch = System.Diagnostics.Stopwatch.StartNew();
			T res = func();
			watch.Stop();

			var elapsedMs = watch.Elapsed.TotalMilliseconds;
			string timeString = string.Format("{0:0.00}ms", elapsedMs);
			Debug.LogWarning("### Exec Time of " + identifier + ": " + timeString);
			return res;
		}

		/// <summary>
		/// Wrapper for measuring the execution time of a given function.
		/// Prints the measured time out in the console.
		/// </summary>
		/// <param name="identifier">String name of the function for identification.</param>
		/// <param name="func">Delegate function to execute. Recommended use is with a lambda function.</param>
		public static void MeasureExecTime(string identifier, Action func)
		{
			// invoke C# GC in order to prevent it running during the measurement
			System.GC.Collect();

			var watch = System.Diagnostics.Stopwatch.StartNew();
			func();
			watch.Stop();

			var elapsedMs = watch.Elapsed.TotalMilliseconds;
			string timeString = string.Format("{0:0.00}ms", elapsedMs);
			Debug.LogWarning("### Exec Time of " + identifier + ": " + timeString);
			return;
		}

	}
}