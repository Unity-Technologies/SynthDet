using System;
using System.Collections.Generic;
using ClipperLib;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [DisableAutoCreation]
    public class PoissonDiscSamplingPlacementSystem : ComponentSystem, IGeneratorSystem<PlacementSystemParameters>
    {
        public PlacementSystemParameters Parameters { get; set; }
        private const float MinimumSpacing = 1f;

        protected override void OnUpdate()
        {
            if (!ParametersValidationCheck())
                return;
            
            var parentObj = new GameObject(Parameters.category.name);
            parentObj.transform.parent = Parameters.parent;
            
            var spacing = Parameters.spacing;
            if (Parameters.spacing < MinimumSpacing)
            {
                Debug.LogWarning(
                    $"Spacing parameter is less than the minimum of {MinimumSpacing}. Proceeding with minimum spacing.");
                spacing = MinimumSpacing;
            }

            var outsidePolygons = new List<List<IntPoint>>();
            
            Entities.ForEach((ref PolygonOrientationComponent roadLane, DynamicBuffer<PointSampleGlobal> vertices) =>
            {
                if (roadLane.orientation == PolygonOrientation.Outside)
                {
                    outsidePolygons.Add(PlacementUtility.FromSamplesToClipper(vertices));
                    return;
                }

                var points = PlacementUtility.PoissonPointsOnPolygon(vertices, spacing);
                PlaceObjectsAtPoissonPoints(points, parentObj.transform);
            });

            var outsidePoints = PlacementUtility.PoissonPointsOutsidePolygon(outsidePolygons, spacing, Parameters.offsetFromPath);
            PlaceObjectsAtPoissonPoints(outsidePoints, parentObj.transform);
        }

        private void PlaceObjectsAtPoissonPoints(IEnumerable<float3> points, Transform parent)
        {
            foreach (var point in points)
            {
                var placementObject = Parameters.category.NextPlacementObject();
                var rotation = quaternion.RotateY(Random.Range(0, math.PI * 2));
                if (PlacementUtility.CheckForCollidingObjects(
                    placementObject.BoundingBoxes,
                    point,
                    rotation,
                    Parameters.collisionLayerMask))
                    continue;

                var placedObject = Object.Instantiate(placementObject.Prefab, parent.transform);
                placedObject.transform.position = point;
                placedObject.transform.rotation = rotation;
                Physics.SyncTransforms();
            }
        }
        
        private bool ParametersValidationCheck()
        {
            var errorOutput = "";
            if (Parameters.category == null)
            {
                errorOutput += "Category parameter is null\n";
            }

            if (Parameters.parent == null)
            {
                errorOutput += "Parent transform parameter is null\n";
            }

            if (string.IsNullOrEmpty(errorOutput)) return true;
            Debug.LogError(errorOutput);
            return false;
        }
    }
}