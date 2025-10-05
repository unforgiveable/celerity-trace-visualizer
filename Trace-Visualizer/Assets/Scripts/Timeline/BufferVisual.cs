using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using celerity.visualizer.tracedata;

namespace celerity.visualizer.timeline
{
    public class BufferVisual : MonoBehaviour
    {
        public Buffer Buffer { get; private set; }

        public void Init(Buffer buffer)
		{
            Buffer = buffer;
		}

        public Transform GetCanvasParent()
		{
            return transform.GetChild(0);
		}

        public Transform GetFloorTransform()
		{
            return transform.GetChild(1);
		}

        public Transform GetRangeTransform()
		{
            return transform.GetChild(2);
		}
    }
}