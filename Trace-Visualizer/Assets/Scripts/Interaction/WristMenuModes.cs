using celerity.visualizer.timeline;
using celerity.visualizer.tracedata;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace celerity.visualizer.interaction
{
	/// <summary>
	/// Handles the different display modes that are selected in the wrist menu modes section.
	/// Handles the UI display of the different mode settings panels and delegates the application of the modes to the respecting systems.
	/// </summary>
	public class WristMenuModes : MonoBehaviour
	{
		private enum ModeState
		{
			None,
			TypeFilter,
			RangeFilter,
			RecDep,
			CriticalPath,
			TypeRangeFilter,
			TypeRecDepFilter,
			IdleSec,
			TypeIdleSecFilter,
		}

		public static WristMenuModes Instance { get; private set; }

		[SerializeField] Transform ModesTabParent;

		private readonly Color ButtonColorActive = new(1.0f, 1.0f, 1.0f);
		private readonly Color ButtonColorDisabled = new(0.6f, 0.6f, 0.6f);
		private const ulong MinVicinitySize = 1000; // 0.1sec
		private const ulong MaxVicinitySize = 5000000; // 5sec
		private const int MaxRecDepDepth = 5;
		private const ulong MinMinIdleSectionTime = 100; //0.01sec
		private const ulong MaxMinIdleSectionTime = 10000; //1 sec

		private readonly bool[] modeToggles = new bool[] { false, false, false, false, false };

		// settings for default mode selection on timeline load
		private readonly bool applyDefaultModeSelection = false;
		//												  filter, range, recdep, crit, idle sec
		private readonly bool[] defaultModes = new bool[] { false, false, false, false, false };
		private readonly CommandType defaultTypeFilter = CommandType.Task;

		// CommandTypes in the order of the toggles in the filter toggle 
		private readonly CommandType[] ToggleToTypeMap = new CommandType[]
		{
			CommandType.Task, CommandType.Push, CommandType.AwaitPush, CommandType.Horizon, CommandType.Sync, CommandType.Shutdown, CommandType.Nop
		};

		private int _recDepDepth = 1;
		private ModeState _currentState;
		private Node _lastSelectedNode;

		private void Awake()
		{
			Instance = this;

			Init();
		}

		/// <summary>
		/// Resets the modes system to default.
		/// </summary>
		public void Init()
		{
			for (int i = 0; i < modeToggles.Length; i++)
				modeToggles[i] = false;

			SetRangeSliderValue(0.1f);
			SetRecDepNonDepNodesHidingToggleValue(false);
			SetMinIdleSectionLengthSliderValue(0.2f);
			SetIdleSectionsOnlyTasksToggleValue(false);

			_recDepDepth = 1;
			_currentState = ModeState.None;
		}

		/// <summary>
		/// Applies the default mode selection to the scene on timeline load
		/// </summary>
		public void ApplyDefaultModeSelection()
		{
			if (applyDefaultModeSelection)
			{
				// apply default mode selection
				for (int i = 0; i < defaultModes.Length; i++)
					modeToggles[i] = defaultModes[i];

				// set default type filter mask if filter is active
				if (modeToggles[0])
					SetTypeMaskToToggles(defaultTypeFilter);

				MatchModesButtonStates();
				MatchSettingsPanelsStates();

				ApplyMode();
			}
		}


		#region ButtonEvents

		public void SelectFilterMode()
		{
			modeToggles[0] = !modeToggles[0];

			// disable crit path mode if on
			modeToggles[3] = false;

			MatchModesButtonStates();
			MatchSettingsPanelsStates();
		}

		public void SelectRangeMode()
		{
			modeToggles[1] = !modeToggles[1];

			// make mutex to all modes except filter
			modeToggles[2] = false;
			modeToggles[3] = false;
			modeToggles[4] = false;

			MatchModesButtonStates();
			MatchSettingsPanelsStates();
		}

		public void SelectRecDepMode()
		{
			modeToggles[2] = !modeToggles[2];

			// make mutex to all modes except filter
			modeToggles[1] = false;
			modeToggles[3] = false;
			modeToggles[4] = false;

			MatchModesButtonStates();
			MatchSettingsPanelsStates();
		}

		public void SelectCritPathMode()
		{
			modeToggles[3] = !modeToggles[3];

			// made mutex to all other modes
			modeToggles[0] = false;
			modeToggles[1] = false;
			modeToggles[2] = false;
			modeToggles[4] = false;

			MatchModesButtonStates();
			MatchSettingsPanelsStates();
		}

		public void SelectIdleSectionsMode()
		{
			modeToggles[4] = !modeToggles[4];

			// make mutex to other modes except filter
			modeToggles[1] = false;
			modeToggles[2] = false;
			modeToggles[3] = false;

			MatchModesButtonStates();
			MatchSettingsPanelsStates();
		}

		public void RangeSliderInput()
		{
			// update range text
			MatchRangeSliderText();
		}

		public void MinIdleSectionSizeSliderInput()
		{
			// update size text
			MatchMinIdleSectionSizeSliderText();
		}

		public void DepDepthPlus()
		{
			_recDepDepth++;

			if (_recDepDepth > MaxRecDepDepth)
				_recDepDepth = MaxRecDepDepth;

			MatchRecDepDepthText();
		}

		public void DepDepthMinus()
		{
			_recDepDepth--;

			if (_recDepDepth < 1)
				_recDepDepth = 1;

			MatchRecDepDepthText();
		}

		/// <summary>
		/// Apply button action.
		/// </summary>
		public void ApplyMode()
		{
			ModeState newState = GetStateFromToggles();

			ApplyCurrentModeSettings(newState);
		}
		#endregion //ButtonEvents

		#region ExternalEvents

		/// <summary>
		/// Gets invoked by the <see cref="WristMenuManager"/> when the modes menu is being shown.
		/// Makes sure the UI is in sync with the backend values.
		/// </summary>
		public void ShowModesMenu()
		{
			MatchSettingsPanelsStates();
			MatchModesButtonStates();
			MatchFilterToggleStates();
			MatchRangeSliderText();
			MatchRecDepDepthText();
		}

		/// <summary>
		/// Updates the range filter mode after the timeline was moved to match the new origin location.
		/// Gets called from the <see cref="TraceMover"/> after the timeline was moved.
		/// </summary>
		public void UpdateRangeFilterModeAfterMove()
		{
			if (_currentState == ModeState.RangeFilter)
			{
				ApplyRangeFilter();
			}
			else if (_currentState == ModeState.TypeRangeFilter)
			{
				ApplyRangeAndTypeFilter();
			}
		}

		/// <summary>
		/// Shows the dependencies of the <paramref name="node"/> with the currently active mode settings.
		/// Gets invoked by the <see cref="NodeSelector"/> when a node is selected.
		/// </summary>
		/// <param name="node">Node to show dependencies for.</param>
		public void SelectNode(Node node)
		{
			//Debug.Log("SelectNode on Node " + ((node != null) ? node.Command.Id : "null"));

			if (node == null)
				return;

			NodeDetailsManager.Instance.CreateNodeDetails(node);

			// critical path mode active
			if (_currentState == ModeState.CriticalPath)
			{
				return;
			}

			// highlight selected node
			NodeVisualsManager.Instance.HighlightNode(node);


			// check if filter mode is active
			CommandType typeMask = (_currentState == ModeState.TypeFilter || _currentState == ModeState.TypeRangeFilter || _currentState == ModeState.TypeRecDepFilter || _currentState == ModeState.TypeIdleSecFilter)
				? GetTypeMaskFromToggles() : CommandType.All;

			WristMenuList wristMenuList = WristMenuList.Instance;

			// check if recursive dependencies mode is active (or with filter)
			if (_currentState == ModeState.RecDep || _currentState == ModeState.TypeRecDepFilter)
			{
				// show dependencies for node
				HashSet<Node> dependencyNodes = NodeDependenciesManager.Instance.ShowNodeDependenciesRecursive(node, _recDepDepth, typeMask);

				// hide all other nodes if option enabled
				if (GetRecDepNonDepNodesHidingToggleValue())
					NodeManager.Instance.ShowNodesBySetInCurrentChunks(dependencyNodes);

				// show dependencies in wrist list menu
				wristMenuList.ShowNodeDependenciesList("Command " + node.Command.Id + " Dependencies", node);

				// highlight nodes in dependencies
				foreach (Node n in dependencyNodes)
				{
					NodeVisualsManager.Instance.HighlightNode(n);
				}
			}
			// check if no idle sec list displayed - show buffer accesses in list
			else if (!(_currentState == ModeState.IdleSec || _currentState == ModeState.TypeIdleSecFilter))
			{
				wristMenuList.ShowBufferAccessesList("Command " + node.Command.Id + " Buffer Accesses", node.Command.BufferAccesses);
			}

			_lastSelectedNode = node;
		}

		/// <summary>
		/// Hides all dependencies and un-highlights all nodes, and removes details from the <paramref name="node"/>.
		/// Gets invoked by the <see cref="NodeSelector"/> when either a new node is selected or the current node gets de-selected.
		/// </summary>
		/// <param name="node">Node that was deselected.</param>
		public void DeselectNode(Node node)
		{
			//Debug.Log("DeselectNode on Node " + ((node != null) ? node.Command.Id : "null"));

			if (node == null)
				return;

			_lastSelectedNode = null;

			// unhighlight nodes / remove dependencies only if not in critical path mode
			if (_currentState != ModeState.CriticalPath)
			{
				NodeVisualsManager.Instance.UnHighlightAllNodes();
				NodeDependenciesManager.Instance.RemoveAllDependencyLines();
			}

			NodeDetailsManager.Instance.RemoveNodeDetails(node);

			// check if rec dep mode is active and nodes are currently being shown by set
			if ((_currentState == ModeState.RecDep || _currentState == ModeState.TypeRecDepFilter) && NodeManager.Instance.IsShowingNodesBySet)
			{
				if (_currentState == ModeState.TypeRecDepFilter)
				{
					// show nodes by filter instead
					NodeManager.Instance.ShowNodesByTypeInCurrentChunks(GetTypeMaskFromToggles(), false);
				}
				else
				{
					// unhide all nodes
					NodeManager.Instance.ShowAllNodesInCurrentChunks();
				}

				//NodeManager.Instance.ShowNodesByType(typeMask, false);
			}

			// check if critical path mode not active - clear node buffer/dependency list
			if (_currentState != ModeState.CriticalPath)
			{
				WristMenuList wristMenuList = WristMenuList.Instance;
				wristMenuList.ClearList();
			}
		}

		#endregion //ExternalEvents

		/// <summary>
		/// Handles state transitions between mode states.
		/// </summary>
		/// <param name="newState">The new state to be applied.</param>
		private void ApplyCurrentModeSettings(ModeState newState)
		{
			Debug.Log("Applying mode change from " + _currentState + " to " + newState);

			// re-applying same state
			if (newState == _currentState)
			{
				switch (_currentState)
				{
					// type filter might have updated type settings
					case ModeState.TypeFilter:
						break;

					// range filters might have updated range settings
					case ModeState.RangeFilter:
					case ModeState.TypeRangeFilter:
						break;

					// recdep mode might have changed settings
					case ModeState.RecDep:
					case ModeState.TypeRecDepFilter:
						break;

					// idle sec mode might have changed task-only / type settings
					case ModeState.IdleSec:
					case ModeState.TypeIdleSecFilter:
						break;

					// skip re-apply for all others
					default:
						return;
				}
			}

			ResetNodeHighlighting();

			// if was in critical path mode - remove node depdenceny lines
			if (_currentState == ModeState.CriticalPath)
			{
				NodeDependenciesManager.Instance.RemoveAllDependencyLines();
			}
			// if was displaying idle sections and aren't anymore - remove idle sections
			else if ((_currentState == ModeState.IdleSec || _currentState == ModeState.TypeIdleSecFilter) && (newState != ModeState.IdleSec && newState != ModeState.TypeIdleSecFilter))
			{
				SectionsManager.Instance.RemoveAllSections();
			}
			// if was using range filter and aren't anymore - clear range filter display
			else if ((_currentState == ModeState.RangeFilter || _currentState == ModeState.TypeRangeFilter) && (newState != ModeState.RangeFilter && newState != ModeState.TypeRangeFilter))
			{
				ApplyNoRangeFilter();
			}

			// call apply function for the new mode
			switch (newState)
			{
				case ModeState.None:
					ApplyNoTypeFilter();
					break;

				case ModeState.TypeFilter:
					ApplyTypeFilter();
					break;

				case ModeState.RangeFilter:
					ApplyRangeFilter();
					break;

				case ModeState.RecDep:
					// if switching from type filter -> clear filter
					if (_currentState == ModeState.TypeRecDepFilter)
						ApplyNoTypeFilter();
					break;

				case ModeState.CriticalPath:
					ApplyCriticalPathMode();
					break;

				case ModeState.TypeRangeFilter:
					ApplyRangeAndTypeFilter();
					break;

				case ModeState.TypeRecDepFilter:
					ApplyTypeFilter();
					break;

				case ModeState.IdleSec:
					// if swtiching from type filter -> clear filter
					if (_currentState == ModeState.TypeIdleSecFilter)
						ApplyNoTypeFilter();
					ApplyIdleSectionsMode();
					break;

				case ModeState.TypeIdleSecFilter:
					ApplyTypeFilter();
					if (_currentState == ModeState.IdleSec)
						break;
					ApplyIdleSectionsMode();
					break;
			}


			// re-select node if re-applying specific modes
			if (_currentState == newState)
			{
				switch (_currentState)
				{
					case ModeState.RecDep:
					case ModeState.TypeRecDepFilter:
						Node lastNode = _lastSelectedNode;
						DeselectNode(lastNode);
						SelectNode(lastNode);
						break;

					// skip re-select for all others
					default:
						ForceDeselectLastNode();
						break;
				}
			}

			_currentState = newState;
		}


		/// <summary>
		/// Calls the <see cref="NodeDetailsManager"/>, <see cref="NodeDependenciesManager"/>, and <see cref="NodeVisualsManager"/> to remove all node selections / highlightings.
		/// Gets called when a new mode setting is applied since the highlighted nodes may no longer be visible.
		/// </summary>
		private void ResetNodeHighlighting()
		{
			NodeVisualsManager nodeVisualsManager = NodeVisualsManager.Instance;
			nodeVisualsManager.UnHighlightAllNodes();

			NodeDetailsManager nodeDetailsManager = NodeDetailsManager.Instance;
			nodeDetailsManager.RemoveAllNodeDetails();

			NodeDependenciesManager nodeDependenciesManager = NodeDependenciesManager.Instance;
			nodeDependenciesManager.RemoveAllDependencyLines();

			WristMenuList wristMenuList = WristMenuList.Instance;
			wristMenuList.ClearList();
		}

		/// <summary>
		/// Force resets the last selected node.
		/// Gets called after clearing all other mode/highlighting displays if no re-select is wanted.
		/// </summary>
		private void ForceDeselectLastNode()
		{
			_lastSelectedNode = null;

			NodeSelector nodeSelector = NodeSelector.Instance;
			nodeSelector.ClearSelectedNode();
		}

		#region UIMatchingFunctions

		/// <summary>
		/// Matches the active state of the different settings panels to the mode toggles.
		/// </summary>
		private void MatchSettingsPanelsStates()
		{
			GetToggleParent().gameObject.SetActive(modeToggles[0]);
			GetRangeSliderParent().gameObject.SetActive(modeToggles[1]);
			GetRecDepParent().gameObject.SetActive(modeToggles[2]);
			GetCritPathParent().gameObject.SetActive(modeToggles[3]);
			GetIdleSectionsParent().gameObject.SetActive(modeToggles[4]);
		}

		/// <summary>
		/// Updates the color of the mode buttons to match the current mode state.
		/// </summary>
		private void MatchModesButtonStates()
		{
			for (int i = 0; i < modeToggles.Length; i++)
			{
				ModesTabParent.GetChild(0).GetChild(i).GetComponent<Image>().color = modeToggles[i] ? ButtonColorActive : ButtonColorDisabled;
			}
		}

		/// <summary>
		/// Updates the states of all type filter toggles to match the current type mask of the <see cref="NodeManager"/>.
		/// </summary>
		private void MatchFilterToggleStates()
		{
			Transform toggleParent = GetToggleParent();
			NodeManager nodeManager = NodeManager.Instance;
			CommandType currentTypeMask = nodeManager.CurrentTypeMask;

			// go through all toggles in list and set state depending on if their corresponding type is in the current mask
			for (int i = 0; i < ToggleToTypeMap.Length; i++)
			{
				toggleParent.GetChild(i).GetComponent<Toggle>().isOn = (currentTypeMask & ToggleToTypeMap[i]) != 0;
			}
		}

		/// <summary>
		/// Matches the range slider text to the current value on the slider.
		/// </summary>
		private void MatchRangeSliderText()
		{
			Transform rangeSliderParent = GetRangeSliderParent();
			TMP_Text rangeText = rangeSliderParent.GetChild(2).GetComponent<TMP_Text>();

			ulong vicinitySize = GetVicinitySizeFromRangeSlider();

			rangeText.text = string.Format("{0:0.00}", vicinitySize / GlobalSettings.TimestampToSecondsConversionFactor);
		}

		/// <summary>
		/// Matches the min idle section size text to the current value on the slider.
		/// </summary>
		private void MatchMinIdleSectionSizeSliderText()
		{
			Transform parent = GetIdleSectionsParent();
			TMP_Text sizeText = parent.GetChild(2).GetComponent<TMP_Text>();

			ulong minSize = GetMinIdleSectionLengthFromSlider();

			sizeText.text = string.Format("{0:0.000} sec.", minSize / GlobalSettings.TimestampToSecondsConversionFactor);

		}

		/// <summary>
		/// Matches the recdep depth text to the current <see cref="_recDepDepth"/> value.
		/// </summary>
		private void MatchRecDepDepthText()
		{
			Transform recDepParent = GetRecDepParent();

			recDepParent.GetChild(4).GetComponent<TMP_Text>().text = _recDepDepth.ToString();
		}

		#endregion //UIMatchingFunctions

		#region ModeApplicationFunctions

		/// <summary>
		/// Extracts the type mask defined by the filter toggles and applies it to the <see cref="NodeManager"/>.
		/// </summary>
		private void ApplyTypeFilter()
		{
			NodeManager nodeManager = NodeManager.Instance;
			CommandType currentTypeMask = nodeManager.CurrentTypeMask;

			CommandType newTypeMask = GetTypeMaskFromToggles();

			if (currentTypeMask == newTypeMask && // no change in mask
					(nodeManager.CurrentVicinityCenter == null || nodeManager.CurrentVicinitySize == null) && // not switching from range mode
					(!nodeManager.IsShowingNodesBySet) // not showing nodes by set
				)
			{
				return;
			}

			nodeManager.ShowNodesByTypeInCurrentChunks(newTypeMask, true);

			//nodeManager.ShowNodesByType(newTypeMask);
		}

		/// <summary>
		/// Sets the node manager to display all nodes, corresponding to no type filter being applied.
		/// </summary>
		private void ApplyNoTypeFilter()
		{
			NodeManager nodeManager = NodeManager.Instance;
			CommandType currentTypeMask = nodeManager.CurrentTypeMask;

			if (currentTypeMask != CommandType.All || //incorrect mask applied
				nodeManager.IsShowingNodesBySet) //currently showing nodes by set
			{
				nodeManager.ShowAllNodesInCurrentChunks();
				//nodeManager.ShowNodesByType(CommandType.All);
			}
		}

		/// <summary>
		/// Applies the range/vicinity filter with the given vicinity size.
		/// </summary>
		/// <param name="vicinitySize">Size of the vicinity in microseconds.</param>
		/// <param name="typeMask">Optional mask for command types to be shown.</param>
		private void ApplyRangeFilter(ulong vicinitySize, CommandType? typeMask = null)
		{
			NodeManager nodeManager = NodeManager.Instance;

			ulong timeAtOrigin = TraceMover.Instance.GetTimelineTimeAtOrigin();

			ulong? currentVicinityCenter = nodeManager.CurrentVicinityCenter;
			ulong? currentVicinitySize = nodeManager.CurrentVicinitySize;
			CommandType currentMask = nodeManager.CurrentTypeMask;

			// no change in arrangement - don't re-apply
			if (currentVicinityCenter != null && currentVicinityCenter == timeAtOrigin &&
				currentVicinitySize != null && currentVicinitySize == vicinitySize &&
				typeMask != null && currentMask == typeMask.Value)
				return;

			//Debug.Log(string.Format("range filter with mask {0,2:X}", typeMask));

			nodeManager.ShowChunksInVicinity(timeAtOrigin, vicinitySize, typeMask);

			/* TODO remove
			// if filter changed - call filter show method first to reposition nodes
			if (currentMask != typeMask.Value)
				nodeManager.ShowNodesByType(typeMask.Value, true);

			nodeManager.ShowNodesInVicinity(timeAtOrigin, vicinitySize, typeMask);
			*/
		}

		/// <summary>
		/// Applies the range filter with no type filters.
		/// </summary>
		private void ApplyRangeFilter()
		{
			//ApplyRangeFilter(GetVicinitySizeFromRangeSlider());
			ApplyRangeFilter(GetVicinitySizeFromRangeSlider(), CommandType.All);
		}

		/// <summary>
		/// Applies the range filter with the current type filter settings.
		/// </summary>
		private void ApplyRangeAndTypeFilter()
		{
			ApplyRangeFilter(GetVicinitySizeFromRangeSlider(), GetTypeMaskFromToggles());
		}

		/// <summary>
		/// Applies no range filter. Acts as a reset from any of the Filter modes.
		/// </summary>
		private void ApplyNoRangeFilter()
		{
			NodeManager nodeManager = NodeManager.Instance;
			nodeManager.ShowAllChunks();
		}

		/// <summary>
		/// Applies the critical path mode.
		/// </summary>
		private void ApplyCriticalPathMode()
		{
			NodeDependenciesManager nodeDependenciesManager = NodeDependenciesManager.Instance;

			List<Node> nodes = nodeDependenciesManager.ShowCriticalPath();

			// highlight nodes on critical path
			foreach (Node n in nodes)
			{
				NodeVisualsManager.Instance.HighlightNode(n);
			}

			Debug.Log("ShowCriticalPathList with " + nodes.Count + " nodes");

			// pass nodes to list tab for display
			WristMenuList wristMenuList = WristMenuList.Instance;

			wristMenuList.ShowCriticalPathList("Critical Path", nodes);

		}

		/// <summary>
		/// Applies the idle sections mode.
		/// </summary>
		private void ApplyIdleSectionsMode()
		{
			ulong minIdleSectionSize = GetMinIdleSectionLengthFromSlider();
			bool onlyConsiderTaskNodes = GetIdleSectionsOnlyTasksToggleValue();

			Debug.Log("apply idle sections mode with section size " + minIdleSectionSize + " and onlyTaskNodes " + onlyConsiderTaskNodes);

			List<SectionsManager.SecData> sections;

			if (onlyConsiderTaskNodes)
			{
				sections = SectionsManager.Instance.CreateTaskIdleSections(TimelineManager.Instance.CurrentTrace, GlobalSettings.LAYOUT_POLICY, minIdleSectionSize);
			}
			else
			{
				sections = SectionsManager.Instance.CreateIdleSections(TimelineManager.Instance.CurrentTrace, GlobalSettings.LAYOUT_POLICY, minIdleSectionSize);
			}

			// sort sections by start time
			sections.Sort((x, y) => { return x.StartTime.CompareTo(y.StartTime); });

			string listTitle = onlyConsiderTaskNodes ? "Task Idle Sections" : "Idle Sections";

			WristMenuList wristMenuList = WristMenuList.Instance;
			wristMenuList.ShowIdleSectionsList(listTitle, sections);
		}


		#endregion //ModeApplicationFunctions

		#region UtilityFunctions

		/// <summary>
		/// Decodes the toggle states and combines them into a <see cref="CommandType"/> mask.
		/// </summary>
		private CommandType GetTypeMaskFromToggles()
		{
			Transform toggleParent = GetToggleParent();
			CommandType newTypeMask = CommandType.None;

			// go through all toggles in list and update the type mask depending on their state
			for (int i = 0; i < ToggleToTypeMap.Length; i++)
			{
				if (toggleParent.GetChild(i).GetComponent<Toggle>().isOn)
					newTypeMask |= ToggleToTypeMap[i];
			}
			return newTypeMask;
		}

		/// <summary>
		/// ONLY FOR USE WITH DEFAULT MODE SELECTION
		/// Sets the type mask selection toggles to match the supplied command type mask.
		/// </summary>
		private void SetTypeMaskToToggles(CommandType mask)
		{
			Transform toggleParent = GetToggleParent();

			// go through all toggles in list and update their state depending on the mask
			for (int i = 0; i < ToggleToTypeMap.Length; i++)
			{
				toggleParent.GetChild(i).GetComponent<Toggle>().isOn = (mask & ToggleToTypeMap[i]) != CommandType.None;
			}
		}

		/// <summary>
		/// Computes the vicinity size from the current range slider value.
		/// Linearly interpolates between the <see cref="MinVicinitySize"/> and <see cref="MaxVicinitySize"/>.
		/// </summary>
		private ulong GetVicinitySizeFromRangeSlider()
		{
			Transform rangeSliderParent = GetRangeSliderParent();
			Slider rangeSlider = rangeSliderParent.GetChild(1).GetComponent<Slider>();

			double sliderValue = rangeSlider.value;

			ulong range = MaxVicinitySize - MinVicinitySize;
			ulong vicinitySize = (ulong)(sliderValue * range) + MinVicinitySize;

			return vicinitySize;
		}

		/// <summary>
		/// Sets the value of the range slider. Should only be used for initialization.
		/// </summary>
		private void SetRangeSliderValue(float value)
		{
			value = Mathf.Clamp(value, 0, 1);

			Transform rangeSliderParent = GetRangeSliderParent();
			Slider rangeSlider = rangeSliderParent.GetChild(1).GetComponent<Slider>();
			rangeSlider.value = value;
		}

		/// <summary>
		/// Computes the minimum idle section length value from the slider.
		/// Linearly interpolates between <see cref="MinMinIdleSectionTime"/> and <see cref="MaxMinIdleSectionTime"/>.
		/// </summary>
		/// <returns></returns>
		private ulong GetMinIdleSectionLengthFromSlider()
		{
			Transform parent = GetIdleSectionsParent();
			Slider lengthSlider = parent.GetChild(1).GetComponent<Slider>();

			double sliderValeu = lengthSlider.value;

			ulong range = MaxMinIdleSectionTime - MinMinIdleSectionTime;
			ulong size = (ulong)(sliderValeu * range) + MinMinIdleSectionTime;

			return size;
		}

		/// <summary>
		/// Sets the value of the min idle section length slider. Should only be used for initialization.
		/// </summary>
		private void SetMinIdleSectionLengthSliderValue(float value)
		{
			Transform parent = GetIdleSectionsParent();
			Slider lengthSlider = parent.GetChild(1).GetComponent<Slider>();
			lengthSlider.value = value;
		}

		/// <summary>
		/// Returns the state defined by the current mode toggle arrangement.
		/// </summary>
		private ModeState GetStateFromToggles()
		{
			if (modeToggles[0])
			{
				if (modeToggles[1])
					return ModeState.TypeRangeFilter;
				else if (modeToggles[2])
					return ModeState.TypeRecDepFilter;
				else if (modeToggles[4])
					return ModeState.TypeIdleSecFilter;
				else
					return ModeState.TypeFilter;
			}
			else if (modeToggles[1])
			{
				return ModeState.RangeFilter;
			}
			else if (modeToggles[2])
			{
				return ModeState.RecDep;
			}
			else if (modeToggles[3])
			{
				return ModeState.CriticalPath;
			}
			else if (modeToggles[4])
			{
				return ModeState.IdleSec;
			}
			else
			{
				return ModeState.None;
			}
		}

		/// <summary>
		/// Sets the value of the recursive dependencies non-dependent node hiding toggle.
		/// </summary>
		/// <param name="value">Value to be set.</param>
		private void SetRecDepNonDepNodesHidingToggleValue(bool value)
		{
			GetRecDepParent().GetChild(5).GetComponent<Toggle>().isOn = value;
		}

		/// <summary>
		/// Returns the current value of the recursive dependencies non-dependent node hiding toggle.
		/// </summary>
		private bool GetRecDepNonDepNodesHidingToggleValue()
		{
			return GetRecDepParent().GetChild(5).GetComponent<Toggle>().isOn;
		}

		/// <summary>
		/// Sets the value of the idle sections only consider task nodes toggle.
		/// </summary>
		/// <param name="value"></param>
		private void SetIdleSectionsOnlyTasksToggleValue(bool value)
		{
			GetIdleSectionsParent().GetChild(3).GetComponent<Toggle>().isOn = value;
		}

		/// <summary>
		/// Returns the current value of the idle sections only consider task nodes toggle.
		/// </summary>
		private bool GetIdleSectionsOnlyTasksToggleValue()
		{
			return GetIdleSectionsParent().GetChild(3).GetComponent<Toggle>().isOn;
		}


		// -----
		// Extracted methods for referencing different transforms within the modes tab.
		// Allows for a one-place change if the structure of the tab is modified.

		private Transform GetToggleParent()
		{
			return ModesTabParent.GetChild(1).GetChild(0);
		}

		private Transform GetRangeSliderParent()
		{
			return ModesTabParent.GetChild(1).GetChild(1);
		}

		private Transform GetRecDepParent()
		{
			return ModesTabParent.GetChild(1).GetChild(2);
		}

		private Transform GetCritPathParent()
		{
			return ModesTabParent.GetChild(1).GetChild(3);
		}

		private Transform GetIdleSectionsParent()
		{
			return ModesTabParent.GetChild(1).GetChild(4);
		}

		#endregion //UtilityFunctions
	}
}
