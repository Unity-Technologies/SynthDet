using System.Collections;
using System.Collections.Generic;
using Unity.Simulation.DistributedRendering.Render;
using UnityEngine;

public class MaterialPropertyBlockUpdater : MonoBehaviour, IComponentSerializerProxy
{

    public string resourceTexturePath;
    public void ReadAndApply(GameObject obj, IMessageSerializer serializer)
    {
        var mpb = new MaterialPropertyBlock();
        var tex = Resources.Load<Texture2D>(serializer.ReadString());
        mpb.SetTexture("_BaseMap", tex);
        Debug.Assert(tex != null, "The texture asset is not present in resources directory: " + tex);
        gameObject.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
        gameObject.GetComponentInChildren<MeshRenderer>().SetPropertyBlock(mpb);
    }

    public void WriteCustom(IMessageSerializer serializer)
    {
        serializer.Write(resourceTexturePath);
    }
}
