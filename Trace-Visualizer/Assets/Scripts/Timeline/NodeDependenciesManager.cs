using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using celerity.visualizer.tracedata;
using Unity.VisualScripting;
using System.Linq;
using System;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;

namespace celerity.visualizer.timeline
{
	public class NodeDependenciesManager : MonoBehaviour
	{
		public static NodeDependenciesManager Instance { get; private set; }

		[SerializeField] private Transform DependenciesParent;

		private GameObject _dependencyPrefab;
		private float _currentLineWidth;
		private int _currentNumDependenciesShown;

		private List<Dependency<Command>> _cachedCriticalPathDependencies = null;

		private readonly Color ColorStartTrueDep = new(0.05f, 0.05f, 0.05f);
		private readonly Color ColorEndTrueDep = new(0.1f, 0.1f, 0.1f);
		private readonly Color ColorStartAntiDep = new(0.4f, 0.2f, 0.2f);
		private readonly Color ColorEndAntiDep = new(0.5f, 0.3f, 0.3f);
		private readonly Color ColorStartOrderDep = new(0.2f, 0.2f, 0.4f);
		private readonly Color ColorEndOrderDep = new(0.3f, 0.3f, 0.5f);
		private readonly Color ColorStartDataDep = new(0.5f, 0.6f, 1f);
		private readonly Color ColorEndDataDep = new(0.5f, 0.6f, 1f);

		private const float DefaultLineWidth = 0.02f;
		private const float MinLineWidth = 0.003f;
		private const float MaxLineWidth = 0.3f;

		private void Awake()
		{
			Instance = this;

			_dependencyPrefab = Resources.Load<GameObject>("Prefabs/Dependency");
		}

		public void Init()
		{
			RemoveAllDependencyLines();

			_currentLineWidth = DefaultLineWidth;
			_currentNumDependenciesShown = 0;
			_cachedCriticalPathDependencies = null;
		}


		/// <summary>
		/// Hides all dependency lines.
		/// </summary>
		public void RemoveAllDependencyLines()
		{
			if (_currentNumDependenciesShown == 0)
				return;

			for (int i = 0; i < _currentNumDependenciesShown; i++)
			{
				DependenciesParent.GetChild(i).gameObject.SetActive(false);
			}

			_currentNumDependenciesShown = 0;
		} 


		/// <summary>
		/// Adjusts the line width of all currently shown dependency lines.
		/// Applies the same width to all dependencies shown in the future.
		/// Clamps the resulting value to within the minLineWidth and maxLineWidth.
		/// </summary>
		/// <param name="scaleFactor">Factor by which to scale the default line width.</param>
		public void SetDependencyLineWidth(float scaleFactor)
		{
			float width = DefaultLineWidth * scaleFactor;
			width = Mathf.Clamp(width, MinLineWidth, MaxLineWidth);

			for (int i = 0; i < _currentNumDependenciesShown; i++)
			{
				LineRenderer lineRenderer = DependenciesParent.GetChild(i).GetComponent<LineRenderer>();
				lineRenderer.startWidth = width;
				lineRenderer.endWidth = width;
			}

			_currentLineWidth = width;
		}


		/// <summary>
		/// Finds all nodes on the critical path and shows the dependencies between them.
		/// </summary>
		/// <returns>A List of all nodes on the critical path in order from last to first node.</returns>
		public List<Node> ShowCriticalPath()
		{
			NodeManager nodeManager = NodeManager.Instance;
			List<Node> nodesOnPath = new();

			List<Dependency<Command>> criticalPathDependencies;
			if (GlobalSettings.BENCHMARK)
			{
				criticalPathDependencies = PerformanceLogger.MeasureExecTime<List<Dependency<Command>>>("critical path computation", () => ComputeCriticalPath());
				Debug.Log("Critical path length: " +  criticalPathDependencies.Count);
			}
			else
				criticalPathDependencies = ComputeCriticalPath();

			// no dependencies found -> early return
			if (criticalPathDependencies.Count == 0)
			{
				Debug.LogWarning("ShowCriticalPath found no dependencies on critical path!");
				return nodesOnPath;
			}

			// add last node on critical path (= origin of first dependency in list)
			nodesOnPath.Add(nodeManager.Nodes[criticalPathDependencies[0].Origin]);

			// go through all dependencies adding their targets to the nodes list
			// also show the dependency lines
			foreach (Dependency<Command> dependency in criticalPathDependencies)
			{
				nodesOnPath.Add(nodeManager.Nodes[dependency.Target]);
				ShowDependencyLine(dependency);
			}

			return nodesOnPath;
		}

