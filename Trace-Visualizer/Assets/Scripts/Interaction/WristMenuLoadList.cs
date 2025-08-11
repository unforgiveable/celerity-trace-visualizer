using celerity.visualizer.ui;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using celerity.visualizer.timeline;

namespace celerity.visualizer.interaction
{
	/// <summary>
	/// Manages the list menu for loading traces.
	/// Creates list entries for all trace files found in the folder and calls the <see cref="TimelineManager"/> with the selected trace name to load.
	/// </summary>
	public class WristMenuLoadList : MonoBehaviour
	{
		public static WristMenuLoadList Instance { get; private set; }

		[SerializeField] Transform LoadListTabParent;

		private const float ListItemHeight = 1.5f;
		private const string TraceFolderName = @"./traces/";
		private readonly Color EntryColorBase = new(0.9f, 0.9f, 0.9f);
		private readonly Color EntryColorHighlight = new(0.6f, 0.6f, 1f);

		private GameObject _listItemPrefab;
		private int _numEntries = 0;
		private int _currentSelectedEntry = -1;
		private string[] _currentFilePathsList;

		private void Awake()
		{
			Instance = this;

			_listItemPrefab = Resources.Load<GameObject>("Prefabs/UI/LoadListItem");

			RemoveAllItems();

			// match layout toggle to current setting
			Toggle layoutModeToggle = GetLayoutModeToggle();
			layoutModeToggle.isOn = GlobalSettings.LAYOUT_POLICY == NodeLayoutPolicy.Circular;
			LayoutModeToggleAction();
		}

		/// <summary>
		/// Shows a list of all trace files found in the specified traces folder.
		/// </summary>
		public void ShowTracesList()
		{
			if (_numEntries > 0)
			{
				RemoveAllItems();
			}

			// load list of files
			var files = GetTraceFileNames();
			Array.Sort(files);

			// create list entry for each trace file
			for (int i = 0; i < files.Length; i++)
			{
				string traceFileName = files[i].Replace(TraceFolderName, "");

				int index = i;
				CreateEntry(traceFileName, () =>
				{
					Debug.Log("Select entry index " + index);
					SelectEntry(index);
				});
			}

			_currentFilePathsList = files;
			_numEntries = _currentFilePathsList.Length;

			MatchContentParentSize();
			ResetScrollBarPosition();
		}

		/// <summary>
		/// Called by the Load Trace button click.
		/// </summary>
		public void LoadButtonAction()
		{
			// button clicked with no entry selected - ignore
			if (_currentSelectedEntry == -1)
				return;

			if (_currentSelectedEntry < 0 || _currentSelectedEntry >= _numEntries)
			{
				Debug.LogError("Invalid _currentSelectedEntry for LoadButtonAction.");
				return;
			}

			// set layout mode from toggle
			Toggle layoutModeToggle = GetLayoutModeToggle();
			GlobalSettings.LAYOUT_POLICY = (layoutModeToggle.isOn) ? NodeLayoutPolicy.Circular : NodeLayoutPolicy.LinearMixed;

			// load selected timeline
			TimelineManager timelineManager = TimelineManager.Instance;

			string fileName = _currentFilePathsList[_currentSelectedEntry];
			timelineManager.LoadTimeline(fileName);

			// switch to overview tab
			WristMenuManager.Instance.ShowOverviewTab();
		}

		/// <summary>
		/// Called when the value of the layout toggle is changed.
		/// </summary>
		public void LayoutModeToggleAction()
		{
			Toggle toggle = GetLayoutModeToggle();
			TMP_Text text = toggle.transform.GetChild(1).GetComponent<TMP_Text>();

			if (toggle.isOn)
			{
				text.text = "Layout: Circular";
			}
			else
			{
				text.text = "Layout: Linear";
			}
		}

		/// <summary>
		/// Returns an array of all trace file names in the <see cref="TraceFolderName"/> folder.
		/// </summary>
		/// <returns></returns>
		private string[] GetTraceFileNames()
		{
			return Directory.GetFiles(TraceFolderName, "*.ctrc");
		}

		/// <summary>
		/// Selects the list entry at the index, highlighting it with a color.
		/// </summary>
		private void SelectEntry(int index)
		{
			if (index < 0 || index >= _numEntries)
			{
				throw new ArgumentException("SelectEntry index out of range. (" + index + ")");
			}

			// if another is already selected - deselect first
			if (_currentSelectedEntry != -1)
			{
				DeselectEntry(_currentSelectedEntry);

				// select same entry again - deselect
				if (_currentSelectedEntry == index)
					return;
			}

			Transform contentParent = GetListContentParent();

			contentParent.GetChild(index).GetComponent<Image>().color = EntryColorHighlight;

			_currentSelectedEntry = index;
		}

		/// <summary>
		/// Deselects the entry at the index, reverting its color back to default.
		/// </summary>
		private void DeselectEntry(int index)
		{
			if (index < 0 || index >= _numEntries)
			{
				throw new ArgumentException("DeselectEntry index out of range.");
			}

			Transform contentParent = GetListContentParent();

			contentParent.GetChild(index).GetComponent<Image>().color = EntryColorBase;
		}

		/// <summary>
		/// Creates a new list entry with the given trace file name and the clickAction to be called when the entry is clicked.
		/// </summary>
		/// <param name="traceFileName">The filename of the trace.</param>
		/// <param name="clickAction">Action to be called when the entry is clicked.</param>
		private void CreateEntry(string traceFileName, Action clickAction)
		{
			GameObject newEntry = Instantiate(_listItemPrefab);

			Transform contentParent = GetListContentParent();

			newEntry.transform.SetParent(contentParent, false);
			newEntry.GetComponent<RectTransform>().anchoredPosition = new Vector2(newEntry.GetComponent<RectTransform>().anchoredPosition.x, -(ListItemHeight * _numEntries));

			newEntry.GetComponent<ListItem>().ClickAction = clickAction;
			newEntry.transform.GetChild(0).GetComponent<TMP_Text>().text = traceFileName;
			newEntry.GetComponent<Image>().color = EntryColorBase;

			_numEntries++;
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
			_currentSelectedEntry = -1;
			_currentFilePathsList = null;
		}

		private Transform GetListContentParent()
		{
			return LoadListTabParent.GetChild(1).GetChild(0).GetChild(0);
		}

		private Scrollbar GetListScrollbar()
		{
			return LoadListTabParent.GetChild(1).GetChild(1).GetComponent<Scrollbar>();
		}

		private Toggle GetLayoutModeToggle()
		{
			return LoadListTabParent.GetChild(4).GetComponent<Toggle>();
		}

	}
}