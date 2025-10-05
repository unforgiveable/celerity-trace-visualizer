using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using celerity.visualizer.tracedata;
using System;

namespace celerity.visualizer.timeline
{
	/// <summary>
	/// System for positioning command nodes in the scene.
	/// Accounts for layoutPolicy, lane number, layers, etc.
	/// </summary>
	public class NodePositionManager
	{
		public const float ComputeNodeLaneWidth = 0.6f;
		public const float TimeToDistanceScaleFactor = 5f / GlobalSettings.TimestampToSecondsConversionFactor; //5m in-scene for 1 sec.
		public const float NodeWidth = 0.2f;

		private const float HeightOffset = 0.5f;
		private const float NodeMinLength = 0.02f;
		private const float NodeVerticalSpacing = 0.05f;

		private const float NodeMinSequentialSpacing = 0.05f; // 0.012f;
		private const float DistanceToTimeScaleFactor = 1f / TimeToDistanceScaleFactor;
		private const float NodeMinSequentialTimeSpacing = NodeMinSequentialSpacing * DistanceToTimeScaleFactor;

		private const float MinimumCircleRadius = 2f;

		/// <summary>
		/// Keys are compute node ids, values are lists of lists where the index in the top list is the layer number.
		/// Nodes in each layer list are ordered in ascending order by StartTime.
		/// </summary>
		private readonly Dictionary<ulong, List<List<Node>>> _layersDict;

		public NodePositionManager()
		{
			Trace trace = TimelineManager.Instance.CurrentTrace;

			_layersDict = new Dictionary<ulong, List<List<Node>>>();
			foreach (ulong nodeId in trace.ComputeNodes.Keys)
			{
				_layersDict.Add(nodeId, new List<List<Node>>());
			}
		}

		/// <summary>
		/// Returns a dictionary of lists, where each list contains the nodes of a compute node in ascending order by StartTime.
		/// The key in the dictionary corresponds to the compute node id.
		/// </summary>
		[Obsolete] //TODO remove?
		public Dictionary<ulong, List<Node>> GetAllNodesAsOrderedLists()
		{
			Dictionary<ulong, List<Node>> result = new();
			foreach (var nodeId in _layersDict.Keys)
			{
				List<List<Node>> layers = _layersDict[nodeId];
				List<Node> nodes = new();

				int[] idxs = new int[layers.Count];
				for (int i = 0; i < idxs.Length; i++) idxs[i] = 0;

				while (true)
				{
					ulong minStartTime = ulong.MaxValue;
					int minIdx = -1;

					// find which layer's next node has the minimum start time
					for (int i = 0; i < layers.Count; i++)
					{
						if (idxs[i] < layers[i].Count && layers[i][idxs[i]].Command.StartTime < minStartTime)
						{
							minStartTime = layers[i][idxs[i]].Command.StartTime;
							minIdx = i;
						}
					}

					// no valid element found - we're done
					if (minIdx == -1) break;

					// next element is in layer minIdx - add to nodes list
					nodes.Add(layers[minIdx][idxs[minIdx]]);
					idxs[minIdx]++;
				}

				result.Add(nodeId, nodes);
			}

			return result;
		}

		/// <summary>
		/// Positions the node in the scene based on the given layoutPolicy.
		/// </summary>
		public void PositionNode(Node node, NodeLayoutPolicy layoutPolicy)
		{
			switch (layoutPolicy)
			{
				case NodeLayoutPolicy.LinearMixed:
					PositionNodeLinearMixed(node);
					break;
				case NodeLayoutPolicy.Circular:
					PositionNodeCircular(node);
					break;
				default:
					Debug.LogError("Layout policy not yet implemented!");
					break;
			}
		}

		private void PositionNodeLinearMixed(Node node)
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;

			GameObject visualsObject = node.GetVisualsParent().gameObject;
			int layer = ComputeLayer(node);

			float realStartTime = (node.Command.StartTime - _trace.MinStartTime);

			// size node to start and end with the start and end times
			float rawSize = (node.Command.EndTime - node.Command.StartTime) * TimeToDistanceScaleFactor;
			float scaleX = rawSize > NodeMinLength ? rawSize : NodeMinLength;
			visualsObject.transform.localScale = new Vector3(scaleX, NodeWidth, NodeWidth);


			// position node to start at its start time
			float offsetToStart = realStartTime * TimeToDistanceScaleFactor;
			float offsetX = offsetToStart + (scaleX / 2f);

			float offsetZ = node.Command.ComputeNode.Id * ComputeNodeLaneWidth;
			float offsetY = HeightOffset + layer * (NodeWidth + NodeVerticalSpacing);
			node.transform.localPosition = new Vector3(offsetX, offsetY, offsetZ);
		}