		/// <summary>
		/// Computes the critical path of the trace and returns it as a list of <see cref="Dependency{Command}"/>s in order from last to first.
		/// </summary>
		private List<Dependency<Command>> ComputeCriticalPath()
		{
			// check if cached result available
			if (_cachedCriticalPathDependencies != null)
			{
				return _cachedCriticalPathDependencies;
			}


			Trace trace = TimelineManager.Instance.CurrentTrace;

			Command endComm = trace.Commands.Values.Where(x => x.EndTime == trace.MaxEndTime).FirstOrDefault()
				?? throw new ArgumentException("ComputeCriticalPath can't find last command in trace.");

			List<Dependency<Command>> criticalPathDependencies = new();

			// since we already have a the true finish times of all nodes, we can just go backwards from the last one
			// and since the data dependencies are pre-computed in the trace conversion they're automatically included

			Command currentCommand = endComm;
			while (currentCommand != null)
			{
				//Debug.Log("Command " + currentCommand.Id + " with " + currentCommand.Predecessors.Count + " preds");

				// command doesn't have any predecessors -> reached the start
				if (currentCommand.Predecessors.Count == 0)
					break;

				// find predecessor dependency where command has maximium end time and with more than 0 predecessors (if available)
				// also exclude anti-dependencies
				var predecessorCommands = currentCommand.Predecessors.Where(x => x.Kind != DependencyKind.AntiDep).Select(x => x.Target);
				List<Command> predCommsOrderedByEndTime = predecessorCommands.OrderBy(x => x.EndTime).ToList();

				Command maxEndPredCommWithNonZeroPredCount = predCommsOrderedByEndTime.Where(x => x.Predecessors.Count > 0).LastOrDefault();
				Command nextCommand;

				if (maxEndPredCommWithNonZeroPredCount != null)
				{
					nextCommand = maxEndPredCommWithNonZeroPredCount;
				}
				else
				{
					nextCommand = predCommsOrderedByEndTime.Last();
					//Debug.Log("Couldn't find predecessor with non-zero pred count - choosing max end time instead.");
				}

				// get dependency with nextCommand as target
				Dependency<Command> nextDependency = currentCommand.Predecessors.Where(x => x.Target == nextCommand).FirstOrDefault();

				// add dependency to critical path
				criticalPathDependencies.Add(nextDependency);

				currentCommand = nextCommand;
			}

			// cache result
			_cachedCriticalPathDependencies = criticalPathDependencies;

			return criticalPathDependencies;
		}


		/// <summary>
		/// Shows the <paramref name="depth"/> previous and next dependencies for a given <paramref name="node"/>.
		/// This method requires no other dependency lines to be shown in the scene at the time of invocation.
		/// </summary>
		/// <param name="node">Node to use as starting point.</param>
		/// <param name="depth">Number of nodes to recursively show dependencies for. 1 = only for the node itself, 2 for it and its predecessor/successor nodes, etc.</param>
		/// <param name="typeMask">Optional <see cref="CommandType"/> mask to only show dependencies starting/ending at commands of specific types.</param>
		/// <returns>A set of all nodes part of the dependencies. Includes the starting node.</returns>
		/// <exception cref="InvalidOperationException">Thrown if this is called while there are already other dependencies shown.</exception>
		public HashSet<Node> ShowNodeDependenciesRecursive(Node node, int depth, CommandType typeMask = CommandType.All)
		{
			// check if already showing dependencies
			if (_currentNumDependenciesShown != 0)
			{
				throw new InvalidOperationException("ShowNodeDependenciesRecursive called while already showing other dependency lines!");
			}

			HashSet<Dependency<Command>> includedDependencies = new();

			// compute dependencies to be shown, stored in includedDependencies
			ComputeRecursiveDependencies(node.Command, depth, typeMask, includedDependencies, true); // predecessors
			ComputeRecursiveDependencies(node.Command, depth, typeMask, includedDependencies, false); // successors
			
			NodeManager nodeManager = NodeManager.Instance;
			HashSet<Node> containedNodes = new();
			foreach (var dependency in includedDependencies)
			{
				// show lines for the dependencies
				ShowDependencyLine(dependency);

				// compute set of nodes involved in the dependencies by throwing all targets / origins into a set
				containedNodes.Add(nodeManager.Nodes[dependency.Origin]);
				containedNodes.Add(nodeManager.Nodes[dependency.Target]);
			}

			return containedNodes;
		}

