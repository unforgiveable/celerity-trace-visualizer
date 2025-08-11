using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using celerity.visualizer.timeline;
using celerity.visualizer.ui;
using celerity.visualizer.tracedata;
using System;
using UnityEngine.UI;
using TMPro;
using static UnityEngine.GraphicsBuffer;

namespace celerity.visualizer.interaction
{
	/// <summary>
	/// Manages the wrist list menu.
	/// Interface is built to be re-usable for different types of lists, such as Node depndencies or buffer accesses.
	/// </summary>
	public class WristMenuList : MonoBehaviour
	{
		private enum ListType
		{
			None,
			NodeDependencies,
			CriticalPathNodes,
			BufferAccesses,
			IdleSections
		}

		public static WristMenuList Instance { get; private set; }

		[SerializeField] Transform ListTabParent;

		private GameObject _listItemPrefab;

		private ListType _currentType = ListType.None;
		private string _currentTitle;
		private int _numEntries = 0;

		private const float ListItemHeight = 1.5f;
		private readonly Color DataDepListItemColor = new(0.5f, 0.6f, 1f, 1f);

		private void Awake()
		{
			Instance = this;

			_listItemPrefab = Resources.Load<GameObject>("Prefabs/UI/ListItem");

			ClearList();
		}

		/// <summary>
		/// Shows all node dependencies for the given <paramref name="rootNode"/> ordered from earliest to latest.
		/// Clears a previously shown list if present.
		/// </summary>
		/// <param name="title">Title of the list.</param>
		/// <param name="rootNode">Root node from which the dependencies originate.</param>
		public void ShowNodeDependenciesList(string title, Node rootNode)
		{
			if (CheckIfAlreadyShown(ListType.NodeDependencies, title)) return;

			GetTitle().text = title;
			SetColumnHeaders("COMM. ID", "DEPENDENCY TYPE", "TIME DIFF.");
			
			List<Tuple<long, Command, DependencyKind>> dependencies = new();
			Command self = rootNode.Command;

			// add predecessors
			foreach (var dep in rootNode.Command.Predecessors)
			{
				Command other = dep.Target;

				long timeDifference;
				if (dep.Kind == DependencyKind.DataDep)
				{
					// for data dependencies compute time difference between midpoint of both commands
					long selfMidPoint = (long)((self.EndTime - self.StartTime) / 2L + self.StartTime);
					long otherMidPoint = (long)((other.EndTime - other.StartTime)/2L + other.StartTime);
					timeDifference = otherMidPoint - selfMidPoint;
				}
				else
				{
					// for all other compute difference between other ending and self starting time
					timeDifference = (long)other.EndTime - (long)self.StartTime;
				}

				dependencies.Add(new Tuple<long, Command, DependencyKind>(timeDifference, dep.Target, dep.Kind));
			}

			// add successors
			foreach (var dep in rootNode.Command.Successors)
			{
				Command other = dep.Origin;

				long timeDifference;
				if (dep.Kind == DependencyKind.DataDep)
				{
					// for data dependencies compute time difference between midpoint of both commands
					long selfMidPoint = (long)((self.EndTime - self.StartTime) / 2L + self.StartTime);
					long otherMidPoint = (long)((other.EndTime - other.StartTime) / 2L + other.StartTime);
					timeDifference = otherMidPoint - selfMidPoint;
				}
				else
				{
					// for all other compute difference between other starting and self ending time
					timeDifference = (long)other.StartTime - (long)self.EndTime;
				}

				dependencies.Add(new Tuple<long, Command, DependencyKind>(timeDifference, dep.Origin, dep.Kind));
			}

			// sort list of dependencies by time difference ascending
			// pin data dependencies to the top by modifying the comparison method
			dependencies.Sort((x, y) => 
				{
					if (x.Item3 == DependencyKind.DataDep) return -1;
					else if (y.Item3 == DependencyKind.DataDep) return 1;
					else return x.Item1.CompareTo(y.Item1);
				}
			);

			// create list entries
			foreach (Tuple<long, Command, DependencyKind> tuple in dependencies)
			{
				string timeDiffText = string.Format("{0:000.00}ms", tuple.Item1 / 1000f);

				Node n = NodeManager.Instance.Nodes[tuple.Item2];

				if (tuple.Item3 == DependencyKind.DataDep) // change list entry background color for data dependencies
				{
					CreateEntry(tuple.Item2.Id.ToString(), tuple.Item3.ToString(), timeDiffText, () => { TraceMover.Instance.CenterTraceOnNode(n); }, DataDepListItemColor);
				}
				else
				{
					CreateEntry(tuple.Item2.Id.ToString(), tuple.Item3.ToString(), timeDiffText, () => { TraceMover.Instance.CenterTraceOnNode(n); });
				}
			}

			MatchContentParentSize();
			ResetScrollBarPosition();
		}


