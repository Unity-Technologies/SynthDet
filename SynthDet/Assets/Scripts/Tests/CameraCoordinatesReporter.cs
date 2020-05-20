using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

[ExecuteInEditMode]
public class CameraCoordinatesReporter : MonoBehaviour
{
    public Camera TheCamera;

    public Vector3 CoordsCameraSpace;
    public Vector3 CoordsNormalizedClipSpace;
    public Vector2 ViewCoordsComputed;
    public Vector2 ViewCoordsFromAPI;
    public Vector2 ScreenCoordsComputed;
    public Vector2 ScreenCoordsFromAPI;
    
    Vector4 m_W = new Vector4(0,0,0,1);

    void Update()
    {
        if (TheCamera == null)
            return;

//        var worldToClipSpace = TheCamera.nonJitteredProjectionMatrix * TheCamera.worldToCameraMatrix;
        var resolution = new Vector2(TheCamera.pixelWidth, TheCamera.pixelHeight);
        CoordsCameraSpace = TheCamera.worldToCameraMatrix.MultiplyPoint(transform.position);
        var posClip = TheCamera.nonJitteredProjectionMatrix.MultiplyPoint(CoordsCameraSpace);
        CoordsNormalizedClipSpace = new Vector3(posClip.x, posClip.y, posClip.z); // posClip.w;
        ViewCoordsComputed = ((Vector2)CoordsNormalizedClipSpace + Vector2.one) / 2f;
        ScreenCoordsComputed = ViewCoordsComputed * resolution;
        ScreenCoordsFromAPI = TheCamera.WorldToScreenPoint(transform.position);
        ViewCoordsFromAPI = TheCamera.WorldToViewportPoint(transform.position);
    }
}
