using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using celerity.visualizer.tracedata;

namespace celerity.visualizer.timeline
{
	/// <summary>
	/// System for showing the details of a command node.
	/// Supports showing details on an arbitrary number of nodes at the same time, but is currently only ever used for one node at once.
	/// </summary>
	public class NodeDetailsManager : MonoBehaviour
	{
		public static NodeDetailsManager Instance { get; private set; }

		// pre-loaded prefabs
		private GameObject _textCanvasPrefab;
		private GameObject _bufferVisualPrefab;
		private GameObject _bufferRangePrefab;
		private GameObject _bufferCanvasTextLinePrefab;

		private Transform _mainCam;
		private List<Node> _nodesDetailsShown;

		private readonly Vector3 DetailsCanvasBaseOffset = new(0, 0, -0.2f);
		private const float DetailsVertOffset = 0.1f;

		private readonly Vector3 BufferVisualBaseOffset = new(0, 0.15f, 0.1f);
		private const float BufferVisualsHorizontalSpacing = 0.4f;
		private const float BufferRangeOverallSize = 0.3f;
		private const float BufferRangeMinSize = 0.001f;
		private const float BufferCanvasHorizontalSize = 20;
		private const float BufferCanvasTextVertSize = 1.5f;

		private readonly Color ColorAccessModeRead = Color.green;
		private readonly Color ColorAccessModeWrite = Color.red;
		private readonly Color ColorAccessModeReadWrite = Color.yellow;
		private readonly Color ColorAccessModeDiscardWrite = new(0.5f, 0, 0, 1);
		private readonly Color ColorAccessModeDiscardReadWrite = new(0.8f, 0.62f, 0, 1);
		private readonly Color ColorAccessModeAtomic = Color.white;
		private readonly Color ColorAccessOpacityReduction = new(0, 0, 0, 0.3f);


		private void Awake()
		{
			Instance = this;

			_mainCam = Camera.main.transform;

			_textCanvasPrefab = Resources.Load<GameObject>("Prefabs/NodeCanvas");
			_bufferVisualPrefab = Resources.Load<GameObject>("Prefabs/BufferVisual");
			_bufferRangePrefab = Resources.Load<GameObject>("Prefabs/BufferRange");
			_bufferCanvasTextLinePrefab = Resources.Load<GameObject>("Prefabs/UI/TextLine");
		}

		private void Update()
		{
			if (_nodesDetailsShown == null)
				return;

			foreach(Node node in _nodesDetailsShown)
			{
				// TODO decide if details should move to match relative player position vs. stay in initial location
				// if so -> split into only repositioning along node vs full setup (since number of buffers only known at initial creation)

				//PositionDetails(node, GlobalSettings.LAYOUT_POLICY);
				RotateCanvasTowardsPlayer(node);
			}
		}

		public void Init()
		{
			_nodesDetailsShown = new List<Node>();
		}

		public void CreateNodeDetails(Node node)
		{
			if (_nodesDetailsShown.Contains(node))
				return;

			int numBuffersShown = 0;

			switch (node.Command.CommandType)
			{
				case CommandType.Nop:
				case CommandType.Horizon:
				case CommandType.Sync:
				case CommandType.Shutdown:
					AddTextCanvas(node);
					break;
				case CommandType.Task:
				case CommandType.Push:
				case CommandType.AwaitPush:
					AddTextCanvas(node);
					numBuffersShown = AddBufferVisuals(node);
					break;
			}

			_nodesDetailsShown.Add(node);
			PositionDetails(node, GlobalSettings.LAYOUT_POLICY, numBuffersShown);
			RotateCanvasTowardsPlayer(node);
		}

		public void RemoveNodeDetails(Node node)
		{
			if (!_nodesDetailsShown.Contains(node))
				return;

			_nodesDetailsShown.Remove(node);
			Transform detailsParent = node.GetDetailsParent();
			for (int i = detailsParent.childCount - 1; i >= 0; i--)
			{
				Destroy(detailsParent.GetChild(i).gameObject);
			}
		}

