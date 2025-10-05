using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using celerity.visualizer.tracedata;
using System.Linq;
using System;

namespace celerity.visualizer.timeline
{
	/// <summary>
	/// System for creating the visuals for the task nodes.
	/// </summary>
	public class TaskNodeManager : MonoBehaviour
	{
		public static TaskNodeManager Instance { get; private set; }

		[SerializeField] Transform TaskParent;
		public Dictionary<Task, TaskNode> TaskNodes { get; private set; }

		private GameObject _taskPrefab;
		private int _numLanes;

		private const float VertOffset = 0.05f;

		private void Awake()
		{
			Instance = this;
			_taskPrefab = Resources.Load<GameObject>("Prefabs/Task");
			TaskNodes = new Dictionary<Task, TaskNode>();

			ForceRemoveAllTaskNodes();
		}

		public void Init()
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;

			RemoveAllTaskNodes();

			if (_trace == null)
			{
				_numLanes = 0;
				return;
			}

			_numLanes = (int)_trace.ComputeNodes.Keys.Max() + 1;
		}

		public void CreateAllTaskNodes(ICollection<Task> tasks)
		{
			foreach (Task task in tasks)
			{
				CreateTaskNode(task);
			}
		}

		public void CreateTaskNode(Task task)
		{
			if (task == null)
			{
				Debug.LogError("CreateTaskNode called with task = null");
				return;
			}
			if (TaskNodes.ContainsKey(task))
			{
				Debug.LogError("trying to create task node for task id " + task.Id + " but node already exists.");
				return;
			}

			// TODO: remove - skipping task creation for circular layout policy for now
			if (GlobalSettings.LAYOUT_POLICY == NodeLayoutPolicy.Circular)
				return;

			GameObject newGameObject = Instantiate(_taskPrefab, TaskParent);
			newGameObject.name = "TaskNode " + task.Id;

			TaskNode taskNode = newGameObject.GetComponent<TaskNode>();
			taskNode.Task = task;

			TaskNodes[task] = taskNode;

			taskNode.transform.GetChild(1).GetComponent<TMPro.TMP_Text>().text = task.Name;
			taskNode.transform.GetChild(2).GetComponent<TMPro.TMP_Text>().text = task.ExecutionTarget.ToString();

			PositionTask(taskNode, GlobalSettings.LAYOUT_POLICY);
		}

		public void RemoveTaskNode(TaskNode taskNode)
		{
			if (taskNode == null)
			{
				Debug.LogError("RemoveTaskNode called with taskNode = null");
				return;
			}

			TaskNodes.Remove(taskNode.Task);

			Destroy(taskNode.gameObject);
		}

		public void RemoveAllTaskNodes()
		{
			ForceRemoveAllTaskNodes();

			TaskNodes.Clear();
		}

		private void ForceRemoveAllTaskNodes()
		{
			for (int i = TaskParent.childCount - 1; i >= 0; i--)
			{
				Destroy(TaskParent.GetChild(i).gameObject);
			}
		}

		/// <summary>
		/// Positions the task node according to the specified layout policy.
		/// </summary>
		private void PositionTask(TaskNode taskNode, NodeLayoutPolicy layoutPolicy)
		{

			switch (layoutPolicy)
			{
				case NodeLayoutPolicy.LinearMixed:
					PositionTaskLinearMixed(taskNode);
					break;

				case NodeLayoutPolicy.Circular:
					PositionTaskCircular(taskNode);
					break;

				default:
					throw new NotImplementedException("Layout policy not yet implemented.");
			}
		}

		private void PositionTaskLinearMixed(TaskNode taskNode)
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;
			ulong taskId = taskNode.Task.Id;

			ulong minTime = _trace.Commands.Select(x => x.Value).Where(x => x.Task != null && x.Task.Id == taskId).Select(x => x.StartTime).Min() - _trace.MinStartTime;
			ulong maxTime = _trace.Commands.Select(x => x.Value).Where(x => x.Task != null && x.Task.Id == taskId).Select(x => x.EndTime).Max() - _trace.MinStartTime;

			float rawWidth = (maxTime - minTime) * NodePositionManager.TimeToDistanceScaleFactor;

			float width = rawWidth * 100;
			float height = _numLanes * NodePositionManager.ComputeNodeLaneWidth * 100f;

			float offsetX = minTime * NodePositionManager.TimeToDistanceScaleFactor;

			RectTransform rectTransform = taskNode.gameObject.GetComponent<RectTransform>();
			rectTransform.localPosition = new(offsetX, VertOffset, -(NodePositionManager.NodeWidth / 2f) - (NodePositionManager.ComputeNodeLaneWidth / 4f));
			rectTransform.sizeDelta = new(width, height);
		}

		private void PositionTaskCircular(TaskNode taskNode)
		{
			//TODO come up with task placement for circular mode
			throw new NotImplementedException();
		}

	}
}