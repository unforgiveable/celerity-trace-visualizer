using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using celerity.visualizer.tracedata;
using System;

namespace celerity.visualizer.timeline
{
	public enum NodeLayoutPolicy
	{
		LinearMixed = 0,
		Circular = 1,
	}

	/// <summary>
	/// System for creating and removing command node visual objects.
	/// Also handles hiding/showing of nodes based on filters.
	/// Acts as a container and interface point for the current <see cref="ChunkingSystem"/> instance.
	/// </summary>
	public class NodeManager : MonoBehaviour
	{
		public static NodeManager Instance { get; private set; }

		[SerializeField] Transform NodeParent;
		public Dictionary<Command, Node> Nodes { get; private set; }

		public CommandType CurrentTypeMask { get; private set; }
		public bool IsShowingNodesBySet { get; private set; }

		/// <summary>
		/// If not <see langword="null"/> contains the current vicinity size. If <see langword="null"/> means entire timeline is shown (=vicinity mode is off).
		/// </summary>
		public ulong? CurrentVicinitySize { get; private set; }

		/// <summary>
		/// If not <see langword="null"/> contains the current vicinity center time. If <see langword="null"/> means entire timeline is shown (=vicinity mode is off).
		/// </summary>
		public ulong? CurrentVicinityCenter { get; private set; }

		/// <summary>
		/// The last used NodePositionManager. Will get replaced when the nodes are positioned again.
		/// </summary>
		public NodePositionManager CurrentPositionManager { get; private set; }


		private GameObject _nodePrefab;
		private NodeLayoutPolicy _currentNodeLayoutPolicy;
		private ChunkingSystem _chunkingSystem;

		/// <summary>
		/// If not null contains the nodes that are currently being shown by set.
		/// Used for when different chunks are being shown to still maintain the set hiding.
		/// </summary>
		private HashSet<Node> _currentShowingSet = null;

		void Awake()
		{
			Instance = this;

			_nodePrefab = Resources.Load<GameObject>("Prefabs/Node");

			ForceRemoveAllNodeObjects();
		}

		public void Init()
		{
			Nodes = new Dictionary<Command, Node>();

			CurrentTypeMask = CommandType.All;
			IsShowingNodesBySet = false;
			CurrentVicinitySize = null;
			CurrentVicinityCenter = null;

			ForceRemoveAllNodeObjects();

			Trace _trace = TimelineManager.Instance.CurrentTrace;
			if (_trace != null)
				_chunkingSystem = new(_trace.Duration, _trace.MinStartTime, NodeParent);
		}

		/// <summary>
		/// Creates the nodes for all <paramref name="commands"/> and positions them using a new <see cref="NodePositionManager"/> instance according to the <paramref name="layoutPolicy"/>.
		/// </summary>
		/// <param name="commands">List of commands to create nodes for. Can be in any order.</param>
		/// <param name="minStartTime">MinStartTime of the trace. Used to assign the commands into their respective chunks.</param>
		/// <param name="layoutPolicy">LayoutPolicy to use for positioning the nodes.</param>
		public void CreateCommandNodes(List<Command> commands, ulong minStartTime, NodeLayoutPolicy layoutPolicy)
		{
			_currentNodeLayoutPolicy = layoutPolicy;

			NodePositionManager nodePositionManager = new();

			foreach (Command c in commands)
			{
				// compute corresponding chunk and chunk transform
				ulong commandDuration = c.EndTime - c.StartTime;
				ulong midTime = c.StartTime + (commandDuration / 2) - minStartTime;

				Transform chunkTransform = _chunkingSystem.GetChunkTransformForTime(midTime);
				
				Node node = CreateNode(c, chunkTransform);

				_chunkingSystem.AddNodeToChunk(node, midTime);

				nodePositionManager.PositionNode(node, layoutPolicy);
				NodeVisualsManager.Instance.CreateNodeVisuals(node);
			}

			CurrentPositionManager = nodePositionManager;

			_chunkingSystem.ShowAllChunks();
		}

		/// <summary>
		/// Creates a single command node object and parents it to the given <paramref name="chunkParent"/> Transform.
		/// </summary>
		/// <param name="command">The command to visualize.</param>
		/// <param name="chunkParent">The transform of the corresponding chunk.</param>
		private Node CreateNode(Command command, Transform chunkParent)
		{
			if (command == null)
			{
				Debug.LogError("AddNode called with command = null");
				return null;
			}
			if (Nodes.ContainsKey(command))
			{
				Debug.LogError("Tried inserting Node for command id " + command.Id + " twice!");
				return null;
			}

			// create new node object and parent to provided chunkParent transform
			GameObject newNodeObject = Instantiate(_nodePrefab, chunkParent);

			newNodeObject.name = "Node Cmd " + command.Id + " [" + command.ComputeNode.Id + "] - " + command.CommandType.ToString()
				+ " - taskID " + (command.Task != null ? command.Task.Id : -1)
				+ " - s: " + command.StartTime + ", e: " + command.EndTime
				+ " - " + ((command.OtherNode != null) ? ("targetNode: " + command.OtherNode.Id) : "")
				+ ((command.ExecutionRange != null) ? (command.ExecutionRange.Start.ToString() + ", " + command.ExecutionRange.End.ToString()) : "");

			Node newNode = newNodeObject.GetComponent<Node>();
			newNode.Command = command;

			Nodes.Add(command, newNode);

			return newNode;
		}

