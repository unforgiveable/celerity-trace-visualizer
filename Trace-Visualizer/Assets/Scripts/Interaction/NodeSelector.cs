using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using celerity.visualizer.timeline;

namespace celerity.visualizer.interaction
{
	public class NodeSelector : MonoBehaviour
	{
		public static NodeSelector Instance { get; private set; }

		[SerializeField] InputAction SelectAction;
		[SerializeField] InputAction ToggleModeAction;

		public bool DoNodeDetection { get; set; }

		private Node _lastNode;
		private const float DetectionRadius = 0f;

		private void Awake()
		{
			Instance = this;
		}

		private void Start()
		{
			ToggleModeAction.Enable();

			SetInteractionEnabled();
		}

		private void Update()
		{
			//if (ToggleModeAction.triggered)
			//{
			//	ToggleInteraction();
			//}

			if (DoNodeDetection && SelectAction.triggered)
			{
				DetectNode();
			}
		}

		/// <summary>
		/// Gets called when a new mode filter setting is applied.
		/// All highlights/node details/dependencies are already removed at this point.
		/// </summary>
		public void ClearSelectedNode()
		{
			_lastNode = null;
		}

		private void DetectNode()
		{
			var res = Physics.OverlapSphere(transform.position, DetectionRadius);

			if (res.Length == 1)
			{
				Node node;
				try
				{
					node = res[0].transform.parent.GetComponent<Node>();
				}
				catch (System.Exception)
				{
					Debug.Log("Detect node on collider without node component.");
					return;
				}

				//Debug.Log("Found node " + node.name);

				WristMenuModes wristMenuModes = WristMenuModes.Instance;

				wristMenuModes.DeselectNode(_lastNode);

				if (_lastNode == node)
				{
					_lastNode = null;
					return;
				}

				wristMenuModes.SelectNode(node);

				_lastNode = node;
			}
		}

		private void ToggleInteraction()
		{
			if (DoNodeDetection)
				SetInteractionDisabled();
			else
				SetInteractionEnabled();
		}

		private void SetInteractionEnabled()
		{
			DoNodeDetection = true;
			SelectAction.Enable();
		}

		private void SetInteractionDisabled()
		{
			DoNodeDetection = false;
			SelectAction.Disable();
		}


	}
}
