using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [DisableAutoCreation]
    public class UniformPlacementSystem : ComponentSystem, IGeneratorSystem<PlacementSystemParameters>
    {
        public PlacementSystemParameters Parameters { get; set; }
        private const float MinimumSpacing = 0.01f;

        protected override void OnUpdate()
        {
            if (!ParametersValidationCheck())
                return;
            
            var parentObj = new GameObject(Parameters.category.name);
            parentObj.transform.parent = Parameters.parent;
            
            var spacing = Parameters.spacing;
            if (Parameters.spacing < MinimumSpacing)
            {
                Debug.LogWarning($"Spacing parameter is less than the minimum of {MinimumSpacing}");
                spacing = MinimumSpacing;
            }
            
            Entities.ForEach((ref PolygonOrientationComponent roadLane, DynamicBuffer<PointSampleGlobal> vertices) =>
            {
                var placementObject = Parameters.category.NextPlacementObject();
                var rotationFromRoad = Parameters.rotationFromPath;
                if (roadLane.orientation == PolygonOrientation.Inside)
                    rotationFromRoad = math.mul(quaternion.RotateY(math.PI), rotationFromRoad);
                
                var offsetPolygons = PlacementUtility.OffsetPolygon(vertices, Parameters.offsetFromPath);
                foreach (var polygon in offsetPolygons)
                {
                    var interpolator = new PathInterpolator(polygon);
                    do
                    {
                        var pose = interpolator.InterpolatedVertex.pose;
                        var rotation = math.mul(rotationFromRoad,
                            math.mul(placementObject.Prefab.transform.rotation, pose.rot));
                        
                        if (PlacementUtility.CheckForCollidingObjects(
                            placementObject.BoundingBoxes,
                            pose.pos,
                            rotationFromRoad,
                            Parameters.collisionLayerMask))
                        {
                            continue;
                        }

                        var placedObject = Object.Instantiate(placementObject.Prefab, parentObj.transform);
                        placedObject.transform.position = pose.pos;
                        placedObject.transform.rotation = rotation;
                        Physics.SyncTransforms();
                        placementObject = Parameters.category.NextPlacementObject();
                    } while (interpolator.Navigate(spacing));
                }
            });
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