		[Obsolete] //TODO Remove?
		public void RemoveAllNodes()
		{
			ForceRemoveAllNodeObjects();

			// reset system
			Init();
		}

		private void ForceRemoveAllNodeObjects()
		{
			for (int i = NodeParent.childCount - 1; i >= 0; i--)
			{
				Destroy(NodeParent.GetChild(i).gameObject);
			}
		}


		/// <summary>
		/// Shows all Chunks in a given vicinity.
		/// Takes an optional typeMask to only show specific node types within the resulting chunks.
		/// </summary>
		/// <param name="timePosition">Center time of the vicinity.</param>
		/// <param name="vicinitySize">Size of the vicinity in both directions.</param>
		/// <param name="typeMask">Optional node type mask to only show specific nodes in the resulting chunks.</param>
		public void ShowChunksInVicinity(ulong timePosition, ulong vicinitySize, CommandType? typeMask = null)
		{
			_chunkingSystem.ShowChunksInVicinity(timePosition, vicinitySize);

			if (typeMask != null) // if typeMask is set hide nodes by mask after showing chunks
			{
				ShowNodesByTypeInCurrentChunks(typeMask.Value, false);
			}
			else if (IsShowingNodesBySet && _currentShowingSet != null) // if currently showing by set re-apply that filter
			{
				ShowNodesBySetInCurrentChunks(_currentShowingSet);
			}

			CurrentVicinitySize = vicinitySize;
			CurrentVicinityCenter = timePosition;
		}

		/// <summary>
		/// Shows nodes according to a typeMask within the currently shown chunks.
		/// Uses the layout policy of the current timeline for repositioning the nodes if enabled.
		/// </summary>
		/// <param name="typeMask">Type mask to apply to the nodes in the currently visible chunks.</param>
		/// <param name="reposition">Optional, if true the shown nodes will be repositioned on the timeline.</param>
		public void ShowNodesByTypeInCurrentChunks(CommandType typeMask, bool reposition = true)
		{
			ShowNodesByTypeInCurrentChunks(typeMask, _currentNodeLayoutPolicy, reposition);
		}


		// TODO: consolidate with previous method since only ever used with current layout policy?
		/// <summary>
		/// Shows nodes according to a typeMask within the currently shown chunks.
		/// Allows for a specific layoutPolicy to be specified for the repositioning, if enabled.
		/// </summary>
		/// <param name="typeMask">Type mask to apply to the nodes in the currently visible chunks.</param>
		/// <param name="layoutPolicy">Layout policy to be used for the repositioning.</param>
		/// <param name="reposition">Optional, if true the shown nodes will be repositioned on the timeline.</param>
		public void ShowNodesByTypeInCurrentChunks(CommandType typeMask, NodeLayoutPolicy layoutPolicy, bool reposition = true)
		{
			if (Nodes == null || Nodes.Count == 0)
			{
				Debug.LogError("ShowNodesByType called before NodeManager init.");
				return;
			}

			// if nodes should be repositioned -> do for entire timeline so future chunk-showing is fast
			if (reposition)
			{
				NodePositionManager nodePositionManager = new();

				foreach (var entry in Nodes)
				{
					nodePositionManager.PositionNode(entry.Value, layoutPolicy);
				}

				CurrentPositionManager = nodePositionManager;
			}

			_chunkingSystem.ShowNodesInCurrentChunksByType(typeMask);

			CurrentTypeMask = typeMask;
			IsShowingNodesBySet = false;
		}

		/// <summary>
		/// Shows all nodes in the current chunks.
		/// Acts as a reset to the ShowNodesByTypeInCurrentChunks and ShowNodesBySetInCurrentChunks functions.
		/// Should be called ONCE when the type filter mode or set hiding mode is disabled.
		/// </summary>
		public void ShowAllNodesInCurrentChunks()
		{
			_chunkingSystem.ShowAllNodesInCurrentChunks();

			CurrentTypeMask = CommandType.All;
			IsShowingNodesBySet = false;
			_currentShowingSet = null;
		}

		/// <summary>
		/// Shows all chunks in the timeline.
		/// Acts as a reset to the ShowChunksInVicinity function.
		/// </summary>
		public void ShowAllChunks()
		{
			_chunkingSystem.ShowAllChunks();
		}

		/// <summary>
		/// Shows the nodes in the currently shown chunks if they're contained in the given nodeSet.
		/// </summary>
		/// <param name="nodeSet">Set of Nodes to show if they're in the currently shown Chunks.</param>
		public void ShowNodesBySetInCurrentChunks(HashSet<Node> nodeSet)
		{
			_chunkingSystem.ShowNodesInCurrentChunksBySet(nodeSet);

			IsShowingNodesBySet = true;
			_currentShowingSet = nodeSet;
		}

	}
}
