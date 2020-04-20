using Unity.Mathematics;
using UnityEngine.SimViz.Content.MapElements;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [CreateAssetMenu(fileName = "PolygonSoupPlacementPipeline", menuName = "SimViz/Content/PolygonSoupPlacementPipeline", order = 2)]
    public class PolygonSoupPlacementPipeline : ScriptableObject
    {
        public RoadNetworkDescription roadNetworkDescription;
        public bool useEcsRoadNetwork;
        public ProcessingScheme processingScheme;

        public bool drawPolygons = true;

        public Material roadMaterial;
        public float uvMultiplier = 1;

        public bool placeBuildings = true;
        public PlacementCategory buildingCategory;
        public float buildingSpacing;
        public float buildingOffset;

        public bool placeTrees = true;
        public PlacementCategory treeCategory;
        public float treeSpacing;
        public float treeOffset;

        public bool placeStreetLights;
        public PlacementCategory streetLightCategory;
        public float streetLightSpacing;
        public float streetLightOffset;

        public bool placeRoadSigns;
        public PlacementCategory roadSignCategory;
        public float roadSignSpacing;
        public float roadSignOffset;

        public bool placePoissonTrees;
        public float poissonTreeSpacing;
        public float poissonOuterPolygonOffset;

        public GameObject RunPipeline()
        {
            if (roadNetworkDescription == null)
                return null;

            using (var pipeline = new ContentPipeline())
            {
                var terrainLayerMask = ~(1 << LayerMask.NameToLayer("Terrain"));
                var sideWalkLayerMask = terrainLayerMask & ~(1 << LayerMask.NameToLayer("Sidewalk"));

                if (useEcsRoadNetwork)
                {

                    pipeline.RunGenerator<RoadNetworkDescriptionToEcsSystem, RoadNetworkDescriptionToEcsSystemParameters>(new RoadNetworkDescriptionToEcsSystemParameters()
                    {
                        roadNetworkDescription = roadNetworkDescription
                    });
                    pipeline.RunGenerator<GeneratePolygonsFromEcsSystem, PolygonSystemFromEcsParameters>(new PolygonSystemFromEcsParameters()
                    {
                        minimumPolygonArea = 800.0f,
                        extensionDistance = 0.1f,
                        processingScheme = processingScheme
                    });
                }
                else
                {
                    pipeline.RunGenerator<GeneratePolygonsSystem, PolygonSystemParameters>(new PolygonSystemParameters()
                    {
                        roadNetworkDescription = roadNetworkDescription,
                        minimumPolygonArea = 800.0f,
                        extensionDistance = 0.1f
                    });
                }

                var parentObject = new GameObject("Procedural Placement");

                pipeline.RunGenerator<MeshFromPolygons, MeshFromPolygonsParameters>(new MeshFromPolygonsParameters()
                {
                    Material = roadMaterial,
                    Parent = parentObject.transform,
                    UVMultiplier = uvMultiplier
                });

                if (drawPolygons)
                {
                    pipeline.RunGenerator<PolygonDrawingSystem, PolygonDrawingParameters>(new PolygonDrawingParameters
                    {
                        parent = parentObject.transform
                    });
                }

                if (placeBuildings)
                {
                    pipeline.RunGenerator<AdaptiveSpacingPlacementSystem, PlacementSystemParameters>(new PlacementSystemParameters
                    {
                        spacing = buildingSpacing,
                        category = buildingCategory,
                        collisionLayerMask = terrainLayerMask,
                        parent = parentObject.transform,
                        offsetFromPath = buildingOffset,
                        rotationFromPath = quaternion.RotateY(math.PI / 2)
                    });
                }

                if (placeTrees)
                {
                    pipeline.RunGenerator<UniformPlacementSystem, PlacementSystemParameters>(new PlacementSystemParameters
                    {
                        spacing = treeSpacing,
                        category = treeCategory,
                        collisionLayerMask = terrainLayerMask,
                        parent = parentObject.transform,
                        offsetFromPath = treeOffset,
                        rotationFromPath = quaternion.RotateY(math.PI / 2)
                    });
                }

                if (placeStreetLights)
                {
                    pipeline.RunGenerator<UniformPlacementSystem, PlacementSystemParameters>(new PlacementSystemParameters
                    {
                        spacing = streetLightSpacing,
                        category = streetLightCategory,
                        collisionLayerMask = sideWalkLayerMask,
                        parent = parentObject.transform,
                        offsetFromPath = streetLightOffset,
                        rotationFromPath = quaternion.RotateY(math.PI / 2)
                    });
                }

                if (placeRoadSigns)
                {
                    pipeline.RunGenerator<UniformPlacementSystem, PlacementSystemParameters>(new PlacementSystemParameters
                    {
                        spacing = roadSignSpacing,
                        category = roadSignCategory,
                        collisionLayerMask = sideWalkLayerMask,
                        parent = parentObject.transform,
                        offsetFromPath = roadSignOffset,
                        rotationFromPath = quaternion.identity
                    });
                }

                if (placePoissonTrees)
                {
                    pipeline.RunGenerator<PoissonDiscSamplingPlacementSystem, PlacementSystemParameters>(new PlacementSystemParameters
                    {
                        spacing = poissonTreeSpacing,
                        category = treeCategory,
                        collisionLayerMask = terrainLayerMask,
                        parent = parentObject.transform,
                        offsetFromPath = poissonOuterPolygonOffset,
                        rotationFromPath = quaternion.identity
                    });
                }

                return parentObject;
            }
        }
    }
}
