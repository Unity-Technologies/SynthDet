using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [DisableAutoCreation]
    public class GenerateMeshSystem : ComponentSystem, IGeneratorSystem
    {
        protected override void OnUpdate()
        {
            GameObject roadObject = new GameObject($"Geometry");   
            Entities.ForEach((ref RoadLane roadLane, DynamicBuffer<LaneVertex> vertices) =>
            {
                int sampleIdx = 0;
                //Arbitrary large number to prevent resizing over and over
                var samples = new NativeList<PointSampleGlobal>(1028, Allocator.Temp);
                do
                {
                    var geometryIndex = vertices[sampleIdx].GeometryIndex;
                    for (; sampleIdx < vertices.Length; sampleIdx++)
                    {
                        var vertex = vertices[sampleIdx];
                        if (vertex.GeometryIndex != geometryIndex)
                            break;
                        throw new NotImplementedException("laneVertex coordinate system needs to be fixed.");
                        //samples.Add(new PointSampleGlobal(new Vector2(vertex.Position.x, vertex.Position.z), vertex.Heading));
                    }
                    var mesh = RoadNetworkMesher.MeshSamples(new NativeSlice<PointSampleGlobal>(samples), 1.5f);

                    GameObject go = new GameObject(roadLane.Name.ToString());
                    go.transform.parent = roadObject.transform;
                    go.AddComponent<MeshRenderer>();
                    var meshFilter = go.AddComponent<MeshFilter>();
                    meshFilter.mesh = mesh;
                    
                    samples.Clear();
                } while (sampleIdx < vertices.Length);
                
                samples.Dispose();
            });
        }
    }
}