		/// <summary>
		/// Computes the dependencies matching the <paramref name="typeMask"/> and within the specified <paramref name="depth"/> starting at the <paramref name="startCommand"/>.
		/// If <paramref name="doComputePredecessors"/> is set, the predecessor dependencies are calculated, if not then the successor ones are.
		/// Stores the results in the <paramref name="includedDependencies"/> HashSet.
		/// </summary>
		private void ComputeRecursiveDependencies(Command startCommand, int depth, CommandType typeMask, HashSet<Dependency<Command>> includedDependencies, bool doComputePredecessors)
		{
			// red/black lists for swapping
			List<Command> commandsA = new(8);
			List<Command> commandsB = new(8);

			// pointers to lists
			List<Command> previousCommands = commandsA;
			List<Command> nextCommands = commandsB;

			// initialize with starting node command
			previousCommands.Add(startCommand);

			// search depth counter
			for (int d = 0; d < depth; d++)
			{
				foreach (Command command in previousCommands)
				{
					// dependencies always go from "later" command (origin) to "earlier" command (target)

					if (doComputePredecessors) // predecessors
					{
						foreach (Dependency<Command> dependency in command.Predecessors)
						{
							// check for the target since going backwards for predecessors
							// skip if other command is wrong type
							if ((dependency.Target.CommandType & typeMask) == 0)
								continue;

							includedDependencies.Add(dependency);
							nextCommands.Add(dependency.Target);
						}
					}
					else // successors
					{
						foreach (Dependency<Command> dependency in command.Successors)
						{
							// check for the origin since we're going "forwards" for successors
							// skip if other command is wrong type
							if ((dependency.Origin.CommandType & typeMask) == 0)
								continue;

							includedDependencies.Add(dependency);
							nextCommands.Add(dependency.Origin);
						}
					}
				}

				// swap lists (very fancy syntax btw)
				(previousCommands, nextCommands) = (nextCommands, previousCommands);
				nextCommands.Clear();
			}
		}


		/// <summary>
		/// Draws a dependency line for the given dependency.
		/// </summary>
		/// <param name="dependency"></param>
		private void ShowDependencyLine(Dependency<Command> dependency)
		{
			if (dependency == null) return;

			GameObject dependencyLineObject = GetNextDependencyLine();
			dependencyLineObject.SetActive(true); //enable / show the line (somewhat important to actually see it)

			LineRenderer lineRenderer = dependencyLineObject.GetComponent<LineRenderer>();

			NodeLayoutPolicy layoutPolicy = GlobalSettings.LAYOUT_POLICY;

			ComputeDependencyLineLocations(dependency.Target, dependency.Origin, dependency.Kind, layoutPolicy, out Vector3 startPosition, out Vector3 endPosition);

			lineRenderer.SetPosition(0, startPosition);
			lineRenderer.SetPosition(1, endPosition);

			lineRenderer.startWidth = _currentLineWidth;
			lineRenderer.endWidth = _currentLineWidth;

			//set color based on dependency type
			switch (dependency.Kind)
			{
				case DependencyKind.TrueDep:
					lineRenderer.startColor = ColorStartTrueDep;
					lineRenderer.endColor = ColorEndTrueDep;
					break;
				case DependencyKind.AntiDep:
					lineRenderer.startColor = ColorStartAntiDep;
					lineRenderer.endColor = ColorEndAntiDep;
					break;
				case DependencyKind.OrderDep:
					lineRenderer.startColor = ColorStartOrderDep;
					lineRenderer.endColor = ColorEndOrderDep;
					break;
				case DependencyKind.DataDep:
					lineRenderer.startColor = ColorStartDataDep;
					lineRenderer.endColor = ColorEndDataDep;
					break;
				default:
					Debug.LogError("ShowDependencyLine depedency kind " + dependency.Kind.ToString() + " not implemented!");
					lineRenderer.startColor = ColorStartTrueDep;
					lineRenderer.endColor = ColorEndTrueDep;
					break;
			}

		}