		/// <summary>
		/// Shows the list of buffer accesses.
		/// Clears a previously shown list if present.
		/// </summary>
		/// <param name="title">Title of the list.</param>
		/// <param name="bufferAccesses">List of <see cref="BufferAccess"/>es to be shown.</param>
		public void ShowBufferAccessesList(string title, List<BufferAccess> bufferAccesses)
		{
			if (CheckIfAlreadyShown(ListType.BufferAccesses, title)) return;

			GetTitle().text = title;
			SetColumnHeaders("BUFFER ID", "ACCESS RANGE", "ACC. TYPE");

			// create entries
			foreach (BufferAccess bufferAccess in bufferAccesses)
			{
				CreateEntry(bufferAccess.Buffer.Id.ToString(), bufferAccess.Start + "->" + bufferAccess.End, bufferAccess.AccessMode.ToString(), () => {  });
			}

			MatchContentParentSize();
			ResetScrollBarPosition();
		}

		/// <summary>
		/// Shows the list of nodes on the critical path.
		/// Clears a previously shown list if present.
		/// </summary>
		/// <param name="title">Title of the list.</param>
		/// <param name="nodes">Ordered list of nodes to be shown.</param>
		public void ShowCriticalPathList(string title, List<Node> nodes)
		{
			if (CheckIfAlreadyShown(ListType.CriticalPathNodes, title)) return;

			GetTitle().text = title;
			SetColumnHeaders("COMM. ID", "START TIME", "COMM. TYPE");

			// create entries
			for (int i = nodes.Count - 1; i >= 0; i--)
			{
				Node node = nodes[i];
				string startTime = string.Format("{0:000.00}ms", node.Command.StartTime / 1000f);

				CreateEntry(node.Command.Id.ToString(), startTime, node.Command.CommandType.ToString(), () => { TraceMover.Instance.CenterTraceOnNode(node); });
			}

			MatchContentParentSize();
			ResetScrollBarPosition();
		}

		/// <summary>
		/// Shows the list of sections.
		/// Clears a previously shown list if present.
		/// </summary>
		/// <param name="title">Title of the list.</param>
		/// <param name="sections">Ordered list of sections to be shown.</param>
		public void ShowIdleSectionsList(string title, List<SectionsManager.SecData> sections)
		{
			if (CheckIfAlreadyShown(ListType.IdleSections, title)) return;

			GetTitle().text = title;
			SetColumnHeaders("NODE ID", "DURATION", "START TIME");

			// create entries
			for (int i = sections.Count - 1; i >= 0; i--)
			{
				SectionsManager.SecData section = sections[i];
				string startTime = string.Format("{0:000.00}ms", section.StartTime / 1000f);
				string duration = string.Format("{0:000.00}ms", (section.EndTime - section.StartTime) / 1000f);
				ulong centerTime = section.StartTime + ((section.EndTime - section.StartTime) / 2);

				CreateEntry(section.ComputeNodeId.ToString(), duration, startTime, () => { TraceMover.Instance.CenterTraceOnTime(centerTime); });
			}

			MatchContentParentSize();
			ResetScrollBarPosition();
		}

		/// <summary>
		/// Clears the currently shown list.
		/// </summary>
		public void ClearList()
		{
			_currentTitle = string.Empty;
			_currentType = ListType.None;

			GetTitle().text = "-";
			SetColumnHeaders("-", "-", "-");

			RemoveAllItems();
		}

		/// <summary>
		/// Checks if the specified list combination is already being shown. Sets the <see cref="_currentType"/> and <see cref="_currentTitle"/> and clears the list if its a new combination.
		/// </summary>
		/// <returns>True if the list is already shown, false otherwise.</returns>
		private bool CheckIfAlreadyShown(ListType type, string title)
		{
			if (type == ListType.None || string.IsNullOrEmpty(title))
			{
				throw new ArgumentException("WristMenuList setup with invalid type or title.");
			}

			//check if same list, otherwise remove old one
			if (type == _currentType && title == _currentTitle) return true;

			_currentType = type;
			_currentTitle = title;

			RemoveAllItems();

			return false;
		}

