using System;
using System.Collections.Generic;
using System.Linq;
using Poly2Tri;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.MapElements;

namespace UnityEngine.SimViz.Content.Sampling
{
    
    public class RoadNetworkMesher : MonoBehaviour
    {
        internal static readonly float LaneWidth = .1f;
        internal static readonly float RoadWidth = 1.5f;

        internal static GameObject GenerateMesh(RoadNetworkDescription roadNetworkDescription)
        {
            GameObject container = new GameObject($"{roadNetworkDescription.name} (Road Network Mesh)");
            foreach (var road in roadNetworkDescription.AllRoads)
            {
                GenerateMeshForRoad(container, road);
            }

            return container;
        }

        public static void GenerateMeshForRoad(GameObject container, Road road, float samplesPerMeter = 3f)
        {
            GameObject roadObject = new GameObject($"Road {road.roadId}");
            roadObject.transform.parent = container.transform;

            foreach (var geometry in road.geometry)
            {
#if DEBUG_XODF_MESH
                    var mesh = BuildMeshForGeometry(geometry, ((polygonPoints, vertices, indices) =>
                    {
                        AddDebugRectangles(default, polygonPoints, vertices, indices);
                    }));
#else
                var mesh = BuildMeshForGeometry(geometry, samplesPerMeter);
#endif

                GameObject go = new GameObject($"Geometry {geometry.geometryKind}");
                go.transform.parent = roadObject.transform;
                go.AddComponent<MeshRenderer>();
                var meshFilter = go.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
            }
        }

        internal static Mesh BuildMeshForGeometry(Geometry geometry, float samplesPerMeter,
            Action<IList<TriangulationPoint>, List<Vector3>, List<int>> meshModifier = null)
        {
            var samples = GeometrySampling.BuildSamplesFromGeometry(geometry, samplesPerMeter);
            var mesh =  MeshSamples(samples, RoadWidth, meshModifier);
            samples.Dispose();
            return mesh;
        }


        internal static Mesh MeshSamples(NativeSlice<PointSampleGlobal> samples, float width,
            Action<IList<TriangulationPoint>, List<Vector3>, List<int>> meshModifier = null)
        {
            var halfWidth = width / 2f;
            // TODO: Refactor for unity.Mathematics
            var points = new PolygonPoint[samples.Length * 2];
            for (var i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                var heading = sample.pose.rot;
                var position = sample.pose.pos;
                
                var rightOffset = math.mul(heading, Vector3.right) * halfWidth;
                points[i] = ToPolygonPoint(position + rightOffset);

                var leftOffset = math.mul(heading, Vector3.left) * halfWidth;
                points[points.Length - 1 - i] = ToPolygonPoint(position + leftOffset);
            }

            Polygon poly = new Polygon(points);

            return PolygonToMesh(poly, meshModifier: meshModifier);
        }

        public static Mesh PolygonToMesh(Polygon poly, float uvMultiplier = 1, Action<IList<TriangulationPoint>, List<Vector3>, List<int>> meshModifier = null)
        {
            // Triangulate it!  Note that this may throw an exception if the data is bogus.
            var tcx = new DTSweepContext();
            tcx.PrepareTriangulation(poly);
            DTSweep.Triangulate(tcx);

            var codeToIndex = new Dictionary<uint, int>();
            var vertexList = new List<Vector3>();

            foreach (DelaunayTriangle t in poly.Triangles)
            {
                foreach (var p in t.Points)
                {
                    if (codeToIndex.ContainsKey(p.VertexCode)) continue;
                    codeToIndex[p.VertexCode] = vertexList.Count;
                    var pos = ToVector3(p);
                    vertexList.Add(pos);
                }
            }

            // Create the indices array
            List<int> indices = new List<int>(poly.Triangles.Count * 3);
            {
                foreach (DelaunayTriangle t in poly.Triangles)
                {
                    indices.Add(codeToIndex[t.Points[2].VertexCode]);
                    indices.Add(codeToIndex[t.Points[1].VertexCode]);
                    indices.Add(codeToIndex[t.Points[0].VertexCode]);
                }
            }

            meshModifier?.Invoke(poly.Points, vertexList, indices);

            // Create UV's
            var uvs = new Vector2[vertexList.Count];
            for (int i = 0; i < vertexList.Count; i++)
            {
                var v = vertexList[i];
                uvs[i] = new Vector2(v.x, v.z) * uvMultiplier;
            }

            // Create the mesh
            var msh = new Mesh
            {
                vertices = vertexList.ToArray(),
                triangles = indices.ToArray(),
                uv = uvs
            };

            //msh.uv = uv;
            msh.RecalculateNormals();
            msh.RecalculateBounds();
            return msh;
        }