		/// <summary>
		/// Retruns a reference to the next free dependency line object from the pool.
		/// If no objects are freeeeee will create a new one and add it to the pool.
		/// </summary>
		private GameObject GetNextDependencyLine()
		{
			int totalNumDepenencyLines = DependenciesParent.childCount;

			// safety check
			if (_currentNumDependenciesShown > totalNumDepenencyLines)
			{
				throw new InvalidOperationException("Current number of dependency lines in use exceeds object pool size!");
			}

			GameObject nextDependencyLineObject;

			if (_currentNumDependenciesShown == totalNumDepenencyLines) // pool is exhausted, create new object and add it to the pool
			{
				nextDependencyLineObject = Instantiate(_dependencyPrefab, DependenciesParent);
			}
			else // pool still has sufficient objects, just pick the next one
			{
				nextDependencyLineObject = DependenciesParent.GetChild(_currentNumDependenciesShown).gameObject;
			}
			
			_currentNumDependenciesShown++;
			return nextDependencyLineObject;
		}


		/// <summary>
		/// Computes the start and end positions of the dependency line based on the given dependency and the layout policy.
		/// All returned coordinates are relative to the dependenciesParent transform.
		/// </summary>
		/// <param name="earlierCommand">The command earlier in the timeline of the dependency to draw. Usually the "target".</param>
		/// <param name="laterCommand">The command later in the timeline of the dependency to draw. Usually the "origin".</param>
		/// <param name="dependencyKind">The kind of depedency for which to compute the line positions. Allows some dependency types to start at different positions.</param>
		/// <param name="layoutPolicy">The current layout policy.</param>
		/// <param name="startPosition">The starting position of the dependency line.</param>
		/// <param name="endPosition">The ending position of the dependency line.</param>
		/// <exception cref="NotImplementedException">In case of unimplemented <paramref name="layoutPolicy"/></exception>
		private void ComputeDependencyLineLocations(Command earlierCommand, Command laterCommand, DependencyKind dependencyKind, NodeLayoutPolicy layoutPolicy, out Vector3 startPosition, out Vector3 endPosition)
		{
			Node startNode = NodeManager.Instance.Nodes[earlierCommand];
			Transform startNodeVisualsParent = startNode.GetVisualsParent();
			Node endNode = NodeManager.Instance.Nodes[laterCommand];
			Transform endNodeVisualsParent = endNode.GetVisualsParent();

			// compute start / end positions of dependency line depending on layout policy
			// since dependency parent and node parent are both scaled together through the trace parent no coordinate transformation is needed
			switch (layoutPolicy)
			{
			case NodeLayoutPolicy.LinearMixed:
				if (dependencyKind == DependencyKind.DataDep) // data dependency lines start at center of node visuals
				{
					// = start node local position
					startPosition = startNode.transform.localPosition;
					// = end node local position
					endPosition = endNode.transform.localPosition;
				}
				else // all other start at ends of either node
				{
					// = start node local position + half of x-axis visual scale
					startPosition = startNode.transform.localPosition + new Vector3(startNodeVisualsParent.localScale.x / 2f, 0, 0);
					// = end node local position [-] half of x-axis visual scale
					endPosition = endNode.transform.localPosition - new Vector3(endNodeVisualsParent.localScale.x / 2f, 0, 0);
				}
				break;

			case NodeLayoutPolicy.Circular:
				if (dependencyKind == DependencyKind.DataDep) // data dependency lines start at center of node visuals
				{
					// = start node local position
					startPosition = startNode.transform.localPosition;
					// = end node local position
					endPosition = endNode.transform.localPosition;
				}
				else
				{
					// = start node local position + half of y-axis visual scale
					startPosition = startNode.transform.localPosition + new Vector3(0, startNodeVisualsParent.localScale.y / 2f, 0);
					// = end node local position [-] half of y-axis visual scale
					endPosition = endNode.transform.localPosition - new Vector3(0, endNodeVisualsParent.localScale.y / 2f, 0);
				}
				break;

			default:
				throw new NotImplementedException("Layout policy not yet implemented.");
			}
		}

	}
}