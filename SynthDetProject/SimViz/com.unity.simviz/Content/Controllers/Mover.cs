using QuickGraph;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SimViz.Scenarios;

public class Mover : MonoBehaviour
{
    [Header("Movement")]
    public WaypointPath waypointPath;

    [Header("Parameters")]
    public float speed = 5f;
    public float speedRandom = 0f;
    public float speedWheelsMultiplier = 1f;
    public float rotationSpeed = 5;

    public float position = 0.0f;
    public bool stopRotation = false;

    [Header("Random")]
    public float randomRange = 0;
    public float randomPos = 0;

    [Header("Wheels")]
    public Transform[] wheels;
    public Vector3 wheelRotationAxis = new Vector3(0, 0, 1);


    [Header("Trailer")]
    public Transform trailer;
    public float lerpSpeedTrailer = 1;
    public int rotationsCheckNumber = 5;

    [Header("Additional settings")]
    public bool raycastPosition = false;
    public bool pathLogging = false;
    public bool debug = false;


    private List<float> trailerRotations = new List<float>();

    internal Vector3 newPosition;
    internal Vector3 oldPosition;

    internal Vector3 originPoint;
    internal Vector3 destinationPoint;

    public float sideOffset = 0;

    private static List<float> randomList = new List<float>();
    private List<Vector3> splinePoints = new List<Vector3>();

    private MovementDirection movementDirection = MovementDirection.Forward;
    private Vector3 pointGraphOrigin;
    private Vector3 previousPoint;
    private Vector3 sourcePoint;
    private Vector3 destPoint;
    private Vector3 nextPoint;
    private int segmentPointIndex = 0;
    // Indicates the next position at which a decision was made.  This is tracked because decisions are made prior to arriving at intersection points.
    Vector3 decisionPosition = Vector3.negativeInfinity;
    private BidirectionalGraph<Vector3, Edge<Vector3>> controlPointGraph = new BidirectionalGraph<Vector3, Edge<Vector3>>();


    public static float GetRandomFromList()
    {

        if (randomList == null)
        {
            randomList = new List<float>();
        }

        if (randomList.Count <= 0)
        {

            Random.InitState(System.DateTime.Now.Millisecond);
            for (int i = 0; i < 2000; i++)
            {
                randomList.Add(Random.value);
            }
        }

        float value = randomList[0];
        randomList.RemoveAt(0);
        return value;
    }

