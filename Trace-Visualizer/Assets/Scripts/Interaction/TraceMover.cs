using celerity.visualizer.timeline;
using celerity.visualizer.tracedata;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

namespace celerity.visualizer.interaction
{
	/// <summary>
	/// System for handling the dragging and scaling of the timeline by the user.
	/// Keeps track of the current interaction type (moving = dragging, scaling = 2-controller-dragging) using various states.
	/// Also handles the translation of the timeline from other sources (timeline slider, jump-to-node, etc.).
	/// Implements the methods to interface with the UnityEngine.InputSystem events for the controllers.
	/// </summary>
	public class TraceMover : MonoBehaviour
	{
		public static TraceMover Instance { get; private set; }

		[SerializeField] InputAction ActionLeftController;
		[SerializeField] InputAction ActionRightController;
		[SerializeField] Transform LeftControllerTransform;
		[SerializeField] Transform RightControllerTransform;

		public bool IsMovementEnabled { get; private set; }

		private Vector3? _leftControllerStartPoint;
		private Vector3? _rightControllerStartPoint;
		private Vector3 _leftControllerCurrentPoint;
		private Vector3 _rightControllerCurrentPoint;
		private bool _isLeftHeld;
		private bool _isRightHeld;
		private Vector3 _startingPosition;
		private Vector3 _startingScale;
		private bool _didScaling;
		private bool _wasOverUI;
		private bool _clearedDeadzone;

		private const float MoveMultiplier = 2f;
		private const float DragDeadzone = 0.01f; // 1cm
		private const float TaskParentVertPos = 0.05f;
		private const float CircularLayoutOriginYOffset = 1f;

		private const bool DEBUG_EVENTS = false;
		private const bool DEBUG_MOVING = false;
		private const bool DEBUG_SCALING = false;

#pragma warning disable CS0162 // Unreachable code detected

		private void Awake()
		{
			Instance = this;
		}

		private void Start()
		{
			ActionLeftController.performed += LeftControllerActionPerformed;
			ActionLeftController.canceled += LeftControllerActionCanceled;

			ActionRightController.performed += RightControllerActionPerformed;
			ActionRightController.canceled += RightControllerActionCanceled;

			SetMovement(true);
		}

		public void Init()
		{
			transform.position = Vector3.zero;
			transform.localScale = Vector3.one;

			GetTaskParent().transform.localPosition = new(0, TaskParentVertPos, 0);
			GetNodeIdLabelParent().transform.localPosition = Vector3.zero;
		}


		private void Update()
		{
			if (!IsMovementEnabled)
				return;

			if (_wasOverUI)
				return;

			// newly over UI
			if (UIInteractionLocker.Instance.IsOverUI())
			{
				_wasOverUI = true;
				return;
			}

			// update controller positions
			if (_isLeftHeld)
			{
				_leftControllerCurrentPoint = LeftControllerTransform.position;
				if (_leftControllerStartPoint == null)
					_leftControllerStartPoint = LeftControllerTransform.position;
			}
			if (_isRightHeld)
			{
				_rightControllerCurrentPoint = RightControllerTransform.position;
				if (_rightControllerStartPoint == null)
					_rightControllerStartPoint = RightControllerTransform.position;
			}

			// update movement depending on type
			if (_isLeftHeld && _isRightHeld)
			{
				if (!_didScaling)
				{
					// first frame of scaling - reset any "accidental" moving
					UpdateTransformPositions(_startingPosition);
					_didScaling = true;

					if (DEBUG_SCALING)
						Debug.Log("Start Scaling");
				}

				UpdateDoubleControllerDrag();
			}
			else if (!_didScaling && _isLeftHeld)
			{
				UpdateSingleControllerDrag(true);
			}
			else if (!_didScaling && _isRightHeld)
			{
				UpdateSingleControllerDrag(false);
			}
			else if (!_isLeftHeld && !_isRightHeld)
			{
				if (_didScaling)
				{
					if (DEBUG_SCALING)
						Debug.Log("Scaling released.");

					// everything released - reset scaling toggle
					_didScaling = false;
				}
			}

		}


