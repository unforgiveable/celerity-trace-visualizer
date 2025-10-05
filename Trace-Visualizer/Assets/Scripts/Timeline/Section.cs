using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace celerity.visualizer.timeline
{
    public class Section : MonoBehaviour
    {
        public ulong StartTime { get; set; }
        public ulong EndTime { get; set; }

        public ulong ComputeNodeId { get; set; }
        public SectionType Type { get; set; }
    }
}