		public void RemoveAllNodeDetails()
		{
			for (int i = _nodesDetailsShown.Count - 1; i >= 0; i--)
			{
				RemoveNodeDetails(_nodesDetailsShown[i]);
			}

			if (_nodesDetailsShown.Count != 0)
			{
				Debug.LogError("Failed to remove details from all nodes.");
			}
		}

		/// <summary>
		/// Positions the node details relative to the <paramref name="node"/> depending on the current <paramref name="layoutPolicy"/>. Takes the number of shown buffers to position the details correctly in circular layout mode.
		/// </summary>
		/// <exception cref="System.NotImplementedException">In case of unimplemented layout policy.</exception>
		private void PositionDetails(Node node, NodeLayoutPolicy layoutPolicy, int numBuffersShown)
		{
			Transform positionTarget = _mainCam;
			Transform detailsParent = node.GetDetailsParent();
			Vector3 localDetailsPos;

			// get camera position in (details parent-)local coordinates
			Vector3 localTargetPos = node.transform.InverseTransformPoint(positionTarget.position);

			if (layoutPolicy == NodeLayoutPolicy.LinearMixed)
			{
				// position relative to node along x-extend of the node
				float localHalfXExtend = node.GetVisualsParent().localScale.x / 2f;
				Vector3 localNodeXStartPos = new(-localHalfXExtend, DetailsVertOffset, 0f);
				Vector3 localNodeXEndPos = new(localHalfXExtend, DetailsVertOffset, 0f);

				// if target position is outside x-extend of the node clamp it to either end
				if (localTargetPos.x < localNodeXStartPos.x)
					localDetailsPos = localNodeXStartPos;
				else if (localTargetPos.x > localNodeXEndPos.x)
					localDetailsPos = localNodeXEndPos;
				else
					localDetailsPos = new(localTargetPos.x, DetailsVertOffset, 0);
			}
			else if (layoutPolicy == NodeLayoutPolicy.Circular)
			{
				// add dynamic offset depending on number of buffers that are visualized
				// (base sideways offset) + ((addition offset for >1 number of buffers) / 2 since moving center of mass pivot)
				float horizontalOffset = (DetailsVertOffset * 3) + ((BufferVisualsHorizontalSpacing * (numBuffersShown - 1)) / 2f);

				float localHalfYExtend = node.GetVisualsParent().localScale.y / 2f;
				Vector3 localNodeYStartPos = new(0f, -localHalfYExtend, -horizontalOffset);
				Vector3 localNodeYEndPos = new(0f, localHalfYExtend, -horizontalOffset);

				// if target position is outside y-extend of the node clamp it to either end
				if (localTargetPos.y < localNodeYStartPos.y)
					localDetailsPos = localNodeYStartPos;
				else if (localTargetPos.y > localNodeYEndPos.y)
					localDetailsPos = localNodeYEndPos;
				else
					localDetailsPos = new(0, localTargetPos.y, -horizontalOffset);

				// rotate details towards circle center by addint 90° to rotation
				detailsParent.localRotation = Quaternion.Euler(0, 90, 0);
			}
			else
			{
				throw new System.NotImplementedException("Layout policy not yet implemented.");
			}

			detailsParent.localPosition = localDetailsPos;

		}

		private void RotateCanvasTowardsPlayer(Node node)
		{
			Transform detailsCanvas = node.GetDetailsParent().GetChild(0);
			Vector3 diff = detailsCanvas.position - _mainCam.position;
			detailsCanvas.rotation = Quaternion.LookRotation(diff, Vector3.up);
		}

