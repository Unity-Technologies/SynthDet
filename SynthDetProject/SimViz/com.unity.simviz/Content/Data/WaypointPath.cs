using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SimViz.Scenarios;

namespace UnityEngine.SimViz.Scenarios
{
    public class WaypointPath : MonoBehaviour, ISerializationCallbackReceiver
    {
        public bool raycastPosition = false;
        public List<Vector3> controlPoints = new List<Vector3>();
        [SerializeField]
        private List<SegmentWrapper> Segments = new List<SegmentWrapper>();

        public float resolution = 0.1f;
        public float length = 0;

        public bool lengthGizmoCal = true;
        public float lengthGizmo = 0;

        public ControlPoint FindPointByPosition(Vector3 position)
        {
            return Segments.Select(s => s.Where(p => p.Point == position)).SkipWhile(s => !s.Any()).First().First();
        }

        private void HandlePointDefinitionRemoval(ControlPoint point)
        {
            // If this was a point definition (not a reference) we need to make another reference into the actual definition.
            if (point.Type == ControlPointType.Point)
            {
                bool found = false;
                for (int si = 0; si < Segments.Count; si++)
                {
                    for (int pi = 0; pi < Segments[si].Count; pi++)
                    {
                        if (Segments[si][pi].Id == point.Id)
                        {
                            point.Type = ControlPointType.Point;
                            Segments[si][pi] = point;
                            found = true;
                            break;
                        }
                    }

                    if (found) break;
                }
            }
        }

        public bool RemovePoint(int segmentIndex, int itemIndex)
        {
            var found = false;

            if (segmentIndex >= 0 && segmentIndex < Segments.Count)
            {
                var segment = Segments[segmentIndex];
                if (itemIndex >= 0 && itemIndex < segment.Count)
                {
                    var point = segment[itemIndex];
                    segment.RemoveAt(itemIndex);

                    HandlePointDefinitionRemoval(point);
                    found = true;
                }
            }

            return found;
        }

        public void CopySegmentsFrom(WaypointPath path)
        {
            Segments = path.Segments.Select(s => new SegmentWrapper(s.internalSegment.ToList())).ToList();
        }

        public void Reverse()
        {
            // Reverse the points in every segment
            Segments = Segments.Select(s => new SegmentWrapper(s.internalSegment.Reverse<ControlPoint>().ToList())).ToList();
        }

        public bool RemoveSegment(int segmentIndex)
        {
            bool found = false;

            if (segmentIndex >= 0 && segmentIndex < Segments.Count)
            {
                var segment = Segments[segmentIndex].internalSegment;
                Segments.RemoveAt(segmentIndex);

                foreach (var point in segment)
                {
                    HandlePointDefinitionRemoval(point);
                }

                found = true;
            }

            return found;
        }

        public void RemoveAllSegments()
        {
            Segments.Clear();
        }

        public void AddSegment()
        {
            Segments.Add(new SegmentWrapper());
        }

        public void AddExistingPointAt(int segmentIndex, int pointIndex, int existingPointId)
        {
            var temp = Segments.Select(s => s.Where(p => p.Id == existingPointId));

            // Search across all segments for the specified point
            var existingPoint = Segments.Select(s => s.Where(p => p.Id == existingPointId)).SkipWhile(s => !s.Any()).First().First().Point;

            // Insert the point reference.
            Segments[segmentIndex].Insert(pointIndex, new ControlPoint { Id = existingPointId, Type = ControlPointType.PointReference, Point = existingPoint });
        }

        public void AddNewPointAt(int segmentIndex, int pointIndex)
        {
            // Extrapolate a position to insert when a position is not specified.
            var position = extrapolateNewPointAt(segmentIndex, pointIndex);

            AddNewPointAt(segmentIndex, pointIndex, position);
        }

