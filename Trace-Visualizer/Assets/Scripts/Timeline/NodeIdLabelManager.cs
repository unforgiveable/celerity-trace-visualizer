using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace celerity.visualizer.timeline
{
	/// <summary>
	/// System for creating/removing the compute node id labels for the circular layout policy.
	/// </summary>
	public class NodeIdLabelManager : MonoBehaviour
	{
		public static NodeIdLabelManager Instance { get; private set; }

		[SerializeField] Transform NodeIdLabelParent;

		private GameObject _prefabNodeIdLabel;

		private const float LabelYOffset = 0.05f;

		private void Awake()
		{
			Instance = this;

			_prefabNodeIdLabel = Resources.Load<GameObject>("Prefabs/NodeIdLabel");
		}

		public void Init(NodeLayoutPolicy layoutPolicy, int numNodes)
		{
			RemoveAllLabels();

			// only place labels if using circular layout
			if (layoutPolicy != NodeLayoutPolicy.Circular)
				return;

			CreateNodeIdLabels(numNodes);
		}

		/// <summary>
		/// Creates the labels for all compute nodes.
		/// </summary>
		/// <param name="numNodes"></param>
		private void CreateNodeIdLabels(int numNodes)
		{
			float degPerNode = 360f / numNodes;
			float circleRadius = NodePositionManager.CalculateCircleRadius(numNodes);
			float labelSideLength, labelDistanceFromCenter;

			if (numNodes > 2)
			{
				float degPerNodeRadHalf = (degPerNode * Mathf.Deg2Rad) / 2;

				// derived from the trigonometric formulas for right-angled triangles
				labelSideLength = Mathf.Sin(degPerNodeRadHalf) * circleRadius * 2;
				labelDistanceFromCenter = Mathf.Cos(degPerNodeRadHalf) * circleRadius;
			}
			else
			{
				// fewer than 2 nodes -> computation would break down
				// use default values instead
				labelSideLength = 1f;
				labelDistanceFromCenter = circleRadius - 0.5f;
			}

			// create and place labels
			for (int i = 0; i < numNodes; i++)
			{
				GameObject label = Instantiate(_prefabNodeIdLabel, NodeIdLabelParent);

				label.transform.GetChild(1).GetComponent<TMP_Text>().text = i.ToString();

				// position offset towards +x on the perimeter of the circle
				label.transform.localPosition = new Vector3(labelDistanceFromCenter, LabelYOffset, labelSideLength / 2f);

				// set size to the appropriate side length
				label.GetComponent<RectTransform>().sizeDelta = new Vector2(labelSideLength * 100, 30);

				// rotate into position around the parent location
				float yRotAngle = i * degPerNode;
				label.transform.RotateAround(NodeIdLabelParent.transform.position, Vector3.up, yRotAngle);
			}
		}

		/// <summary>
		/// Removes all node id labels from the scene.
		/// </summary>
		private void RemoveAllLabels()
		{
			for (int i = NodeIdLabelParent.childCount - 1; i >= 0; i--)
			{
				Destroy(NodeIdLabelParent.GetChild(i).gameObject);
			}
		}


	}
}
