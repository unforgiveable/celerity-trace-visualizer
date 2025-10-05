using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using celerity.visualizer.timeline;
using celerity.visualizer.tracedata;
using System;
using JetBrains.Annotations;

namespace celerity.visualizer.interaction
{
	/// <summary>
	/// Manages the Wrist Menu and delegates the functionality of the different pages to their respective managers. Singleton.
	/// Does the automatic hiding/showing of the wrist menu based on angle to the camera.
	/// Also manages the timeline slider.
	/// </summary>
	public class WristMenuManager : MonoBehaviour
	{
		public static WristMenuManager Instance { get; private set; }

		/// <summary>
		/// Allows me to disable the automatic hiding/showing of the menu by angle while in the editor.
		/// </summary>
		[SerializeField] bool doHideByAngle = true;

		private Transform _cameraTransform;
		private RectTransform _canvasTransform;

		private const float MinVisibleAngleThreshold = -(35f / 90f);

		private void Awake()
		{
			Instance = this;

			_cameraTransform = Camera.main.transform;
			_canvasTransform = transform.GetChild(0).GetComponent<RectTransform>();
		}

		private void Start()
		{
			ShowLoadListTab();
		}

		private void Update()
		{
			if (!doHideByAngle) return;

			// Hide the wrist menu if it's not facing towards the user
			Vector3 canvasNormal = -_canvasTransform.forward;
			Vector3 camToCanvasDir = _canvasTransform.position - _cameraTransform.position;
			camToCanvasDir = camToCanvasDir.normalized;

			float dotProd = Vector3.Dot(camToCanvasDir, canvasNormal);
			if (dotProd <= MinVisibleAngleThreshold)
			{
				_canvasTransform.gameObject.SetActive(true);
			}
			else
			{
				_canvasTransform.gameObject.SetActive(false);
			}

		}

		public void Init()
		{
			UpdateTimelineSlider(true);
		}

		/// <summary>
		/// Shows the overview tab on the wrist menu.
		/// Gets called by the Overview tab button.
		/// </summary>
		public void ShowOverviewTab()
		{
			HideAllTabs();
			Transform overviewParent = GetOverviewParent();
			Trace _trace = TimelineManager.Instance.CurrentTrace;

			overviewParent.gameObject.SetActive(true);

			if (_trace == null)
			{
				overviewParent.GetChild(0).GetComponent<TMPro.TMP_Text>().text = "No Trace Loaded.";
				overviewParent.GetChild(1).GetComponent<TMPro.TMP_Text>().text = "";
				overviewParent.GetChild(2).GetComponent<TMPro.TMP_Text>().text = "";
			}
			else
			{
				overviewParent.GetChild(0).GetComponent<TMPro.TMP_Text>().text = "ID: " + _trace.RunId;
				overviewParent.GetChild(1).GetComponent<TMPro.TMP_Text>().text = _trace.ComputeNodes.Count + " Nodes, " + _trace.Tasks.Count + " Tasks, " + _trace.Commands.Count + " Commands, " + _trace.Buffers.Count + " Buffers";
				overviewParent.GetChild(2).GetComponent<TMPro.TMP_Text>().text = _trace.Duration / GlobalSettings.TimestampToSecondsConversionFactor + " seconds total";
			}
		}

		/// <summary>
		/// Shows the modes tab on the wrist menu.
		/// Gets called by the Modes tab button.
		/// </summary>
		public void ShowModesTab()
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;

			// disallow switching if no trace loaded.
			if (_trace == null)
				return;

			HideAllTabs();
			Transform modesParent = GetModesParent();

			modesParent.gameObject.SetActive(true);

			WristMenuModes.Instance.ShowModesMenu();
		}

		/// <summary>
		/// Shows the list tab on the wrist menu.
		/// Gets called by the List tab button.
		/// </summary>
		public void ShowListTab()
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;

			// disallow switching if no trace loaded.
			if (_trace == null)
				return;

