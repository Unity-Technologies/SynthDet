using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDataProvider
{
    /// <summary>
    /// 
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    byte[] GetFrameData();

    /// <summary>
    /// 
    /// </summary>
    void Shutdown();
}