        public void AddNewPointAt(int segmentIndex, int pointIndex, Vector3 position)
        {
            if (segmentIndex >= 0 && segmentIndex < Segments.Count)
            {
                // Determine next point (next index after max point index.
                var newIndex = Segments.Max(s => s.Count > 0 ? s.Max(p => p.Id) : 0) + 1;
                var controlPoint = new ControlPoint { Id = newIndex, Type = ControlPointType.Point, Point = position };

                if (pointIndex == Segments[segmentIndex].Count)
                {
                    Segments[segmentIndex].Add(controlPoint);
                }
                else
                {
                    Segments[segmentIndex].Insert(pointIndex, controlPoint);
                }
            }
        }

        private void SetPointPosition(int segmentIndex, int pointIndex, Vector3 newValue)
        {
            var controlPoint = Segments[segmentIndex][pointIndex];
            controlPoint.Point = newValue;
            Segments[segmentIndex][pointIndex] = controlPoint;
        }

        public void MovePointPosition(int segmentIndex, int pointIndex, Vector3 newValue)
        {
            // Point changes need to be applied to all instances including references to keep them in sync.
            for (int si = 0; si < Segments.Count; si++)
            {
                for (int pi = 0; pi < Segments[si].Count; pi++)
                {
                    if (Segments[segmentIndex][pointIndex].Id == Segments[si][pi].Id)
                    {
                        SetPointPosition(si, pi, newValue);
                    }
                }
            }
        }

        // TODO - One off error when the user calls this API, trying to find which loop causes the error
        public int GetSegmentCount()
        {
            return Segments.Count;
        }

        public ControlPoint GetPoint(int segmentIndex, int pointIndex)
        {
            return Segments[segmentIndex][pointIndex];
        }

        // TODO - One off error when the user calls this API, trying to find which loop causes the error 
        public int GetPointCount(int segmentIndex)
        {
            return Segments[segmentIndex].Count;
        }

        private Vector3 extrapolateNewPointAt(int segmentIndex, int pointIndex)
        {
            if (Segments[segmentIndex].Count == 0)
            {
                return gameObject.transform.root.position;
            }

            Vector3 position;
            if (Segments[segmentIndex].Count == 1)
            {
                position = Segments[segmentIndex][0].Point;
                position.x += 1;
            }
            else if (pointIndex > 0 && pointIndex < Segments[segmentIndex].Count)
            {
                // Extrapolate new point from the two points between which we are inserting
                position = Segments[segmentIndex][pointIndex].Point;
                Vector3 positionSecond = Segments[segmentIndex][pointIndex - 1].Point;
                if (Vector3.Distance((Vector3)positionSecond, (Vector3)position) > 0)
                    position = (position + positionSecond) * 0.5f;
                else
                    position.x += 1;
            }
            else // New point is at beginning or end
            {
                // Extrapolate new point by using the distance between the two closest points 
                Vector3 positionSecond;
                if (pointIndex == 0)
                {
                    position = Segments[segmentIndex][pointIndex].Point;
                    positionSecond = Segments[segmentIndex][pointIndex + 1].Point;
                }
                else
                {
                    position = Segments[segmentIndex][pointIndex - 2].Point;
                    positionSecond = Segments[segmentIndex][pointIndex - 1].Point;
                }

                if (Vector3.Distance((Vector3)positionSecond, (Vector3)position) > 0)
                    position = position + (position - positionSecond);
                else
                    position.x += 1;
            }

            return position;
        }

        public void OnBeforeSerialize()
        {
            // Do nothing for now.
        }

        public void OnAfterDeserialize()
        {
            // Converts a control points list to a segment list
            if (Segments.Count == 0 && controlPoints.Count > 0)
            {
                // For a control points list it is just a single segment that loops back on itself.
                var segment = controlPoints.Select((point, id) => new ControlPoint() { Point = point, Type = ControlPointType.Point, Id = id }).ToList();
                segment.Add(new ControlPoint() { Type = ControlPointType.PointReference, Id = 0, Point = segment[0].Point });
                Segments = new List<SegmentWrapper>() { new SegmentWrapper(segment) };
            }
        }
    }
}
