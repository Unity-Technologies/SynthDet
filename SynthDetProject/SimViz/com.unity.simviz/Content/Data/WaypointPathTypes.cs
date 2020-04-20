using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.SimViz.Scenarios
{
    public enum ControlPointType
    {
        Point,
        PointReference,
    }

    [System.Serializable]
    public struct ControlPoint
    {
        public ControlPointType Type;
        public int Id;
        public Vector3 Point;
    }

    public enum MovementDirection
    {
        Forward,
        Backward,
    }

    [System.Serializable]
    public class SegmentWrapper
    {
        public List<ControlPoint> internalSegment;

        public SegmentWrapper(List<ControlPoint> source)
        {
            internalSegment = source;
        }

        public SegmentWrapper()
        {
            internalSegment = new List<ControlPoint>();
        }

        public ControlPoint this[int key]
        {
            get { return internalSegment[key]; }
            set { internalSegment[key] = value; }
        }

        public int Count
        {
            get { return internalSegment.Count; }
        }

        public void Clear()
        {
            internalSegment.Clear();
        }

        public void RemoveAt(int index)
        {
            internalSegment.RemoveAt(index);
        }

        public void Insert(int index, ControlPoint point)
        {
            internalSegment.Insert(index, point);
        }

        public void Add(ControlPoint point)
        {
            internalSegment.Add(point);
        }

        public IEnumerable<ControlPoint> Where(System.Func<ControlPoint, bool> predicate)
        {
            return internalSegment.Where(predicate);
        }

        public int Max(System.Func<ControlPoint, int> selector)
        {
            return internalSegment.Max(selector);
        }
    }
}
