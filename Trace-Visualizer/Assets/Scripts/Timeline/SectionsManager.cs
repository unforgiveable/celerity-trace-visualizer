using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using celerity.visualizer.tracedata;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine.UIElements;

namespace celerity.visualizer.timeline
{
	/// <summary>
	/// Types of sections, determines how the sections are visualized.
	/// </summary>
	public enum SectionType
	{
		Idle,
	}

	/// <summary>
	/// Manager for displaying sections on the timeline.
	/// Currently supports creating Task / Command idle sections, but is built to be re-usable for more section types.
	/// </summary>
	public class SectionsManager : MonoBehaviour
	{
		public struct SecData
		{
			public ulong ComputeNodeId;
			public ulong StartTime;
			public ulong EndTime;
		}

		public static SectionsManager Instance { get; private set; }

		[SerializeField] Transform SectionsParent;
		[SerializeField] Material MatSectionIdle;

		private GameObject _sectionPrefab;
		private List<SecData> _currentSections;

		private const float SectionHeight = 0.5f;
		private const float SectionYOffset = 0.3f;
		private const ulong DefaultMinLength = 1000;

		private void Awake()
		{
			Instance = this;

			_sectionPrefab = Resources.Load<GameObject>("Prefabs/Section");
			_currentSections = null;
		}

		public void Init()
		{
			RemoveAllSections();
		}

		/// <summary>
		/// Creates sections for all parts of the trace where no command is active.
		/// </summary>
		/// <param name="trace">The current trace.</param>
		/// <param name="layoutPolicy">The currently used node layout policy. Used to match the position of the sections with the nodes.</param>
		/// <returns>A list of all idle sections with normalized times.</returns>
		public List<SecData> CreateIdleSections(Trace trace, NodeLayoutPolicy layoutPolicy, ulong minLength = DefaultMinLength)
		{
			// Remove any previously shown sections
			if (_currentSections != null)
			{
				RemoveAllSections();
			}

			List<SecData> sections;
			if (GlobalSettings.BENCHMARK)
			{
				sections = PerformanceLogger.MeasureExecTime<List<SecData>>("idle sections computation", () =>
				{
					// get trace commands grouped by compute node id and sorted by start time ascending
					var orderedCommands = trace.Commands.Values.GroupBy(x => x.ComputeNode.Id).ToDictionary(x => x.Key, x => x.OrderBy(x => x.StartTime).ToList());
					return GetGapsInCommands(orderedCommands, minLength, trace.MinStartTime);
				});
			}
			else
			{
				// get trace commands grouped by compute node id and sorted by start time ascending
				var orderedCommands = trace.Commands.Values.GroupBy(x => x.ComputeNode.Id).ToDictionary(x => x.Key, x => x.OrderBy(x => x.StartTime).ToList());
				sections = GetGapsInCommands(orderedCommands, minLength, trace.MinStartTime);
			}

			//Debug.Log("Found " + sections.Count + " idle sections");

			// create sections for each identified idle section
			foreach (var sec in sections)
			{
				//Debug.Log("Idle section for node " + sec.NodeId + " from " + sec.StartTime + " to " + sec.EndTime);
				CreateSection(sec, layoutPolicy, SectionType.Idle);
			}

			_currentSections = sections;
			return sections;
		}

		/// <summary>
		/// Creates sections for all parts of the trace where no TASK command is active.
		/// </summary>
		/// <param name="trace">The current trace.</param>
		/// <param name="layoutPolicy">The currently used node layout policy. Used to match the position of the sections with the nodes.</param>
		/// <returns>A list of all idle sections with normalized times.</returns>
		public List<SecData> CreateTaskIdleSections(Trace trace, NodeLayoutPolicy layoutPolicy, ulong minLength = DefaultMinLength)
		{
			// Remove any previously shown sections
			if (_currentSections != null)
			{
				RemoveAllSections();
			}

			// get trace TASK commands grouped by compute node id and sorted by start time ascending
			var orderedCommands = trace.Commands.Values.Where(x => x.CommandType == CommandType.Task).GroupBy(x => x.ComputeNode.Id).ToDictionary(x => x.Key, x => x.OrderBy(x => x.StartTime).ToList());

			List<SecData> sections = GetGapsInCommands(orderedCommands, minLength, trace.MinStartTime);

			// create sections for each identified idle section
			foreach (var sec in sections)
			{
				CreateSection(sec, layoutPolicy, SectionType.Idle);
			}

			_currentSections = sections;
			return sections;
		}