		/// <summary>
		/// Gets called on timeline slider input.
		/// Moves the trace parent such that the part of the trace at x=0 (world coordinates) is the specified percentage of the trace.
		/// E.g. a percentage of 0.25 would result in 25% of the trace being in the -x direction and 75% in the +x direction.
		/// Percentage values are limited to range [0;1].
		/// </summary>
		/// <param name="positionPercentage"></param>
		public void SetTimelinePositionPercentage(float positionPercentage)
		{
			if (positionPercentage < 0 || positionPercentage > 1)
			{
				Debug.LogWarning("MoveToTimelinePosition called with percentage out of range (" + positionPercentage + ").");
				return;
			}

			Trace _trace = TimelineManager.Instance.CurrentTrace;

			if (_trace == null)
			{
				transform.position = Vector3.zero;
				return;
			}

			float totalTraceLength = _trace.Duration * NodePositionManager.TimeToDistanceScaleFactor;
			float scaledTraceLength, targetLocation;
			Vector3 newLocation;

			// depending on current layout policy compute the location along different axis
			switch (GlobalSettings.LAYOUT_POLICY)
			{
				case NodeLayoutPolicy.LinearMixed:
					scaledTraceLength = totalTraceLength * transform.localScale.x;
					targetLocation = positionPercentage * scaledTraceLength;
					newLocation = new(-targetLocation, transform.position.y, transform.position.z);
					break;

				case NodeLayoutPolicy.Circular:
					scaledTraceLength = totalTraceLength * transform.localScale.y;
					targetLocation = positionPercentage * scaledTraceLength - CircularLayoutOriginYOffset;
					newLocation = new(transform.position.x, -targetLocation, transform.position.z);
					break;

				default:
					Debug.LogError("Node layout policy not yet implemented.");
					return;
			}

			UpdateTransformPositions(newLocation);
		}

		/// <summary>
		/// Returns the position of the trace currently at the x=0 location as a percentage of the entire trace.
		/// E.g. a return value of 0.25 means 25% of the trace is in the -x direction and 75% in the +x direction, with the point at 25% being right at x=0.
		/// Return value is clamped in range [0;1] even if actual position extends past the ends.
		/// </summary>
		public float GetTimelinePositionPercentage()
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;

			if (_trace == null)
				return 0;

			float totalTraceLength = _trace.Duration * NodePositionManager.TimeToDistanceScaleFactor;

			float scaledTraceLength, currentTraceLocationPercent;

			switch (GlobalSettings.LAYOUT_POLICY)
			{
				case NodeLayoutPolicy.LinearMixed:
					scaledTraceLength = totalTraceLength * transform.localScale.x;
					currentTraceLocationPercent = Mathf.Clamp((-transform.position.x) / scaledTraceLength, 0, 1);
					break;

				case NodeLayoutPolicy.Circular:
					scaledTraceLength = totalTraceLength * transform.localScale.y;
					currentTraceLocationPercent = Mathf.Clamp((-transform.position.y + CircularLayoutOriginYOffset) / scaledTraceLength, 0, 1);
					break;

				default:
					throw new NotImplementedException("Node layout policy not yet implemented.");
			}

			return currentTraceLocationPercent;
		}

		/// <summary>
		/// Computes the timestamp at the scene origin.
		/// </summary>
		public ulong GetTimelineTimeAtOrigin()
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;