        private static void AddDebugRectangles(NativeArray<PointSampleGlobal> samples, PolygonPoint[] points, List<Vector3> vertexList, List<int> indices)
        {
            //draw a small triangle around each sample
            foreach (var sample in samples)
            {
                var pos = sample.pose.pos;
                var idxStart = vertexList.Count;
                var backLeft = math.normalize(-Vector3.forward - Vector3.right);
                var backRight = math.normalize(-Vector3.forward + Vector3.right);
                vertexList.Add(pos + backLeft * .2f);
                vertexList.Add(pos);
                vertexList.Add(pos + backRight * .2f);
                indices.Add(idxStart);
                indices.Add(idxStart + 1);
                indices.Add(idxStart + 2);
            }

            //draw a small square around each expected vertex
            foreach (var point in points)
            { 
                var pos = ToVector3(point);
                var idxStart = vertexList.Count;
                vertexList.Add(pos - Vector3.right * .2f);
                vertexList.Add(pos + Vector3.forward * .2f);
                vertexList.Add(pos + Vector3.right * .2f);
                vertexList.Add(pos - Vector3.forward * .2f);
                indices.Add(idxStart);
                indices.Add(idxStart + 1);
                indices.Add(idxStart + 2);
                indices.Add(idxStart);
                indices.Add(idxStart + 2);
                indices.Add(idxStart + 3);
            }
        }

        internal static GameObject GenerateMeshWithLanes(RoadNetworkDescription roadNetworkDescription, 
            float samplesPerMeter = 3f)
        {
            var name = roadNetworkDescription.name != "" ? roadNetworkDescription.name : "Road Network";
            var container = new GameObject(name);
            foreach (var road in roadNetworkDescription.AllRoads)
            {
                BuildLanesMeshFromRoad(container, road, samplesPerMeter);
            }

            return container;
        }

        private static void BuildLanesMeshFromRoad(GameObject container, Road road, float samplesPerMeter)
        {
            if (road.geometry.Count == 0)
                throw new Exception($"The road {road} has no reference line.");
            
            var roadObject = new GameObject($"Road {road.roadId}");
            roadObject.transform.parent = container.transform;
            var samplingState = new SamplingStateRoad(road, samplesPerMeter);

            for (var sectionIndex = 0; sectionIndex < road.laneSections.Count; sectionIndex++)
            {
                samplingState.SelectLaneSection(road, sectionIndex);
                var goLaneSection = BuildMeshesForLaneSection(road, samplingState);

                goLaneSection.transform.parent = roadObject.transform;
            }
        }

        private static GameObject BuildMeshesForLaneSection(Road road, SamplingStateRoad state)
        {
            var goLanes = new GameObject($"Lane Section {state.laneSectionIdx}");

            // NOTE: Because we effectively sample along one line at a time, we're doing numLanes sampling passes along
            //       the same segment of road line, so we can only pass a copy of the sampling state
            BuildMeshesForLaneSectionSide(road, state, Side.Left, goLanes);
            BuildMeshesForLaneSectionSide(road, state, Side.Right, goLanes);

            return goLanes;
        }

        private static void BuildMeshesForLaneSectionSide(Road road, SamplingStateRoad state, Side side, 
            GameObject goLaneSection)
        {
            var laneSection = road.laneSections[state.laneSectionIdx];
            var lanes = GeometrySampling.GetLaneSectionSide(laneSection, side);
            var numLanes = lanes?.Count() ?? 0;

            if (numLanes == 0)
                return;
            
            var samples = GeometrySampling.BuildSamplesForLaneSectionSide(road, state, side);
            // NOTE: We assume this divides evenly, but if it does not, we may end up with samples in the wrong mesh
            var sampleCountPerLane = samples.Length / numLanes;
            var insertIdx = 0;

            foreach (var lane in lanes)
            {
                var mesh = MeshSamples(new NativeSlice<PointSampleGlobal>(samples, insertIdx,
                    sampleCountPerLane), LaneWidth);

                var goLane = new GameObject($"Lane Section {lane.id}");
                goLane.transform.parent = goLaneSection.transform;
                goLane.AddComponent<MeshRenderer>();
                var meshFilter = goLane.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                insertIdx += sampleCountPerLane;
            }
        }

        private class GeometrySComparer : IComparer<Geometry>
        {
            public static readonly GeometrySComparer instance = new GeometrySComparer();
            public int Compare(Geometry x, Geometry y)
            {
                return x.sRoad.CompareTo(y.sRoad);
            }
        }

        internal static GameObject GenerateLineRenderer(RoadNetworkDescription roadNetworkDescription, float samplesPerMeter = 3f)
        {
            GameObject container = new GameObject("OpenDRIVE Line Rendering");
            foreach (var road in roadNetworkDescription.AllRoads)
            {
                GameObject roadObject = new GameObject($"road {road.roadId}"); 
                roadObject.transform.parent = container.transform;

                foreach (var geometry in road.geometry)
                {
                    GameObject go = new GameObject($"Geometry {geometry.geometryKind}");
                    go.transform.parent = roadObject.transform;
                    go.AddComponent<MeshRenderer>();
                    var lineRenderer = go.AddComponent<LineRenderer>();
                    var samples = GeometrySampling.BuildSamplesFromGeometry(geometry, samplesPerMeter);
                    lineRenderer.positionCount = samples.Length;
                    lineRenderer.SetPositions(samples.Select(s => (Vector3)s.pose.pos).ToArray());
                    lineRenderer.startWidth = lineRenderer.endWidth = .05f;
                    lineRenderer.useWorldSpace = false;
                }
            }

            return container;
        }

        static internal PolygonPoint ToPolygonPoint(Vector3 position)
        {
            return new PolygonPoint(position.x, position.z);
        }

        static internal Vector3 ToVector3<TPoint>(TPoint point) where TPoint : TriangulationPoint
        {
            return new Vector3(point.Xf, 0, point.Yf);
        }
    }
}