//	Poly2Mesh
//
//	This is a static class that wraps up all the details of creating a Mesh
//	(or even an entire GameObject) out of a polygon.  The polygon must be
//	planar, and should be well-behaved (no duplicate points, etc.), but it
//	can have any number of non-overlapping holes.  In addition, the polygon
//	can be in ANY plane -- it doesn't have to be the XY plane.  Huzzah!
//
//	To use:
//		1. Create a Poly2Mesh.Polygon.
//		2. Set its .outside to a list of Vector3's describing the outside of the shape.
//		3. [Optional] Add to its .holes list as desired.
//		4. [Optional] Call CalcPlaneNormal on it, passing in a hint as to which way you
//			want the polygon to face.
//		5. Pass it to Poly2Mesh.CreateMesh, or Poly2Mesh.CreateGameObject.

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Poly2Tri;

public static class Poly2Mesh {

	// Polygon: defines the input to the triangulation algorithm.
	public class Polygon {
		// outside: a set of points defining the outside of the polygon
		public List<Vector3> outside;

		// holes: a (possibly empty) list of non-overlapping holes within the polygon
		public List<List<Vector3>> holes;

		// normal to the plane containing the polygon (normally calculated by CalcPlaneNormal)
		public Vector3 planeNormal;

		// rotation into the XY plane (normally calculated by CalcRotation)
		public Quaternion rotation = Quaternion.identity;

		// constructor (just initializes the lists)
		public Polygon() {
			outside = new List<Vector3>();
			holes = new List<List<Vector3>>();
		}

		/// <summary>
		/// Calculates the plane normal for this polygon, trying to face towards -Z.
		/// </summary>
		public void CalcPlaneNormal() {
			CalcPlaneNormal(Vector3.back);
		}

		/// <summary>
		/// Calculates the plane normal for this polygon, trying to face the given direction.
		/// </summary>
		/// <param name="hint">Direction which you'd like the polygon to face as closely as possible.</param>
		public void CalcPlaneNormal(Vector3 hint) {
			planeNormal = Vector3.Cross(outside[1]-outside[0], outside[2]-outside[0]);
			planeNormal.Normalize();
			if (Vector3.Angle(planeNormal, hint) > Vector3.Angle(-planeNormal, hint)) {
				planeNormal = -planeNormal;
			}
		}

		/// <summary>
		/// Calculates the rotation needed to get this polygon into the XY plane.
		/// </summary>
		public void CalcRotation() {
			if (planeNormal == Vector3.zero) CalcPlaneNormal();
			if (planeNormal == Vector3.forward) {
				// Special case: our polygon is already in the XY plane, no rotation needed.
				rotation = Quaternion.identity;
			} else {
				rotation = Quaternion.FromToRotation(planeNormal, Vector3.forward);
			}
		}
	}

	/// <summary>
	/// Helper method to convert a set of 3D points into the 2D polygon points
	/// needed by the Poly2Tri code.
	/// </summary>
	/// <returns>List of 2D PolygonPoints.</returns>
	/// <param name="points">3D points.</param>
	/// <param name="rotation">Rotation needed to convert 3D points into the XY plane.</param>
	static List<PolygonPoint> ConvertPoints(List<Vector3> points, Quaternion rotation) {
		int count = points.Count;
		List<PolygonPoint> result = new List<PolygonPoint>(count);
		for (int i=0; i<count; i++) {
			Vector3 p = rotation * points[i];
			result.Add(new PolygonPoint(p.x, p.y));
		}
		return result;
	}

	/// <summary>
	/// Create a Mesh from a given Polygon.
	/// </summary>
	/// <returns>The freshly minted mesh.</returns>
	/// <param name="polygon">Polygon you want to triangulate.</param>
	public static Mesh CreateMesh(Polygon polygon) {

		// Ensure we have the rotation properly calculated
		if (polygon.rotation == Quaternion.identity) polygon.CalcRotation();

		// Rotate 1 point and note where it ends up in Z
		float z = (polygon.rotation * polygon.outside[0]).z;

		// Convert the outside points (throwing out Z at this point)
		Poly2Tri.Polygon poly = new Poly2Tri.Polygon(ConvertPoints(polygon.outside, polygon.rotation));

		// Convert each of the holes
		foreach (List<Vector3> hole in polygon.holes) {
			poly.AddHole(new Poly2Tri.Polygon(ConvertPoints(hole, polygon.rotation)));
		}

		// Triangulate it!  Note that this may throw an exception if the data is bogus.
		DTSweepContext tcx = new DTSweepContext();
		tcx.PrepareTriangulation(poly);
		DTSweep.Triangulate(tcx);
		tcx = null;

		// Create the Vector3 vertices (undoing the rotation),
		// and also build a map of vertex codes to indices
		Quaternion invRot = Quaternion.Inverse(polygon.rotation);
		Dictionary<uint, int> codeToIndex = new Dictionary<uint, int>();
		List<Vector3> vertexList = new List<Vector3>();
		foreach (DelaunayTriangle t in poly.Triangles) {
			foreach (var p in t.Points) {
				if (codeToIndex.ContainsKey(p.VertexCode)) continue;
				codeToIndex[p.VertexCode] = vertexList.Count;
				Vector3 pos = new Vector3(p.Xf, p.Yf, z);	// (restore the Z we saved earlier)
				vertexList.Add(invRot * pos);
			}
		}
		
		// Create the indices array
		int[] indices = new int[poly.Triangles.Count * 3];
		{
			int i = 0;
			foreach (DelaunayTriangle t in poly.Triangles) {
				indices[i++] = codeToIndex[t.Points[0].VertexCode];
				indices[i++] = codeToIndex[t.Points[1].VertexCode];
				indices[i++] = codeToIndex[t.Points[2].VertexCode];
			}
		}

		// Create the UV list, by looking up the closest point for each in our poly
		Vector2[] uv = null;
//		if (polygon.outsideUVs != null) {
//			uv = new Vector2[vertexList.Count];
//			for (int i=0; i<vertexList.Count; i++) {
//				uv[i] = polygon.ClosestUV(vertexList[i]);
//			}
//		}
		
		// Create the mesh
		Mesh msh = new Mesh();
		msh.vertices = vertexList.ToArray();
		msh.triangles = indices;
		msh.uv = uv;
		msh.RecalculateNormals();
		msh.RecalculateBounds();
		return msh;
	}

	/// <summary>
	/// Create a GameObject from the given polygon.  The resulting object will
	/// have a Mesh, a MeshFilter, and a MeshRenderer.  It will be all ready
	/// to display (though you may want to add your own material).
	/// </summary>
	/// <returns>The newly created game object.</returns>
	/// <param name="polygon">Polygon you want to triangulate.</param>
	/// <param name="name">Name to assign to the new GameObject.</param>
	public static GameObject CreateGameObject(Polygon polygon, string name="Polygon") {
		GameObject gob = new GameObject();
		gob.name = name;
		gob.AddComponent(typeof(MeshRenderer));
		MeshFilter filter = gob.AddComponent(typeof(MeshFilter)) as MeshFilter;
		filter.mesh = CreateMesh(polygon);
		return gob;
	}
}