		/// <summary>
		/// Creates a new list entry with the given strings in each of the 3 text fields and the clickAction to be called when the entry is clicked.
		/// </summary>
		/// <param name="first">Text in the left column.</param>
		/// <param name="second">Text in the center column.</param>
		/// <param name="third">Text in the right column.</param>
		/// <param name="clickAction">Action to be performed when the user clicks on the list entry.</param>
		/// <param name="backgroundColor">Color of the background for this list entry.</param>
		private void CreateEntry(string first, string second, string third, Action clickAction, Color backgroundColor)
		{
			GameObject newEntry = Instantiate(_listItemPrefab);

			Transform contentParent = GetListContentParent();

			newEntry.transform.SetParent(contentParent, false);
			newEntry.GetComponent<RectTransform>().anchoredPosition = new Vector2(newEntry.GetComponent<RectTransform>().anchoredPosition.x, -(ListItemHeight * _numEntries));

			newEntry.GetComponent<ListItem>().ClickAction = clickAction;
			newEntry.transform.GetChild(0).GetComponent<TMP_Text>().text = first;
			newEntry.transform.GetChild(1).GetComponent<TMP_Text>().text = second;
			newEntry.transform.GetChild(2).GetComponent<TMP_Text>().text = third;

			newEntry.GetComponent<Image>().color = backgroundColor;

			_numEntries++;
		}

		/// <summary>
		/// Creates a new list entry with the given strings in each of the 3 text fields and the clickAction to be called when the entry is clicked.
		/// </summary>
		/// <param name="first">Text in the left column.</param>
		/// <param name="second">Text in the center column.</param>
		/// <param name="third">Text in the right column.</param>
		/// <param name="clickAction">Action to be performed when the user clicks on the list entry.</param>
		private void CreateEntry(string first, string second, string third, Action clickAction)
		{
			CreateEntry(first, second, third, clickAction, new Color(0.9f, 0.9f, 0.9f, 1f));
		}

		/// <summary>
		/// Sets the column header strings to the provided ones.
		/// </summary>
		private void SetColumnHeaders(string first, string second, string third)
		{
			Transform headerParent = GetHeaderParent();
			headerParent.GetChild(0).GetComponent<TMP_Text>().text = first;
			headerParent.GetChild(1).GetComponent<TMP_Text>().text = second;
			headerParent.GetChild(2).GetComponent<TMP_Text>().text = third;
		}

		/// <summary>
		/// Matches the size of the list content partent to the current number of entries.
		/// </summary>
		private void MatchContentParentSize()
		{
			Transform contentParent = GetListContentParent();
			contentParent.GetComponent<RectTransform>().sizeDelta = new Vector2(contentParent.GetComponent<RectTransform>().sizeDelta.x, _numEntries * ListItemHeight);
		}

		/// <summary>
		/// Resets the scrollbar position to the top of the list.
		/// </summary>
		private void ResetScrollBarPosition()
		{
			GetListScrollbar().value = 1;
		}

		/// <summary>
		/// Removes all list items from the content field.
		/// </summary>
		private void RemoveAllItems()
		{
			Transform contentParent = GetListContentParent();
			for (int i = contentParent.childCount - 1; i >= 0; i--)
			{
				Destroy(contentParent.GetChild(i).gameObject);
			}
			contentParent.GetComponent<RectTransform>().sizeDelta = new Vector2(contentParent.GetComponent<RectTransform>().sizeDelta.x, 0);
			_numEntries = 0;
		}

		private TMP_Text GetTitle()
		{
			return ListTabParent.GetChild(0).GetComponent<TMP_Text>();
		}

		private Transform GetHeaderParent()
		{
			return ListTabParent.GetChild(1);
		}

		private Transform GetListContentParent()
		{
			return ListTabParent.GetChild(2).GetChild(0).GetChild(0);
		}

		private Scrollbar GetListScrollbar()
		{
			return ListTabParent.GetChild(2).GetChild(1).GetComponent<Scrollbar>();
		}


	}
}
