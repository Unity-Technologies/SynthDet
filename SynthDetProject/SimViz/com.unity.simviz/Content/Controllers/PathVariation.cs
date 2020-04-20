using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SimViz.Scenarios;

[CreateAssetMenu]
public class PathVariation : ParameterSet
{
    public GuidReference moverObject = null;
    public GuidReference variation = null;

    public override void ApplyParameters()
    {
        var path = variation.gameObject.GetComponent<WaypointPath>();
        moverObject?.gameObject.GetComponent<Mover>()?.UpdatePath(path);
    }
}
