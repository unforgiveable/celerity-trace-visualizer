using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using celerity.visualizer.tracedata;
using UnityEngine.UI;
using System;

namespace celerity.visualizer.timeline
{
	public class Node : MonoBehaviour
	{
		public Command Command { get; set; }

		public Transform GetVisualsParent()
		{
			return transform.GetChild(0);
		}

		public Transform GetDetailsParent()
		{
			return transform.GetChild(1);
		}

		public override bool Equals(object obj)
		{
			return obj is Node node &&
				   base.Equals(obj) &&
				   EqualityComparer<Command>.Default.Equals(Command, node.Command);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(base.GetHashCode(), Command);
		}
	}
}