		private void AddTextCanvas(Node node)
		{
			Transform textCanvas = Instantiate(_textCanvasPrefab, node.GetDetailsParent()).transform;

			textCanvas.localPosition = DetailsCanvasBaseOffset;

			textCanvas.GetChild(1).GetComponent<TMPro.TMP_Text>().text = "ID " + node.Command.Id.ToString() + " [" + node.Command.ComputeNode.Id.ToString() + "] - " + node.Command.CommandType.ToString();
			textCanvas.GetChild(2).GetComponent<TMPro.TMP_Text>().text = "took " + (node.Command.EndTime - node.Command.StartTime) / 1000f + "ms";
			textCanvas.GetChild(3).GetComponent<TMPro.TMP_Text>().text = (node.Command.Task != null) ? "part of " + node.Command.Task.Name : "";

			switch (node.Command.CommandType)
			{
				case CommandType.Push:
				case CommandType.AwaitPush:
					Dependency<Command> dataDep = node.Command.Predecessors.Where(x => x.Kind == DependencyKind.DataDep).FirstOrDefault() ?? 
						node.Command.Successors.Where(x => x.Kind == DependencyKind.DataDep).FirstOrDefault();

					if (dataDep != null)
					{
						Command other = dataDep.Target == node.Command ? dataDep.Origin : dataDep.Target;
						textCanvas.GetChild(4).GetComponent<TMPro.TMP_Text>().text = "Target: Command ID " + other.Id;
					}
					else
						textCanvas.GetChild(4).GetComponent<TMPro.TMP_Text>().text = "Target: [failed to compute]";

					// collect accessed buffer Ids, remove duplicates, sort and concat to string
					List<string> bufferIds1 = node.Command.BufferAccesses.Select(x => x.Buffer.Id).Distinct().OrderBy(x => x).Select(x => x.ToString()).ToList();
					textCanvas.GetChild(5).GetComponent<TMPro.TMP_Text>().text = "Accessed Buffer IDs: " + bufferIds1.Aggregate((x, y) => x + ", " + y); ;
					textCanvas.GetChild(6).GetComponent<TMPro.TMP_Text>().text = "";
					break;
				case CommandType.Task:
					// collect accessed buffer Ids, remove duplicates, sort and concat to string
					List<string> bufferIds2 = node.Command.BufferAccesses.Select(x => x.Buffer.Id).Distinct().OrderBy(x => x).Select(x => x.ToString()).ToList();
					textCanvas.GetChild(4).GetComponent<TMPro.TMP_Text>().text = "Accessed Buffer IDs: " + bufferIds2.Aggregate((x, y) => x + ", " + y);

					textCanvas.GetChild(5).GetComponent<TMPro.TMP_Text>().text = "";
					textCanvas.GetChild(6).GetComponent<TMPro.TMP_Text>().text = "";
					break;
				default:
					textCanvas.GetChild(4).GetComponent<TMPro.TMP_Text>().text = "";
					textCanvas.GetChild(5).GetComponent<TMPro.TMP_Text>().text = "";
					textCanvas.GetChild(6).GetComponent<TMPro.TMP_Text>().text = "";
					break;
			}
		}

		/// <summary>
		/// Adds the buffer visuals to the node details for all buffers accessed by this node's command.
		/// </summary>
		/// <param name="node"></param>
		/// <returns>The number of buffers shown.</returns>
		private int AddBufferVisuals(Node node)
		{
			// combine accesses to the same buffer to the same list
			var bufferAccesses = node.Command.BufferAccesses.GroupBy(x => x.Buffer.Id, (x, y) => y.ToList()).OrderBy(x => x[0].Buffer.Id).ToList();

			for (int i = 0; i < bufferAccesses.Count; i++)
			{
				float positionIndex = i - ((bufferAccesses.Count - 1) / 2f);
				// add visualization for the buffer
				AddBufferVisual(node, bufferAccesses[i], new(positionIndex * BufferVisualsHorizontalSpacing, 0, 0));
			}

			return bufferAccesses.Count;
		}