			return _trace.MinStartTime + (ulong)((double)GetTimelinePositionPercentage() * _trace.Duration);
		}

		/// <summary>
		/// Sets the trace position such that the origin is under the center of the given node.
		/// </summary>
		/// <param name="node">The node to center on.</param>
		public void CenterTraceOnNode(Node node)
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;

			ulong commandDuration = node.Command.EndTime - node.Command.StartTime;
			ulong timeAtCenter = node.Command.StartTime + (commandDuration / 2) - _trace.MinStartTime;

			CenterTraceOnTime(timeAtCenter);
		}

		/// <summary>
		/// Sets the trace position such that the origin is at the given timeline time.
		/// </summary>
		/// <param name="normalizedTime">The time to center on normalized to the minimum start time of the trace.</param>
		public void CenterTraceOnTime(ulong normalizedTime)
		{
			Trace _trace = TimelineManager.Instance.CurrentTrace;
			float positionPercentage = (float)normalizedTime / _trace.Duration;

			SetTimelinePositionPercentage(positionPercentage);

			// notify update wrist menu about new position value
			WristMenuManager.Instance.UpdateTimelineSlider(true);

			// notify mode menu about new position value
			WristMenuModes.Instance.UpdateRangeFilterModeAfterMove();
		}

		public void ToggleMovement()
		{
			SetMovement(!IsMovementEnabled);
		}

		public void SetMovement(bool state)
		{
			if (state)
			{
				ActionLeftController.Enable();
				ActionRightController.Enable();
			}
			else
			{
				ActionLeftController.Disable();
				ActionRightController.Disable();
			}

			IsMovementEnabled = state;
		}

		private void LeftControllerActionPerformed(InputAction.CallbackContext context)
		{
			if (context.interaction is PressInteraction)
			{
				// pressed
				_isLeftHeld = true;
				_startingPosition = transform.position;
				_startingScale = transform.localScale;
				_wasOverUI = false; // reset UI lockout
				if (DEBUG_EVENTS)
					Debug.Log("Left controller press");
			}
		}

		private void LeftControllerActionCanceled(InputAction.CallbackContext context)
		{
			if (context.interaction is PressInteraction)
			{
				// released
				_isLeftHeld = false;
				_leftControllerStartPoint = null;
				_clearedDeadzone = false;
				if (DEBUG_EVENTS)
					Debug.Log("Left controller release");
			}
		}

		private void RightControllerActionPerformed(InputAction.CallbackContext context)
		{
			if (context.interaction is PressInteraction)
			{
				// pressed
				_isRightHeld = true;
				_startingPosition = transform.position;
				_startingScale = transform.localScale;
				_wasOverUI = false; // reset UI lockout
				if (DEBUG_EVENTS)
					Debug.Log("Right controller press");
			}
		}

		private void RightControllerActionCanceled(InputAction.CallbackContext context)
		{
			if (context.interaction is PressInteraction)
			{
				// released
				_isRightHeld = false;
				_rightControllerStartPoint = null;
				_clearedDeadzone = false;
				if (DEBUG_EVENTS)
					Debug.Log("Right controller release");
			}
		}

		private void UpdateSingleControllerDrag(bool isLeftController)
		{
			// only one controller's trigger held, so position dragging
			Vector3 difference;

			if (isLeftController)
			{
				difference = _leftControllerCurrentPoint - _leftControllerStartPoint.Value;
			}
			else
			{
				difference = _rightControllerCurrentPoint - _rightControllerStartPoint.Value;
			}

			// check if we already have cleared the deadzone
			if (!_clearedDeadzone)
			{
				// is difference greater than deadzone now?
				if (difference.magnitude > DragDeadzone)
				{
					_clearedDeadzone = true;
				}
				else
				{
					return;
				}
			}

			UpdateTransformPositions(_startingPosition + (difference * MoveMultiplier));

			// notify update wrist menu about new position value
			WristMenuManager.Instance.UpdateTimelineSlider(true);

			// notify mode menu about new position value
			// TODO might have to limit to only end-of-drag for performance
			WristMenuModes.Instance.UpdateRangeFilterModeAfterMove();

			if (DEBUG_MOVING)
				Debug.Log("moved trace by total of " + difference * MoveMultiplier + ", _rightControllerStartPoint: " + _rightControllerStartPoint + ", _rightControllerCurrentPoint: " + _rightControllerCurrentPoint);
		}

		/// <summary>
		/// Moves the timeline to the new position.
		/// </summary>
		/// <param name="newPosition"></param>
		private void UpdateTransformPositions(Vector3 newPosition)
		{
			transform.position = newPosition;

			// offset task parent to be at TaskParentVertPos
			Transform taskParent = GetTaskParent();
			float diff = TaskParentVertPos - newPosition.y;
			diff *= 1f / transform.localScale.y; //invert scaling of parent
			taskParent.localPosition = new(0, diff, 0);

			// offset node id label parent to be at 0 vertical position
			Transform nodeIdLabelParent = GetNodeIdLabelParent();
			diff = -newPosition.y;
			diff *= 1f / transform.localScale.y; //invert scaling of parent
			nodeIdLabelParent.localPosition = new(0, diff, 0);
		}

		private void UpdateScale(Vector3 newScale)
		{
			transform.localScale = newScale;
			NodeDependenciesManager.Instance.SetDependencyLineWidth(newScale.x);
		}

		private void UpdateDoubleControllerDrag()
		{
			// scale amount is ratio between current controller distance and starting controller distance
			Vector3 startPointConnection = (_rightControllerStartPoint - _leftControllerStartPoint).Value;
			float startDistance = startPointConnection.magnitude;
			float currentDistance = (_rightControllerCurrentPoint - _leftControllerCurrentPoint).magnitude;
			float scaleAmount = currentDistance / startDistance;

			// pivot is mid-point between starting positions
			Vector3 scalePivot = _leftControllerStartPoint.Value + (startPointConnection / 2f);
			Vector3 origin = _startingPosition;

			Vector3 diff = origin - scalePivot;
			Vector3 newScale = _startingScale * scaleAmount;
			float relativeScale = newScale.x / _startingScale.x;

			// scale position based on scale pivot difference
			Vector3 postScalePosition = scalePivot + diff * relativeScale;

			UpdateScale(newScale);
			UpdateTransformPositions(postScalePosition);

			// notify update wrist menu about new position value
			WristMenuManager.Instance.UpdateTimelineSlider(true);

			// notify mode menu about new position value
			// TODO might have to limit to only end-of-drag for performance
			WristMenuModes.Instance.UpdateRangeFilterModeAfterMove();

			if (DEBUG_SCALING)
				Debug.Log("newScale: " + newScale + ", _startingScale: " + _startingScale + ", scaleAmount: " + scaleAmount + ", scalePivot: " + scalePivot + ", _startingPosition: " + _startingPosition + ", diff: " + diff + ", postScalePosition: " + postScalePosition);
		}
#pragma warning restore CS0162 // Unreachable code detected

		private Transform GetTaskParent()
		{
			return transform.GetChild(1);
		}
		private Transform GetNodeIdLabelParent()
		{
			return transform.GetChild(3);
		}

	}
}
