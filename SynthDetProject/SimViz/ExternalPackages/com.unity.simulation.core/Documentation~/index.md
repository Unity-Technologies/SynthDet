# Unity Simulation Core Documentation
## Table Of Contents

## Overview
The Simulation Core is a common set of classes that provides core functionality for the Simulation SDK and other products.

Please direct any questions or support requests to simulation-help@unity3d.com

## AsyncRequest

### Summary: 

AsyncRequest<T> is a templated class similar to Task<T>. The main difference is that AsyncRequest<T> is allocated from an object pool, and recycled on disposal, to keep memory allocation to a minimum. Task<T> also has the limitation (in Unity) of always executing on the main thread. AsyncRequest<T> can run either on the main thread, or on the system thread pool.

You can query whether or not a request has completed or not. Once completed, the passed in functors (if any) are called with the AsyncRequest<T> associated with the request. This generally happens on a different thread, so you must make sure not to invoke any Unity API methods (unless explicitly documented as thread safe) from the completion functor. When returning from the completion functor, you must either return AsyncRequest.Result.Completed or AsyncRequest.Result.Error.

```	
[Flags]
public enum Result
{
    None      = 0,
    Completed = (1<<0),
    Error     = (1<<1) | Completed // Error implies completed
}
```

### Properties
 ```
 public bool error
 ```
If an error occurs, then this property will be set to true. This will also set the completed property to true.

```
 public bool completed
```
If the capture request has completed, this property will be set to true.

```
public ref T data
```
A reference to the payload data for the request.


## BaseDataConsumer / SignedURLDataConsumer

BaseDataConsumer is the base class for consuming data produced by the SDK. SignedURLDataConsumer is the cloud agnostic consumer that gets a signed url and uploads data to the cloud.

There are three methods that you need to provide in a consumer.

```
public override bool Upload(string localPath, string objectPath)
```
Synchronously uploads the data at `localPath` to the cloud location specified by `objectPath`

```
public override Task<bool> UploadAsync(Stream source, string objectPath)
```	
Asynchronously upload data from a stream `source` to the cloud location specified by `objectPath`

```
public override string LocalPathToObjectPath(string localPath)
```
Takes a local path, and converts it to the objectPath where data is stored in the cloud.

## IDataProduced

Consumers that are interested in data that is produced by the SDK can implement this interface and register themselves with the manager.

```
Manager.Instance.StartNotification += () =>
{
    Manager.Instance.RegisterDataConsumer(new MyConsumer());
};
```

When Data is produced, all consumers are called with the path to the data.

```
bool Initialize();
```
Called when the consumer is registered. If the consumer should not be used, then return `false`

```
void Consume(object data, bool synchronous = false);
```
Called when any data is produced by the SDK. If the consumption should be synchronous, i.e. be completed by the time this function returns, then `synchronous ` is set to `true`

```
bool ConsumptionStillInProgress();
```
Called by the manager to check if consumption is still ongoing, i.e. uploads are in flight. The manager will not shut down until all uploads have completed.

## ContentType

Helper class to determine the content-type when uploading data.

```
public static string ForPath(string path)
```
Converts a path to content-type. Known types are image formats, text, and .raw for the profiler log.

## Logger

SDK logging system. All SDK logging is done through this logger which just forwards to Debug.Log or Console.WriteLine.

```
Log.I("some logging statement");
```
Supports I, W, E, F, V for Info, Warning, Error, Fatal, Verbose.

## SequencedPath

Utility class to track a sequence number, and increment it each time a path is generated.

## FileProduced

Utility class to write data either synchronously or asynchronously to disk. Data must be in an array of any type.

```
public static bool Write(string path, Array data, bool uploadSynchronously = false)
```

## Format

Utility class to format an array of floats and return a formatted string of all values truncated to `precision` number of digits.

## Manager

Main manager class for the SDK. This class tracks all data produced, handles uploads, waits for uploads to complete before shutting down etc.

```
public void RegisterDataConsumer(IDataProduced consumer)
public void UnregisterDataConsumer(IDataProduced consumer)
```
Register/Unregister a consumer with the SDK. When registering, the IDataProducer.Initialize method will be called. If this consumer should not run, then return false.

```
public NotificationDelegate StartNotification;
public NotificationDelegate ShutdownNotification;
```
Startup and Shutdown notification, you can add your own handlers to these to be notified of startup/shutdown.

```
public void QueueEndOfFrameItem(Action<object> callback, object functor)
```
Queues a work item to be invoked at the end of the frame.