			HideAllTabs();
			Transform listParent = GetListParent();

			listParent.gameObject.SetActive(true);
		}

		/// <summary>
		/// Shows the load list tab on the wrist menu.
		/// Gets called by the Load List tab button.
		/// </summary>
		public void ShowLoadListTab()
		{
			HideAllTabs();
			Transform loadListParent = GetLoadListParent();

			loadListParent.gameObject.SetActive(true);

			WristMenuLoadList.Instance.ShowTracesList();
		}

		/// <summary>
		/// Called by the timeline slider when its value has changed.
		/// Will also get invoked when its changed by the <see cref="UpdateTimelineSlider(bool)"/> function, but these calls get filtered out.
		/// </summary>
		public void OnSliderInput()
		{
			// Ignore event if not currently over UI - can't be from player input!
			if (!UIInteractionLocker.Instance.IsOverUI())
				return;

			Transform sliderParent = GetSliderParent();
			Slider slider = sliderParent.GetChild(0).GetComponent<Slider>();

			TraceMover.Instance.SetTimelinePositionPercentage(slider.value);

			UpdateTimelineSlider(false);
			WristMenuModes.Instance.UpdateRangeFilterModeAfterMove();
		}

		/// <summary>
		/// Updates the timeline slider value text.
		/// If <paramref name="setSliderValue"/> is <see langword="true"/> sets the value of the timeline slider to match the current timeline position.
		/// Gets called by <see cref="TraceMover.UpdateTransformPositions(Vector3)"/> with <paramref name="setSliderValue"/> as <see langword="true"/> when the timeline is moved by the player.
		/// </summary>
		/// <param name="setSliderValue">If the timeline slider's value should be set to match the current timeline position.</param>
		public void UpdateTimelineSlider(bool setSliderValue)
		{
			Transform sliderParent = GetSliderParent();
			Trace _trace = TimelineManager.Instance.CurrentTrace;

			// if no trace is loaded keep the slider clamped to 0
			if (_trace == null)
			{
				sliderParent.GetChild(1).GetComponent<TMPro.TMP_Text>().text = "-";

				if (setSliderValue)
				{
					Slider slider = sliderParent.GetChild(0).GetComponent<Slider>();
					slider.value = 0;
				}

				return;
			}

			float currentLocationPercentage = TraceMover.Instance.GetTimelinePositionPercentage();

			if (setSliderValue)
			{
				Slider slider = sliderParent.GetChild(0).GetComponent<Slider>();
				slider.value = currentLocationPercentage;
			}

			sliderParent.GetChild(1).GetComponent<TMPro.TMP_Text>().text = string.Format("{0:0.00} / {1:0.00}", (_trace.Duration * currentLocationPercentage) / GlobalSettings.TimestampToSecondsConversionFactor, _trace.Duration / GlobalSettings.TimestampToSecondsConversionFactor);
		}

		/// <summary>
		/// Hides all wrist tabs.
		/// </summary>
		private void HideAllTabs()
		{
			int num = _canvasTransform.transform.GetChild(0).childCount;
			for (int i = 0; i < num; i++)
			{
				_canvasTransform.transform.GetChild(0).GetChild(i).gameObject.SetActive(false);
			}
		}

		// -----
		// Extracted methods for referencing different transforms within the wrist menu.
		// Allows for a one-place change if the structure of the wrist menu is modified.

		private Transform GetOverviewParent()
		{
			return _canvasTransform.transform.GetChild(0).GetChild(0);
		}

		private Transform GetModesParent()
		{
			return _canvasTransform.transform.GetChild(0).GetChild(1);
		}

		private Transform GetListParent()
		{
			return _canvasTransform.transform.GetChild(0).GetChild(2);
		}

		private Transform GetLoadListParent()
		{
			return _canvasTransform.transform.GetChild(0).GetChild(3);
		}

		private Transform GetSliderParent()
		{
			return _canvasTransform.transform.GetChild(1);
		}
	}
}