		/// <summary>
		/// Computes the sections of gaps in the list of ordered commands for each compute node.
		/// </summary>
		/// <param name="orderedCommands">Dictionary with a list of commands ordered by ascending start time for each compute node (=dictionary key).</param>
		/// <param name="minLength">Minimum length for a gap to be considered.</param>
		/// <param name="traceMinStartTime">Minimum start time of the trace. Used for normalizing the section start/end times.</param>
		/// <returns>A list of all found sections.</returns>
		private List<SecData> GetGapsInCommands(Dictionary<ulong, List<Command>> orderedCommands, ulong minLength, ulong traceMinStartTime)
		{
			List<SecData> sections = new();

			foreach (var kv in orderedCommands)
			{
				if (kv.Value.Count == 0)
				{
					Debug.LogError("Idle section computation has no commands on node " + kv.Key);
					continue;
				}

				ulong nodeId = kv.Key;
				ulong lastEnd = kv.Value[0].EndTime;
				for (int i = 1; i < kv.Value.Count(); i++)
				{
					// check if difference between current command start time and maximum found end time is more than threshold
					if (kv.Value[i].StartTime > lastEnd &&
						(kv.Value[i].StartTime - lastEnd) >= minLength)
					{
						sections.Add(new SecData()
						{
							ComputeNodeId = nodeId,
							StartTime = lastEnd - traceMinStartTime,
							EndTime = kv.Value[i].StartTime - traceMinStartTime
						});
					}

					// keep the maximum found end time from all commands so far
					if (kv.Value[i].EndTime > lastEnd)
						lastEnd = kv.Value[i].EndTime;
				}
			}

			return sections;
		}

		/// <summary>
		/// Removes all currently shown sections.
		/// </summary>
		public void RemoveAllSections()
		{
			for (int i = SectionsParent.childCount - 1; i >= 0; i--)
			{
				Destroy(SectionsParent.GetChild(i).gameObject);
			}

			_currentSections = null;
		}

		/// <summary>
		/// Creates a new section between the specified times.
		/// </summary>
		/// <param name="startTime">Start time of the section in normalized time.</param>
		/// <param name="endTime">End time of the section in normalized time.</param>
		/// <param name="layoutPolicy">The layout policy to use for the sections. Should be identical to the one currently used by the <see cref="NodePositionManager"/>.</param>
		/// <param name="sectionType">Type of the section to display. Influences the visuals of the section object.</param>
		private void CreateSection(SecData secData, NodeLayoutPolicy layoutPolicy, SectionType sectionType)
		{
			if (secData.StartTime >= secData.EndTime)
			{
				Debug.LogWarning("Invalid times for section, skipping.");
				return;
			}


			GameObject newSection = Instantiate(_sectionPrefab);
			newSection.transform.SetParent(SectionsParent, false);

			// configure Section component
			Section section = newSection.GetComponent<Section>();
			section.ComputeNodeId = secData.ComputeNodeId;
			section.StartTime = secData.StartTime;
			section.EndTime = secData.EndTime;
			section.Type = sectionType;

			// set visuals depending on type
			switch (sectionType)
			{
				case SectionType.Idle:
					newSection.GetComponent<MeshRenderer>().sharedMaterial = MatSectionIdle;
					break;
				default:
					Debug.LogError("Section type not yet implemented!");
					break;
			}

			// position section depending on layout policy
			switch (layoutPolicy)
			{
				case NodeLayoutPolicy.LinearMixed:
					PositionSectionLinearMixed(section);
					break;

				case NodeLayoutPolicy.Circular:
					PositionSectionCircular(section);
					break;

				default:
					Debug.LogError("Layout policy not yet implemented!");
					break;
			}
		}

		/// <summary>
		/// Positions the section according to the linear mixed layout policy. Uses the limits and values specified in the <see cref="NodePositionManager"/> to position it correctly.
		/// </summary>
		/// <param name="section">The section to position.</param>
		private void PositionSectionLinearMixed(Section section)
		{
			// size section to match the start/end times
			float scaleX = (section.EndTime - section.StartTime) * NodePositionManager.TimeToDistanceScaleFactor;

			section.transform.localScale = new Vector3(scaleX, SectionHeight, NodePositionManager.NodeWidth);


			// position section to start at its start time
			float offsetToStart = section.StartTime * NodePositionManager.TimeToDistanceScaleFactor;
			float offsetX = offsetToStart + (scaleX / 2f);

			// offset for different compute node lanes
			float offsetZ = section.ComputeNodeId * NodePositionManager.ComputeNodeLaneWidth;

			section.transform.localPosition = new Vector3(offsetX, SectionYOffset, offsetZ);
		}

		/// <summary>
		/// Positions the section according to the circular layout policy. Uses the limits and values specified in the <see cref="NodePositionManager"/> to position it correctly.
		/// </summary>
		/// <param name="section">The section to position.</param>
		private void PositionSectionCircular(Section section)
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;
			int numComputeNodes = _trace.ComputeNodes.Count;
			float circleRadius = NodePositionManager.CalculateCircleRadius(numComputeNodes);

			// size section to match the start/end times
			float scaleY = (section.EndTime - section.StartTime) * NodePositionManager.TimeToDistanceScaleFactor;

			section.transform.localScale = new Vector3(SectionHeight, scaleY, NodePositionManager.NodeWidth);


			// position section to start at its start time
			float offsetToStart = section.StartTime * NodePositionManager.TimeToDistanceScaleFactor;
			float offsetY = offsetToStart + (scaleY / 2f);
			float offsetX = SectionYOffset + circleRadius;

			// rotate around y-axis with sections parent as pivot to compute node radial position
			float rotY = (360f / numComputeNodes) * section.ComputeNodeId;

			section.transform.localPosition = new Vector3(offsetX, offsetY, 0);

			section.transform.RotateAround(SectionsParent.position, Vector3.up, rotY);
		}

	}
}
