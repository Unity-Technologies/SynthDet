# AsyncRequest

## Summary: 

AsyncRequest<T> is a templated class similar to Task<T>. The main difference is that AsyncRequest<T> is allocated from an object pool, and recycled on disposal, to keep memory allocation to a minimum. Task<T> also has the limitation (in Unity) of always executing on the main thread. AsyncRequest<T> can run either on the main thread, or on the system thread pool.

All the capture methods immediately return this object, which you can use to determine if a request has completed or not. Once completed, the passed in functors (if any) are called with the AsyncRequest<T> associated with the request. This generally happens on a different thread, so you must make sure not to invoke any Unity API methods (unless explicitly documented as thread safe) from the completion functor. When returning from the completion functor, you must either return AsyncRequest.Result.Completed or AsyncRequest.Result.Error.

### AsyncRequest<T>

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

### Summary:
If an error occurs, then this property will be set to true. This will also set the completed property to true.

```
 public bool completed
```

### Summary: 
If the capture request has completed, this property will be set to true.

```
public ref T data
```

### Summary: 
A reference to the payload data for the request.