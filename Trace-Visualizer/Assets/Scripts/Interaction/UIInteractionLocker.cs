using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace celerity.visualizer.interaction
{
	/// <summary>
	/// System for detecting if the user is actively pointing at a UI canvas.
	/// Had to be reverse-engineered from the OpenXR Interaction Toolkit library since it doesn't expose the *one* function that implements this functionality internally.
	/// </summary>
	public class UIInteractionLocker : MonoBehaviour
	{
		public static UIInteractionLocker Instance { get; private set; }

		[SerializeField] XRRayInteractor InteractorLeft;
		[SerializeField] XRRayInteractor InteractorRight;

		private bool? _isOverThisFrame;

		private void Awake()
		{
			Instance = this;
			_isOverThisFrame = null;
		}

		private void LateUpdate()
		{
			_isOverThisFrame = null;
		}

		public bool IsOverUI()
		{
			if (_isOverThisFrame == null)
			{
				_isOverThisFrame = (InteractorRight.TryGetHitInfo(out _, out _, out _, out bool isValid1) && isValid1);

				_isOverThisFrame |= (InteractorLeft.TryGetHitInfo(out _, out _, out _, out bool isValid2) && isValid2);
			}
			return _isOverThisFrame.Value;
		}

	}
}
