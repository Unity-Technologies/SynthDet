using Unity.Collections;
using UnityEngine;

public class LayoutRotations : MonoBehaviour
{
    public GameObject prefab;
    // Start is called before the first frame update
    void Start()
    {
        var outOfPlaneParent = new GameObject("Out of plane test");
        var outOfPlaneRotations = ObjectPlacementUtilities.GenerateOutOfPlaneRotationCurriculum(Allocator.Temp);
        foreach (var rotation in outOfPlaneRotations)
        {
            var instance = Instantiate(prefab, outOfPlaneParent.transform);
            instance.transform.localRotation = rotation;
        }


        var inPlaneParent = new GameObject("In plane test");
        var inPlaneRotations = ObjectPlacementUtilities.GenerateInPlaneRotationCurriculum(Allocator.Persistent);
        for (var index = 0; index < inPlaneRotations.Length; index++)
        {
            var rotation = inPlaneRotations[index];
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = new Vector3(.2f, .2f, .2f);
            cube.transform.Translate(Vector3.down * index + Vector3.right * 5);
            cube.transform.localRotation = rotation;
            cube.transform.parent = inPlaneParent.transform;
        }

        var combinedParent = new GameObject("Combined Test Grouped By In Plane");
        combinedParent.transform.localPosition = new Vector3(0, 0, 10);

        for (var iInPlane = 0; iInPlane < inPlaneRotations.Length; iInPlane++)
        {
            var inPlaneCombinedParent = new GameObject("In Plane #" + iInPlane);
            inPlaneCombinedParent.transform.parent = combinedParent.transform;
            inPlaneCombinedParent.transform.localPosition = Vector3.right * (iInPlane * 2);
            for (var iOutOfPlane = 0; iOutOfPlane < outOfPlaneRotations.Length; iOutOfPlane++)
            {
                var rotation = ObjectPlacementUtilities.ComposeForegroundRotation(new CurriculumState()
                {
                    InPlaneRotationIndex = iInPlane,
                    OutOfPlaneRotationIndex = iOutOfPlane
                }, outOfPlaneRotations, inPlaneRotations);
                var instance = Instantiate(prefab, inPlaneCombinedParent.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = rotation;
            }
        }
        var combinedOutOfPlane = new GameObject("Combined Test Grouped By Out of Plane");
        combinedOutOfPlane.transform.localPosition = new Vector3(0, 0, 20);

        for (var iOutOfPlane = 0; iOutOfPlane < outOfPlaneRotations.Length; iOutOfPlane++)
        {
            var outOfPlaneCombinedParent = new GameObject("Out of Plane #" + iOutOfPlane);
            outOfPlaneCombinedParent.transform.parent = combinedOutOfPlane.transform;
            outOfPlaneCombinedParent.transform.localPosition = Vector3.right * (iOutOfPlane * 2);
            for (var iInPlane = 0; iInPlane < inPlaneRotations.Length; iInPlane++)
            {
                var rotation = ObjectPlacementUtilities.ComposeForegroundRotation(new CurriculumState()
                {
                    InPlaneRotationIndex = iInPlane,
                    OutOfPlaneRotationIndex = iOutOfPlane
                }, outOfPlaneRotations, inPlaneRotations);
                var instance = Instantiate(prefab, outOfPlaneCombinedParent.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = rotation;
            }
        }

        var combinedHeirParent = new GameObject("Combined Test Using Heirarchy");
        combinedHeirParent.transform.localPosition = new Vector3(0, 0, 40);

        for (var iInPlane = 0; iInPlane < inPlaneRotations.Length; iInPlane++)
        {
            var inPlaneCombinedParent = new GameObject("In Plane #" + iInPlane);
            inPlaneCombinedParent.transform.parent = combinedHeirParent.transform;
            inPlaneCombinedParent.transform.localPosition = Vector3.right * (iInPlane * 2);
            inPlaneCombinedParent.transform.localRotation = inPlaneRotations[iInPlane];
            for (var iOutOfPlane = 0; iOutOfPlane < outOfPlaneRotations.Length; iOutOfPlane++)
            {
                var instance = Instantiate(prefab, inPlaneCombinedParent.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = outOfPlaneRotations[iOutOfPlane];
            }
        }

        var combinedHeirInvParent = new GameObject("Combined Test Using Heirarchy");
        combinedHeirInvParent.transform.localPosition = new Vector3(0, 0, 60);

        for (var iOutOfPlane = 0; iOutOfPlane < outOfPlaneRotations.Length; iOutOfPlane++)
        {
            var outOfPlaneCombinedParent = new GameObject("Out of Plane #" + iOutOfPlane);
            outOfPlaneCombinedParent.transform.parent = combinedHeirInvParent.transform;
            outOfPlaneCombinedParent.transform.localPosition = Vector3.right * (iOutOfPlane * 2);
            outOfPlaneCombinedParent.transform.localRotation = outOfPlaneRotations[iOutOfPlane];
            for (var iInPlane = 0; iInPlane < inPlaneRotations.Length; iInPlane++)
            {
                var instance = Instantiate(prefab, outOfPlaneCombinedParent.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = inPlaneRotations[iInPlane];
            }
        }


        var combinedStackedParent = new GameObject("Combined Test All Stacked");
        combinedStackedParent.transform.localPosition = new Vector3(0, 0, 70);

        for (var iInPlane = 0; iInPlane < inPlaneRotations.Length; iInPlane++)
        {
            for (var iOutOfPlane = 0; iOutOfPlane < outOfPlaneRotations.Length; iOutOfPlane++)
            {
                var rotation = ObjectPlacementUtilities.ComposeForegroundRotation(new CurriculumState()
                {
                    InPlaneRotationIndex = iInPlane,
                    OutOfPlaneRotationIndex = iOutOfPlane
                }, outOfPlaneRotations, inPlaneRotations);
                var instance =  Instantiate(prefab, combinedStackedParent.transform, true);
                instance.name = $"InPlane: {iInPlane} OutOfPlane: {iOutOfPlane}";
                instance.transform.localRotation = rotation;
                instance.transform.localPosition = Vector3.zero;
            }
        }

        outOfPlaneRotations.Dispose();
        inPlaneRotations.Dispose();
    }
}
