# Overview
The Simulation SDK is a set of classes that facilitate the capture of various data sources, such as textures, cameras, logs, etc. and uploading them to the cloud.

Some of these resources can be tricky to get from the player in a performant manner, without impacting the performance of the player itself. This SDK aims to provide methods to access and archive this data with minimal performance impact to the player.

The design of the SDK is asynchronous in nature. The capture methods will execute immediately, returning a request object that can be queried for completion or error status. These requests generally execute in the background on the system threadpool.

In some cases, where there is no support for capturing something in an asynchronous manner, part or all of the capture request may be performed synchronously. In these cases, the request will be marked completed before it is returned to the caller.


## CaptureCamera

### CaptureState       

```
public struct CaptureState
{
    public object colorBuffer;
    public object depthBuffer;
    public object motionVectorsBuffer;

    public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> colorFunctor;
    public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> depthFunctor;
    public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> motionVectorsFunctor;
}
```

Summary: CaptureState is a struct used by all the CaptureCamera methods as the payload for the AsyncRequest<T> that is returned. When the request has completed without error, the buffer objects will be byte arrays of the data that was captured.
