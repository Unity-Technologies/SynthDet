using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SimViz.Scenarios;

[CreateAssetMenu]
public class SunAngle : ParameterSet
{
    public GameObject directionalLightPrefab;

    [Range(0, 24f)]
    public float hour = 12.0f;

    [Range(0f, 1f)]
    [Tooltip("0.0 is Jan 1st, 1.0 is Dec 31st")]
    public float timeOfYear = 0.25f;

    [Tooltip("-90 is the south pole, 90 is the north pole")]
    [Range(-90f, 90f)]
    public float latitude = 0;

    GameObject directionalLight = null;

    const string objectName = "SunAngleObject";

    public override void ApplyParameters()
    {
        // If we do not have a handle to the directional light, see if we can find it.
        if (directionalLight == null)
        {
            directionalLight = GameObject.Find(objectName);

            // if not found, create it
            if (directionalLight == null)
            {
                directionalLight = Instantiate(directionalLightPrefab);
                directionalLight.name = objectName;
                directionalLight.AddComponent<SunAngleController>();
            }

            if (directionalLight == null)
            {
                throw new InvalidOperationException("Unable to create directional light");
            }
        }

        // Add sun angle controller and configure
        var controller = directionalLight.GetComponents<SunAngleController>().FirstOrDefault();
        if (controller != null)
        {
            controller.sunAngle = this;
            controller.SetSunAngle();
        }
    }
}