    List<Vector3> GenerateCatmullRomSpline()
    {

        List<Vector3> positions = new List<Vector3>();
        float splineLength = 0;

        Vector3 p0 = previousPoint;
        Vector3 p1 = sourcePoint;
        Vector3 p2 = destPoint;
        Vector3 p3 = nextPoint;

        Vector3 lastPos = p1;

        int loops = Mathf.FloorToInt(1f / waypointPath.resolution);

        for (int i = 1; i <= loops; i++)
        {
            float t = i * waypointPath.resolution;

            Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);

            positions.Add(newPos);


            splineLength += Vector3.Distance(lastPos, newPos);

            lastPos = newPos;
        }
        waypointPath.length += splineLength;
        return positions;
    }

    Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {

        Vector3 a = 2f * p1;
        Vector3 b = p2 - p0;
        Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;

        //The cubic polynomial: a + b * t + c * t^2 + d * t^3
        Vector3 pos = 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));

        return pos;
    }

    public void BuildGraph()
    {
        controlPointGraph.Clear();

        // Iterate over each segment
        for (int segmentIndex = 0; segmentIndex < waypointPath.GetSegmentCount(); segmentIndex++)
        {
            // Add each vertex to the graph with its successor.
            for (int pointIndex = 1; pointIndex < waypointPath.GetPointCount(segmentIndex); pointIndex++)
            {
                var firstPoint = waypointPath.GetPoint(segmentIndex, pointIndex - 1).Point;
                var secondPoint = waypointPath.GetPoint(segmentIndex, pointIndex).Point;
                var fromVertex = movementDirection == MovementDirection.Forward ? firstPoint : secondPoint;
                var toVertex = movementDirection == MovementDirection.Forward ? secondPoint : firstPoint;
                controlPointGraph.AddVerticesAndEdge(new Edge<Vector3>(fromVertex, toVertex));
            }
        }

        pointGraphOrigin = waypointPath.GetPoint(0, 0).Point;
    }

    public void SetMoveDirection(MovementDirection direction)
    {
        var oldDirection = movementDirection;
        movementDirection = direction;
        if (movementDirection != oldDirection)
        {
            // Movement direction was changed - rebuild graph in reverse order.
            BuildGraph();
        }
    }

    public MovementDirection GetMoveDirection()
    {
        return movementDirection;
    }

    public BidirectionalGraph<Vector3, Edge<Vector3>> GetPointGraph()
    {
        return controlPointGraph;
    }

    private void updateSegment()
    {
        var outDegree = controlPointGraph.OutDegree(nextPoint);

        previousPoint = sourcePoint;
        sourcePoint = destPoint;
        destPoint = nextPoint;

        if (outDegree == 0)
        {
            // On graphs that are not fully closed, we will just reuse the nextPoint indefinitely.
        }
        else if (outDegree == 1)
        {
            // No decision to make, just use the out edge to determine upcoming point
            nextPoint = controlPointGraph.OutEdge(nextPoint, 0).Target;
        }
        else
        {
            // TODO:  Highway scene has some mover objects with consecutive duplicate points.  Need to remove those.

            // Need to pick an edge, randomly for now.
            System.Random r = new System.Random();
            var index = r.Next(outDegree);
            nextPoint = controlPointGraph.OutEdge(nextPoint, index).Target;
            decisionPosition = destPoint;
        }

        // Log decisions
        if (pathLogging && decisionPosition == sourcePoint)
        {
            CarScenarioLogger.Instance.LogRouteSelection(waypointPath.FindPointByPosition(sourcePoint), waypointPath.FindPointByPosition(destPoint));
            decisionPosition = Vector3.negativeInfinity;
        }


        // Rebuild segment interpolation points.
        splinePoints = GenerateCatmullRomSpline();
        segmentPointIndex = 0;
    }

    private void initSegment()
    {
        oldPosition = newPosition = Vector3.zero;

        // Initialize graph
        BuildGraph();

        // Determine control points for splining.  Note that for intersections, the spline is different depending on which origin is used.
        // This naive implementation just picks an edge.
        previousPoint = controlPointGraph.InDegree(pointGraphOrigin) == 0 ? pointGraphOrigin : controlPointGraph.InEdge(pointGraphOrigin, 0).Source;
        sourcePoint = pointGraphOrigin;
        destPoint = controlPointGraph.OutDegree(sourcePoint) == 0 ? pointGraphOrigin : controlPointGraph.OutEdge(sourcePoint, 0).Target;
        nextPoint = controlPointGraph.OutDegree(destPoint) == 0 ? destPoint : controlPointGraph.OutEdge(destPoint, 0).Target;

        // Create segment points from spline.
        splinePoints = GenerateCatmullRomSpline();
        segmentPointIndex = 0;
    }

    public Vector3 GetNextInterpolatedPoint()
    {
        if (splinePoints.Count == 0)
        {
            initSegment();
        }
        if (segmentPointIndex >= splinePoints.Count)
        {
            updateSegment();
        }
        var point = splinePoints[segmentPointIndex++];
        return point;
    }

    public void UpdatePath(WaypointPath path)
    {
        waypointPath = path;
        splinePoints.Clear();
        position = 0;
        originPoint = GetNextInterpolatedPoint();
        destinationPoint = GetNextInterpolatedPoint();
    }

    new Rigidbody rigidbody;
    // Use this for initialization
    void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.isKinematic = false;
        }

        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
        }

        if (randomRange != 0)
            sideOffset = (randomRange * GetRandomFromList() * 2 - randomRange) * 0.5f;

        if (speed < 0)
        {
            SetMoveDirection(MovementDirection.Backward);
        }
        else
        {
            SetMoveDirection(MovementDirection.Forward);
        }

        speed += (speedRandom * GetRandomFromList() * 2 - speedRandom) * 0.5f;

        position += GetRandomFromList() * randomPos * System.Math.Abs(speed);

        originPoint = GetNextInterpolatedPoint();
        destinationPoint = GetNextInterpolatedPoint();

    }

    private void MoveNextDestination()
    {
        originPoint = destinationPoint;
        destinationPoint = GetNextInterpolatedPoint();
    }

    // Update is called once per frame
    void UpdatePosition()
    {
        oldPosition = newPosition;

        float length = Vector3.Distance(originPoint, destinationPoint);

        position += System.Math.Abs(speed) * Time.fixedDeltaTime;

        while (length > float.Epsilon && position / (float)length > 0.9999f)
        {
            MoveNextDestination();
            position = position - length;
            length = Vector3.Distance(originPoint, destinationPoint);
        }


        newPosition = Vector3.Lerp(originPoint, destinationPoint, position / (float)length);

        if (debug)
        {
            var log = string.Format("{0} moved to {1}, {2}, {3}\r\n", name, newPosition.x, newPosition.y, newPosition.z);
            Debug.Log(log);
        }

        var lookVec = speed > 0 ? newPosition - oldPosition : oldPosition - newPosition;
        if (lookVec != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookVec), rotationSpeed * Time.fixedDeltaTime);
        }

        if (stopRotation)
        {
            Vector3 euler = transform.eulerAngles;
            euler.x = 0;
            euler.z = 0;
            transform.eulerAngles = euler;
        }

        if (rigidbody == null)
        {
            if (raycastPosition)
            {
                Ray ray = new Ray(newPosition + Vector3.up * 100, Vector3.down);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                    newPosition.y = hit.point.y;
            }

            transform.position = newPosition;
            transform.position += transform.right * sideOffset;
        }

        if (wheels != null)
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null)
                {
                    wheels[i].Rotate(wheelRotationAxis * (-speedWheelsMultiplier * speed * Time.fixedDeltaTime));
                }
                else if (wheels[i] == null)
                {
                    Debug.LogErrorFormat("Wheel {0} in the array is null, verify the element is not empty", wheels[i]);
                }
            }
        }

        if (trailer != null)
        {
            trailerRotations.Add(transform.eulerAngles.y);


            if (trailerRotations.Count > rotationsCheckNumber)
                trailerRotations.RemoveAt(0);


            trailer.localRotation = Quaternion.Slerp(trailer.localRotation,
                Quaternion.Euler(0, 0, trailerRotations.Average() - transform.eulerAngles.y), lerpSpeedTrailer * Time.fixedDeltaTime);
        }

    }

    Quaternion AverageQuaternion(List<Quaternion> qArray)
    {
        Quaternion qAvg = qArray[0];
        float weight;
        for (int i = 1; i < qArray.Count; i++)
        {
            weight = 1.0f / (float)(i + 1);
            qAvg = Quaternion.Slerp(qAvg, qArray[i], weight);
        }
        return qAvg;
    }

    private void FixedUpdate()
    {
        UpdatePosition();
        if (rigidbody != null)
        {
            rigidbody.MovePosition(newPosition + transform.right * sideOffset);
        }
    }


    private void OnDrawGizmosSelected()
    {
        if (!debug)
            return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(oldPosition, newPosition);
        Gizmos.DrawSphere(oldPosition, 0.1f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + (newPosition - transform.position).normalized * 100);
        Gizmos.DrawSphere(newPosition, 0.1f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 100);

        if (waypointPath.controlPoints.Count > 1)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(originPoint, 0.1f);
            Gizmos.DrawSphere(destinationPoint, 0.1f);
        }

        // For now, just draw lines to control points instead of splining since this is a debug view.

        var graph = GetPointGraph();
        if (graph != null)
        {
            foreach (var vertex in graph.Vertices)
            {
                Gizmos.DrawSphere(vertex, 0.1f);
            }
            foreach (var edge in graph.Edges)
            {
                Gizmos.DrawLine(edge.Source, edge.Target);
            }
        }
    }
}
