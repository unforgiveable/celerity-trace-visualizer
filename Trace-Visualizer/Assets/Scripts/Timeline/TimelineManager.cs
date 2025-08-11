using celerity.visualizer.interaction;
using celerity.visualizer.tracedata;
using ProtoBuf;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace celerity.visualizer.timeline
{
	/// <summary>
	/// The main entry point.
	/// Handles the initialization of all subsystems with a given trace.
	/// Also acts as a container for the Trace data structure.
	/// </summary>
	public class TimelineManager : MonoBehaviour
	{
		public static TimelineManager Instance;

		const bool DEBUG = true;

		/// <summary>
		/// The currently loaded trace. Is set to null if no trace is currently loaded.
		/// </summary>
		public Trace CurrentTrace { get; private set; }

		private void Awake()
		{
			Instance = this;

			CurrentTrace = null;
		}

		private void Start()
		{
			// start with no trace loaded.

			if (!GlobalSettings.BENCHMARK)
			{
				InitSystem(null);
				return;
			}


			// -- debug code below --

			//GlobalSettings.LAYOUT_POLICY = NodeLayoutPolicy.Circular;

			//LoadTimeline(@"./traces/matmul_single_run_4_6512031.ctrc");
			LoadTimeline(@"./traces/mmmul_single_run_8_6512065.ctrc");
			//LoadTimeline(@"./traces/wavesim_single_run_16_6489647.ctrc");

			//SectionsManager.Instance.CreateIdleSections(CurrentTrace, GlobalSettings.LAYOUT_POLICY);
			//_ = NodeDependenciesManager.Instance.ShowCriticalPath();

			//Command comm = CurrentTrace.Commands[52];
			//Node n = NodeManager.Instance.Nodes[comm];
			//interaction.WristMenuModes.Instance.SelectNode(n);
			//NodeDependenciesManager.Instance.ShowNodeDependenciesRecursive(n, 1);



			return;

#pragma warning disable CS0162 // Unreachable code detected
			// temporary testing setup for loading a trace file from the "/trace/" folder

			string folderName = @".\trace\";

			Debug.Log("Checking in folder " + folderName);

			string fileName = Directory.EnumerateFiles(folderName).FirstOrDefault();

			if (fileName == null)
			{
				Debug.LogError("No trace found.");
				return;
			}

			LoadTimeline(fileName);

			// list tab test
			//Command comm = CurrentTrace.Commands[10];
			//Node n = NodeManager.Instance.Nodes[comm];
			//interaction.WristMenuModes.Instance.SelectNode(n);
			//interaction.WristMenuManager.Instance.ShowListTab();

			//NodeManager.Instance.ShowNodesByType(CommandType.Task | CommandType.Horizon | CommandType.Sync);
			//NodeManager.Instance.ShowNodesInVicinity(2800000, 500000);
			//var nodes = NodeDependenciesManager.Instance.ShowCriticalPath();
			//foreach (var node in nodes)
			//{
			//	NodeVisualsManager.Instance.HighlightNode(node);
			//}


			SectionsManager.Instance.CreateIdleSections(CurrentTrace, NodeLayoutPolicy.LinearMixed);
#pragma warning restore CS0162 // Unreachable code detected
		}

		/// <summary>
		/// Loads the trace file and sets up the scene to visualize it.
		/// Gets invoked from the trace selection UI.
		/// </summary>
		/// <param name="fileName"></param>
		public void LoadTimeline(string fileName)
		{
			Trace trace;

			try
			{
				trace = LoadTrace(fileName);
			}
			catch (System.Exception ex)
			{
				Debug.LogError("Trace loading failed!");
				Debug.LogError($"{ex}, {ex.GetType().FullName}, {ex.Message}, {ex.Source}, {ex.StackTrace}");
				return;
			}

			if (trace == null)
			{
				Debug.LogError("Retrieved trace is null, aborting.");
				return;
			}

			InitSystem(trace);

			List<Command> commands = trace.Commands.Values.ToList();

			if (DEBUG)
				Debug.Log("Loaded trace with " + trace.Tasks.Count + " tasks, " + trace.Commands.Count + " commands, " + trace.Buffers.Count + " buffers");

			// create command nodes
			NodeManager nodeManager = NodeManager.Instance;
			
			if (GlobalSettings.BENCHMARK)
				PerformanceLogger.MeasureExecTime("create command nodes", () => nodeManager.CreateCommandNodes(commands, trace.MinStartTime, GlobalSettings.LAYOUT_POLICY));
			else
				nodeManager.CreateCommandNodes(commands, trace.MinStartTime, GlobalSettings.LAYOUT_POLICY);

			// create task nodes
			TaskNodeManager taskNodeManager = TaskNodeManager.Instance;
			taskNodeManager.CreateAllTaskNodes(trace.Tasks.Values);

			// apply default mode selection
			WristMenuModes.Instance.ApplyDefaultModeSelection();

		}

		/// <summary>
		/// Loads a trace file, invokes the trace converter with the raw protobuf data, and returns the resulting trace.
		/// Throws an ArgumentException if the file loading failed.
		/// </summary>
		/// <param name="fileName">The path of the trace file.</param>
		private Trace LoadTrace(string fileName)
		{
			try
			{
				using var file = File.OpenRead(fileName);
				List<celerity.visualizer.tracedata.rawtrace.TracePayload> tracePayloadsList;

				if (GlobalSettings.BENCHMARK)
				{
					tracePayloadsList = PerformanceLogger.MeasureExecTime<List<celerity.visualizer.tracedata.rawtrace.TracePayload>>("trace deserialization", () =>
					{
						var tracePayloads = Serializer.DeserializeItems<celerity.visualizer.tracedata.rawtrace.TracePayload>(file, PrefixStyle.Fixed32, -1);
						List<celerity.visualizer.tracedata.rawtrace.TracePayload> tracePayloadsList = tracePayloads.ToList();
						return tracePayloadsList;
					});
				}
				else
				{
					var tracePayloads = Serializer.DeserializeItems<celerity.visualizer.tracedata.rawtrace.TracePayload>(file, PrefixStyle.Fixed32, -1);
					tracePayloadsList = tracePayloads.ToList();
				}

				Trace convertedTrace;
				if (GlobalSettings.BENCHMARK)
					convertedTrace = PerformanceLogger.MeasureExecTime<Trace>("trace conversion", () => TraceConverter.ConvertTrace(tracePayloadsList));
				else
					convertedTrace = TraceConverter.ConvertTrace(tracePayloadsList);

				return convertedTrace;
			}
			catch (IOException e)
			{
				Debug.LogError(e.Message);
				throw new System.ArgumentException("Couldn't load trace file");
			}
		}

		/// <summary>
		/// Initializes the system with the given trace. Propagates the init to all sub-systems.
		/// </summary>
		/// <param name="trace">The loaded trace file. If <see langword="null"/> the system is initialized as an empty timeline.</param>
		private void InitSystem(Trace trace)
		{
			//if (trace == null)
			//{
			//	Debug.LogError("Init called with trace = null");
			//	return;
			//}

			CurrentTrace = trace;

			NodeManager.Instance.Init();
			NodeVisualsManager.Instance.Init();
			NodeDetailsManager.Instance.Init();
			NodeDependenciesManager.Instance.Init();
			TaskNodeManager.Instance.Init();
			WristMenuManager.Instance.Init();
			SectionsManager.Instance.Init();
			TraceMover.Instance.Init();
			NodeIdLabelManager.Instance.Init(GlobalSettings.LAYOUT_POLICY, CurrentTrace != null ? CurrentTrace.ComputeNodes.Count : 0);
			WristMenuModes.Instance.Init();

			if (trace == null)
			{
				TraceMover.Instance.SetMovement(false);
			}
			else
			{
				TraceMover.Instance.SetMovement(true);
			}
		}

	}
}