		private void PositionNodeCircular(Node node)
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;
			int numComputeNodes = _trace.ComputeNodes.Count;
			float circleRadius = CalculateCircleRadius(numComputeNodes);

			GameObject visualsObject = node.GetVisualsParent().gameObject;
			int layer = ComputeLayer(node);

			float realStartTime = (node.Command.StartTime - _trace.MinStartTime);

			// size node to start and end with the start and end times
			float rawSize = (node.Command.EndTime - node.Command.StartTime) * TimeToDistanceScaleFactor;
			float scaleY = rawSize > NodeMinLength ? rawSize : NodeMinLength; //scale along y-axis for height depending on length
			visualsObject.transform.localScale = new Vector3(NodeWidth, scaleY, NodeWidth);

			// position node vertically to start at its start time
			float offsetToStart = realStartTime * TimeToDistanceScaleFactor;
			float offsetY = offsetToStart + (scaleY / 2f);

			// position outwards to clear circle center + height from assigned layer
			float offsetX = circleRadius + layer * (NodeWidth + NodeVerticalSpacing);

			// rotate around y-axis to compute node radial position
			ulong computeNodeID = node.Command.ComputeNode.Id;
			float rotY = (360f / numComputeNodes) * computeNodeID;

			// position locally before rotation to have the rotate action modify the location accordingly
			node.transform.localPosition = new Vector3(offsetX, offsetY, 0);

			// rotate around parent global position instead of scene origin to accomodate for repositioning with moved timeline
			node.transform.RotateAround(node.transform.parent.position, Vector3.up, rotY);
			//node.transform.RotateAround(Vector3.zero, Vector3.up, rotY);
		}

		/// <summary>
		/// Calculates the circle radius for the circular layout policy.
		/// </summary>
		/// <param name="numComputeNodes">The number of compute nodes in the trace.</param>
		public static float CalculateCircleRadius(int numComputeNodes)
		{
			// compute circle size such that each node has at least nodeWidth * 2.5 radians of space
			float circleRadius = (NodeWidth * 2.5f * numComputeNodes) / (2 * Mathf.PI);
			circleRadius = Mathf.Max(circleRadius, MinimumCircleRadius);
			return circleRadius;
		}

		private int ComputeLayer(Node node)
		{
			bool DEBUG = false;// node.Command.ComputeNode.Id == 0;

			List<List<Node>> layers = _layersDict[node.Command.ComputeNode.Id];

			if (DEBUG)
			{
				Debug.Log("computing layer for node id " + node.Command.Id + " - " + layers.Count + " layers exist");
			}


			for (int i = 0; i < layers.Count; i++)
			{
				if (DEBUG)
				{
					Debug.Log("checking layer " + i + " for node id " + node.Command.Id + " (contains " + layers[i].Count + " nodes)");
				}

				var layer = layers[i];
				for (int j = layer.Count - 1; j >= 0; j--)
				{
					if (DEBUG)
					{
						Debug.Log("checking node j=" + j + "/" + (layer.Count - 1) + " with id " + layer[j].Command.Id + " and s: " + layer[j].Command.StartTime + ", e: " + layer[j].Command.EndTime);
					}

					// does the node end before the new one and is there enough space between the last node and the new one in this layer?
					if (layer[j].Command.EndTime < node.Command.StartTime &&
						(node.Command.StartTime - layer[j].Command.EndTime >= NodeMinSequentialTimeSpacing))
					{
						// check if next node on layer starts after the new ones' end time and if there's enough spacing
						if (j == layer.Count - 1 || (layer[j + 1].Command.StartTime > node.Command.EndTime &&
							(layer[j + 1].Command.StartTime - node.Command.EndTime > NodeMinSequentialTimeSpacing)))
						{
							if (DEBUG)
							{
								Debug.Log("chose spot for id " + node.Command.Id + " at index " + (j + 1) + " after id " + layer[j].Command.Id);
							}

							layer.Insert(j + 1, node);
							return i;
						}
						else
						{
							if (DEBUG)
							{
								Debug.Log("invalid spot because next node (" + (j + 1) + ") ! starttime > new endtime or too little spacing -> going to next layer");
							}
							// if not then the new one doesn't fit in this layer -> goto next layer
							break;
						}
					}
					else
					{
						if (DEBUG)
						{
							Debug.Log("invalid spot because j endtime >= new starttime, checking next node");
						}
					}
				}
			}

			// no space in any existing layers found -> make new one
			layers.Add(new List<Node>());
			layers[^1].Add(node);

			if (DEBUG)
			{
				Debug.Log("Created new layer " + (layers.Count - 1) + " for id " + node.Command.Id);
			}

			return layers.Count - 1;

		}
	}
}
