﻿using System.Collections.Generic;
using UnityEngine;

namespace PathCreation.Utility {
    public static class MathUtility {

        static PosRotScale LockTransformToSpace (Transform t, PathSpace space) {
            var original = new PosRotScale (t);
            if (space == PathSpace.xy) {
                t.eulerAngles = new Vector3 (0, 0, t.eulerAngles.z);
                t.position = new Vector3 (t.position.x, t.position.y, 0);
            } else if (space == PathSpace.xz) {
                t.eulerAngles = new Vector3 (0, t.eulerAngles.y, 0);
                t.position = new Vector3 (t.position.x, 0, t.position.z);
            }

            //float maxScale = Mathf.Max (t.localScale.x * t.parent.localScale.x, t.localScale.y * t.parent.localScale.y, t.localScale.z * t.parent.localScale.z);
            float maxScale = Mathf.Max (t.lossyScale.x, t.lossyScale.y, t.lossyScale.z);

            t.localScale = Vector3.one * maxScale;

            return original;
        }

        public static Vector3 TransformPoint (Vector3 p, Transform t, PathSpace space) {
            var original = LockTransformToSpace (t, space);
            Vector3 transformedPoint = t.TransformPoint (p);
            original.SetTransform (t);
            return transformedPoint;
        }

        public static Vector3 InverseTransformPoint (Vector3 p, Transform t, PathSpace space) {
            var original = LockTransformToSpace (t, space);
            Vector3 transformedPoint = t.InverseTransformPoint (p);
            original.SetTransform (t);
            return transformedPoint;
        }

        public static Vector3 TransformVector (Vector3 p, Transform t, PathSpace space) {
            var original = LockTransformToSpace (t, space);
            Vector3 transformedPoint = t.TransformVector (p);
            original.SetTransform (t);
            return transformedPoint;
        }

        public static Vector3 InverseTransformVector (Vector3 p, Transform t, PathSpace space) {
            var original = LockTransformToSpace (t, space);
            Vector3 transformedPoint = t.InverseTransformVector (p);
            original.SetTransform (t);
            return transformedPoint;
        }

        public static Vector3 TransformDirection (Vector3 p, Transform t, PathSpace space) {
            var original = LockTransformToSpace (t, space);
            Vector3 transformedPoint = t.TransformDirection (p);
            original.SetTransform (t);
            return transformedPoint;
        }

        public static Vector3 InverseTransformDirection (Vector3 p, Transform t, PathSpace space) {
            var original = LockTransformToSpace (t, space);
            Vector3 transformedPoint = t.InverseTransformDirection (p);
            original.SetTransform (t);
            return transformedPoint;
        }

        public static bool LineSegmentsIntersect (Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2) {
            float d = (b2.x - b1.x) * (a1.y - a2.y) - (a1.x - a2.x) * (b2.y - b1.y);
            if (d == 0)
                return false;
            float t = ((b1.y - b2.y) * (a1.x - b1.x) + (b2.x - b1.x) * (a1.y - b1.y)) / d;
            float u = ((a1.y - a2.y) * (a1.x - b1.x) + (a2.x - a1.x) * (a1.y - b1.y)) / d;

            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }

        public static bool LinesIntersect (Vector2 a1, Vector2 a2, Vector2 a3, Vector2 a4) {
            return (a1.x - a2.x) * (a3.y - a4.y) - (a1.y - a2.y) * (a3.x - a4.x) != 0;
        }

        public static Vector2 PointOfLineLineIntersection (Vector2 a1, Vector2 a2, Vector2 a3, Vector2 a4) {
            float d = (a1.x - a2.x) * (a3.y - a4.y) - (a1.y - a2.y) * (a3.x - a4.x);
            if (d == 0) {
                Debug.LogError ("Lines are parallel, please check that this is not the case before calling line intersection method");
                return Vector2.zero;
            } else {
                float n = (a1.x - a3.x) * (a3.y - a4.y) - (a1.y - a3.y) * (a3.x - a4.x);
                float t = n / d;
                return a1 + (a2 - a1) * t;
            }
        }

        public static Vector2 ClosestPointOnLineSegment (Vector2 p, Vector2 a, Vector2 b) {
            Vector2 aB = b - a;
            Vector2 aP = p - a;
            float sqrLenAB = aB.sqrMagnitude;

            if (sqrLenAB == 0)
                return a;

            float t = Mathf.Clamp01 (Vector2.Dot (aP, aB) / sqrLenAB);
            return a + aB * t;
        }

        public static Vector3 ClosestPointOnLineSegment (Vector3 p, Vector3 a, Vector3 b) {
            Vector3 aB = b - a;
            Vector3 aP = p - a;
            float sqrLenAB = aB.sqrMagnitude;

            if (sqrLenAB == 0)
                return a;

            float t = Mathf.Clamp01 (Vector3.Dot (aP, aB) / sqrLenAB);
            return a + aB * t;
        }

        public static int SideOfLine (Vector2 a, Vector2 b, Vector2 c) {
            return (int) Mathf.Sign ((c.x - a.x) * (-b.y + a.y) + (c.y - a.y) * (b.x - a.x));
        }

        /// returns the smallest angle between ABC. Never greater than 180
        public static float MinAngle (Vector3 a, Vector3 b, Vector3 c) {
            return Vector3.Angle ((a - b), (c - b));
        }

        public static bool PointInTriangle (Vector2 a, Vector2 b, Vector2 c, Vector2 p) {
            float area = 0.5f * (-b.y * c.x + a.y * (-b.x + c.x) + a.x * (b.y - c.y) + b.x * c.y);
            float s = 1 / (2 * area) * (a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y);
            float t = 1 / (2 * area) * (a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y);
            return s >= 0 && t >= 0 && (s + t) <= 1;
        }

        public static bool PointsAreClockwise (Vector2[] points) {
            float signedArea = 0;
            for (int i = 0; i < points.Length; i++) {
                int nextIndex = (i + 1) % points.Length;
                signedArea += (points[nextIndex].x - points[i].x) * (points[nextIndex].y + points[i].y);
            }

            return signedArea >= 0;
        }

        class PosRotScale {
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public readonly Vector3 scale;

            public PosRotScale (Transform t) {
                this.position = t.position;
                this.rotation = t.rotation;
                this.scale = t.localScale;
            }

            public void SetTransform (Transform t) {
                t.position = position;
                t.rotation = rotation;
                t.localScale = scale;

            }
        }
    }
}