using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SimViz.Scenarios;

/*public class DirectionalLightVariation : IndexedPermuter
{
    public override int Count => 6;

    static void SetDirectionalLight(Light light, float latitude, float time)
    {
        Vector3 sunTilt = Quaternion.Slerp(Quaternion.Euler(0, 0, 0), Quaternion.Euler(0,0,90), latitude) * Vector3.right;
        light.transform.rotation = Quaternion.AngleAxis(time * 180, sunTilt);
    }

    void OnEnable()
    {
        // Find directional light
        const float latitude = 0.5f;
        var sun = FindObjectsOfType<Light>().First(l => l.type == LightType.Directional);
        SetDirectionalLight(sun, latitude, (float) CurrentPermutation / (float) (Count - 1));
    }
}*/
