using System.Collections;
using System.Collections.Generic;
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

        outOfPlaneRotations.Dispose();
        
        
        var inPlaneParent = new GameObject("In plane test");
        var list = ObjectPlacementUtilities.GenerateInPlaneRotationCurriculum(Allocator.Persistent);
        for (var index = 0; index < list.Length; index++)
        {
            var rotation = list[index];
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = new Vector3(.2f, .2f, .2f);
            cube.transform.Translate(Vector3.down * index + Vector3.right * 5);
            cube.transform.localRotation = rotation;
            cube.transform.parent = inPlaneParent.transform;
        }

        list.Dispose();
    }
}