		private void AddBufferVisual(Node node, List<BufferAccess> bufferAccesses, Vector3 positionOffset)
		{
			Transform detailsParent = node.GetDetailsParent();
			Buffer buffer = bufferAccesses[0].Buffer;

			GameObject bufferObject = Instantiate(_bufferVisualPrefab, detailsParent);
			bufferObject.transform.localPosition = positionOffset + BufferVisualBaseOffset;

			BufferVisual bufferVisual = bufferObject.GetComponent<BufferVisual>();
			bufferVisual.Init(buffer);

			// size entire range cube to match
			float maxDimension = System.Math.Max(buffer.Size.x, System.Math.Max(buffer.Size.y, buffer.Size.z));
			Vector3 rangeSizeActual = ComputeActualBufferCubeSize(buffer.Size, maxDimension);

			Transform entireRange = bufferVisual.GetRangeTransform();
			entireRange.localScale = rangeSizeActual;
			entireRange.localPosition = new(0, BufferRangeOverallSize / 2f, 0);

			// size canvas to fit all lines
			Transform bufferCanvas = bufferVisual.GetCanvasParent();
			bufferCanvas.GetComponent<RectTransform>().sizeDelta = new(BufferCanvasHorizontalSize, (bufferAccesses.Count + 1) * BufferCanvasTextVertSize);

			// add buffer id line
			GameObject bufferIdTextLine = Instantiate(_bufferCanvasTextLinePrefab, bufferCanvas);
			bufferIdTextLine.GetComponent<RectTransform>().anchoredPosition = new(0, -(BufferCanvasTextVertSize / 2f));
			bufferIdTextLine.GetComponent<TMPro.TMP_Text>().text = "Buffer ID " + buffer.Id + " " + buffer.Size;

			for (int i = 0; i < bufferAccesses.Count; i++)
			{
				BufferAccess bufferAccess = bufferAccesses[i];

				// add canvas text lines for each access
				GameObject newTextLine = Instantiate(_bufferCanvasTextLinePrefab, bufferCanvas);
				newTextLine.GetComponent<RectTransform>().anchoredPosition = new(0,- (BufferCanvasTextVertSize / 2f) - ((i+1) * BufferCanvasTextVertSize));
				newTextLine.GetComponent<TMPro.TMP_Text>().text = bufferAccess.AccessMode + ": " + bufferAccess.Start + "->" + bufferAccess.End;

				// add access range cubes
				AddBufferAccessCube(buffer, bufferObject.transform, maxDimension, bufferAccess);
			}
		}

		private void AddBufferAccessCube(Buffer buffer, Transform bufferVisualsTransform, float maxDimension, BufferAccess bufferAccess)
		{
			GameObject newRangeCube = Instantiate(_bufferRangePrefab, bufferVisualsTransform.transform);
			newRangeCube.name = "Access from " + bufferAccess.Start + " to " + bufferAccess.End;
			Vector3Int rangeExtend = bufferAccess.End - bufferAccess.Start;

			// compute range cube size
			Vector3 accessRangeSize = ComputeActualBufferCubeSize(rangeExtend, maxDimension);
			newRangeCube.transform.localScale = accessRangeSize;

			// compute range cube location
			Vector3 accessRangeMidPoint = bufferAccess.Start + ((Vector3)rangeExtend / 2f);
			Vector3 relativeMidPointLocation = new Vector3(accessRangeMidPoint.x / buffer.Size.x, accessRangeMidPoint.y / buffer.Size.y, accessRangeMidPoint.z / buffer.Size.z) - new Vector3(0.5f, 0, 0.5f);
			Vector3 scaledMidPointLocation = relativeMidPointLocation * BufferRangeOverallSize;
			newRangeCube.transform.localPosition = scaledMidPointLocation;

			Material mat = newRangeCube.GetComponent<MeshRenderer>().material;

			// color range depending on access type
			switch (bufferAccess.AccessMode)
			{
				case AccessMode.Read:
					mat.color = ColorAccessModeRead;
					break;
				case AccessMode.Write:
					mat.color = ColorAccessModeWrite;
					break;
				case AccessMode.ReadWrite:
					mat.color = ColorAccessModeReadWrite;
					break;
				case AccessMode.DiscardWrite:
					mat.color = ColorAccessModeDiscardWrite;
					break;
				case AccessMode.DiscardReadWrite:
					mat.color = ColorAccessModeDiscardReadWrite;
					break;
				case AccessMode.Atomic:
					mat.color = ColorAccessModeAtomic;
					break;
			}

			// reduce opacity a bit
			mat.color -= ColorAccessOpacityReduction;
		}

		private Vector3 ComputeActualBufferCubeSize(Vector3Int range, float maxDimension)
		{
			Vector3 rangeSizeNormalized = new(range.x / maxDimension, range.y / maxDimension, range.z / maxDimension);
			return Vector3.Max(rangeSizeNormalized * BufferRangeOverallSize, new(BufferRangeMinSize, BufferRangeMinSize, BufferRangeMinSize));
		}


	}
}