using celerity.visualizer.tracedata;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace celerity.visualizer.timeline
{
    /// <summary>
    /// System for creating the visual components of command nodes.
    /// Mostly based on pre-defined materials that get assigned to the nodes due to performance reasons.
    /// Also handles the highlighting of nodes.
    /// </summary>
	public class NodeVisualsManager : MonoBehaviour
	{
		public static NodeVisualsManager Instance { get; private set; }

        [SerializeField] Material MatNop;
        [SerializeField] Material MatHorizon;
        [SerializeField] Material MatTask;
        [SerializeField] Material MatPush;
        [SerializeField] Material MatAwaitPush;
        [SerializeField] Material MatShutdown;
        [SerializeField] Material MatSync;
        [SerializeField] Material MatError;

        [SerializeField] Material MatHighlightedNop;
        [SerializeField] Material MatHighlightedHorizon;
        [SerializeField] Material MatHighlightedTask;
        [SerializeField] Material MatHighlightedPush;
        [SerializeField] Material MatHighlightedAwaitPush;
        [SerializeField] Material MatHighlightedShutdown;
        [SerializeField] Material MatHighlightedSync;
        [SerializeField] Material MatHighlightedError;

        private GameObject HorizonStreamPrefab;

        private List<Node> _highlightedNodes;

        /*
        private readonly Color OpacityOffset = new(0, 0, 0, 0.5f);
        private readonly Color NodeNopColor = new(0.2f, 0.2f, 0.2f);
        private readonly Color NodeHorizonColor = new(0.6f, 0.6f, 1f);
        private readonly Color NodeTaskColor = new(1f, 1f, 1f);
        private readonly Color NodePushColor = new(0f, 1f, 0f);
        private readonly Color NodeAwaitPushColor = new(0f, 0.6f, 0f);
        private readonly Color NodeShutdownColor = new(1f, 0.2f, 0.2f);
        private readonly Color NodeSyncColor = new(0.9f, 0.3f, 0.3f);
        private readonly Color NodeErrorColor = new(0, 0, 0);
        */

		private void Awake()
		{
			Instance = this;

            HorizonStreamPrefab = Resources.Load<GameObject>("Prefabs/Horizon_Stream");
		}

        public void Init()
		{
            _highlightedNodes = new List<Node>();
        }

        /// <summary>
        /// Sets up the visuals for a node depending on its command type
        /// </summary>
        /// <param name="node"></param>
        public void CreateNodeVisuals(Node node)
		{
            GameObject visualsObject = node.GetVisualsParent().gameObject;

            visualsObject.GetComponent<MeshRenderer>().sharedMaterial = GetNodeTypeMat(node.Command.CommandType);

            if (node.Command.CommandType == CommandType.Horizon)
            {
                GameObject horizonStream = Instantiate(HorizonStreamPrefab);

                horizonStream.transform.SetParent(visualsObject.transform, false);
            }
        }

		public void HighlightNode(Node node)
        {
            if (_highlightedNodes.Contains(node))
                return;

            GameObject visualsObject = node.GetVisualsParent().gameObject;
            visualsObject.GetComponent<MeshRenderer>().sharedMaterial = GetNodeTypeMatHighlighted(node.Command.CommandType);

            _highlightedNodes.Add(node);
        }

        public void UnHighlightNode(Node node)
        {
            if (!_highlightedNodes.Contains(node))
                return;

            GameObject visualsObject = node.GetVisualsParent().gameObject;
            visualsObject.GetComponent<MeshRenderer>().sharedMaterial = GetNodeTypeMat(node.Command.CommandType);

            _highlightedNodes.Remove(node);
        }

        public void UnHighlightAllNodes()
        {
            for (int i = _highlightedNodes.Count - 1; i >= 0; i--)
            {
                UnHighlightNode(_highlightedNodes[i]);
            }

            if (_highlightedNodes.Count > 0)
            {
                Debug.LogError("Failed to un-highlight all nodes.");
            }
        }

        private Material GetNodeTypeMat(CommandType type)
		{
            return type switch
            {
                CommandType.Nop => MatNop,
                CommandType.Horizon => MatHorizon,
                CommandType.Task => MatTask,
                CommandType.Push => MatPush,
                CommandType.AwaitPush => MatAwaitPush,
                CommandType.Shutdown => MatShutdown,
                CommandType.Sync => MatSync,
                _ => MatError,
            };
        }

        private Material GetNodeTypeMatHighlighted(CommandType type)
        {
            return type switch
            {
                CommandType.Nop => MatHighlightedNop,
                CommandType.Horizon => MatHighlightedHorizon,
                CommandType.Task => MatHighlightedTask,
                CommandType.Push => MatHighlightedPush,
                CommandType.AwaitPush => MatHighlightedAwaitPush,
                CommandType.Shutdown => MatHighlightedShutdown,
                CommandType.Sync => MatHighlightedSync,
                _ => MatHighlightedError,
            };
        }

	}
}
