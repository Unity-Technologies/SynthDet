using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Simulation.DistributedRendering.Render;
using UnityEngine;

public interface IComponentSerializerProxy
{
    /// <summary>
    /// Read data from the stream associated with the serializer and apply it to the Gameobject passed in
    /// </summary>
    /// <param name="obj">Gameobject to which the data is to be applied</param>
    /// <param name="serializer">Message serializer associated with the RenderNode</param>
    void ReadAndApply(GameObject obj, IMessageSerializer serializer);

    /// <summary>
    /// Write a Custom FrameData in addition to the transform position, rotation and scale.
    /// </summary>
    /// <param name="serializer">Serializer associated with the Scene Server.</param>
    void WriteCustom(IMessageSerializer serializer);
}