```
public void ConsumerFileProduced(string filePath, bool synchronous = false)
```
This is an important method. This is how you tell the SDK that you have created some data that needs to be tracked and uploaded. All of the SDK features that produce data will call this automatically, but if you produce data of your own, then you must call this method manually to tell the manager about the data.

## TimerLogger

The time logger class outputs the current simulation and wall times to the log. The  logger can be configured to only output certain times, or none at all. Additionally the logging interval can be modified as well.

## Utilities

Various utilities to support the SDK in manipulating arrays without having to copy them each time.

## ChunkedUnityLog

The player log will be uploaded in entirety when your unity simulation run completes or crashes, so there is really no need to capture the Unity log separately. However, if you want the log to be uploaded (in chunks) in semi-real-time, then you can use this class to do so.

In the event of a crash, the application will be re-run by the Unity Simulation agent. If the SDK detects any files from the previous run that are still present, it will attempt to upload those at startup.


```
public static void Capture(
    Int bufferSize = kDefaultBufferSize,
    float maxElapsedSeconds = kDefaultMaxSecondsElapsed,
    Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null)
```
Begins capturing the Unity log output to a memory buffer. Each log line is encoded as JSON. When the log reaches bufferSize, or maxElapsedSeconds passes, it writes the data to the functor.

`bufferSize` Size of the memory buffer to hold logging data.
`maxElapsedSeconds` Time to wait until the buffer is dispatched even if not full.
`functor` Callback handler that is passed the buffered data.

```
public static void CaptureToFile(
    string path,
    bool addSequenceNumber = true,
    int bufferSize = kDefaultBufferSize,
    float maxElapsedSeconds = kDefaultMaxSecondsElapsed)
```
Begins capturing the Unity log output to a file. Each log line is encoded as a json object. Each time the buffer is full, or the maxElapsedSeconds has passed, this will write the captured data to a file at path, and add a sequence number to it. Each of the file pieces will be uploaded to GCS as they are written. In this way streaming data can be written to GCS as it happens.

`path` Where to write the file.
`addSequenceNumber` When true, each time the file writes a buffer segment, it will append N where N is an incrementing integer. This will prevent the file from overwriting itself.
`bufferSize` Size of the memory buffer to hold logging data.
`maxElapsedSeconds` Time to wait until the buffer is dispatched even if not full.

```
public static void EndCapture()
```
Stops capturing the Unity log. Any buffered data is flushed before returning.


```
public static void SetLogStackTracing(StackTraceLogType logType)
```
Sets all the Unity debug log levels to logType. This can be used to disable stack trace logging which is very expensive.



## ChunkedStream

Chunked stream is a buffer you can write to that flushes after it reaches a certain size, or a specified amount of time has elapsed.

```
public CaptureStream(
    int bufferSize = kDefaultBufferSize,
    float maxElapsedSeconds = kDefaultMaxSecondsElapsed,
    Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null)
```
Constructs a new memory buffer to capture data as a stream. Data can be appended to the buffer, and when it is full, or maxElapsedSeconds has passed, the data will be dispatched to the provided functor. This class is primarily used as a base class for other logging classes, but can be useful in other situations.

`bufferSize` The size of the memory buffer.
`maxElapsedSeconds` The amount of time to wait before dispatching the buffer.
`functor` Callback handler to process the data.

```
public void Dispose()
```
Disposes of the buffer and all of its resources. This also removes the buffer from being ticked.

```
public void Flush()
```
Flushes the buffer, dispatching the data to the functor.

```
public void Append(byte[] data)
```
Appends data to the buffer.
`data` Array of bytes to append to the buffer.


## Configuration

```
public T GetAppParams<T>()
```

Get app params for the simulation. App Params are loaded from the json file provided as a part of the command line argument. Please refer the Command Line Options section below to see how can you specify the app params for your app. The app params will be loaded at RuntimeInitializeOnLoad and will be available at the start of your application.

## CommandLine Options
--simulation-config-file <Path to the simulation config file>

Usage:
<build>.x86_64 --simulation-config-file=<simulation-config-path>.json

Simulation Config:

{
    "app_param_uri" : "/tmp/configs/my_app_params.json",
}

`app_param_uri` Path to the app param json file on the local file system

## App Params
This will consist of all the parameters that your unity application understands. The fields in the app param file need to match the field names in serializable struct defined in the script.

Sample AppParam.json

```
{
    "extraPlayers": 4,
    "scale": 8,
    "numPickups": 25,
    "extraCameras": 4,
    "forceCrashAfterSeconds": -1,
    "quitAfterSeconds": 120
}
```