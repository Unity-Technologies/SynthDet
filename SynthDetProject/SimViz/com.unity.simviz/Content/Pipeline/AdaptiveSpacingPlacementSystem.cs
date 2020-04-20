using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [DisableAutoCreation]
    public class AdaptiveSpacingPlacementSystem : ComponentSystem, IGeneratorSystem<PlacementSystemParameters>
    {
        public PlacementSystemParameters Parameters { get; set; }
        private const float CollisionOffset = 0.01f;

        protected override void OnUpdate()
        {
            if (!ParametersValidationCheck())
                return;
            
            var parentObj = new GameObject(Parameters.category.name);
            parentObj.transform.parent = Parameters.parent;

            Entities.ForEach((ref PolygonOrientationComponent roadLane, DynamicBuffer<PointSampleGlobal> vertices) =>
            {
                var placementObject = Parameters.category.NextPlacementObject();
                var rotationFromPath = Parameters.rotationFromPath;
                if (roadLane.orientation == PolygonOrientation.Inside)
                    rotationFromPath = math.mul(quaternion.RotateY(math.PI), rotationFromPath);
                
                var offsetPolygons = PlacementUtility.OffsetPolygon(vertices, Parameters.offsetFromPath);
                foreach (var polygon in offsetPolygons)
                {
                    var interpolator = new PathInterpolator(polygon);
                    do
                    {
                        var pose = interpolator.InterpolatedVertex.pose;
                        while (PlacementUtility.CheckForCollidingObjects(
                            placementObject.BoundingBoxes,
                            pose.pos,
                            math.mul(rotationFromPath, math.mul(placementObject.Prefab.transform.rotation, pose.rot)),
                            Parameters.collisionLayerMask))
                        {
                            if (!interpolator.Navigate(CollisionOffset)) return;
                            pose = interpolator.InterpolatedVertex.pose;
                        }
                        
                        pose = interpolator.InterpolatedVertex.pose;
                        var placedObject = Object.Instantiate(placementObject.Prefab, parentObj.transform);
                        placedObject.transform.position = pose.pos;
                        placedObject.transform.rotation = math.mul(rotationFromPath,
                            math.mul(placementObject.Prefab.transform.rotation, pose.rot));
                        
                        Physics.SyncTransforms();
                        placementObject = Parameters.category.NextPlacementObject();
                    } while (interpolator.Navigate(placementObject.BoundingVolume.size.x + Parameters.spacing));            
                }
            });
        }

        private bool ParametersValidationCheck()
        {
            var errorOutput = "";
            if (Parameters.category == null)
            {
                errorOutput += "Prefab parameter is null\n";
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