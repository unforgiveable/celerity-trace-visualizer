using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using celerity.visualizer.timeline;

namespace celerity.visualizer.ui
{
	public class ListItem : MonoBehaviour
	{
		public Action ClickAction;

		public void ClickEvent()
		{
			ClickAction();
		}
	}
}
