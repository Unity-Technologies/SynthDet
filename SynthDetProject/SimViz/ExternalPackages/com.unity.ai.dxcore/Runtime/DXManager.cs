using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.AI.Simulation
{
    using ConcurrentTypePool = ConcurrentDictionary<Type, ConcurrentBag<object>>;

    public struct DataCapturePaths
    {
        public readonly static string Logs          = "Logs";
        public readonly static string ScreenCapture = "ScreenCapture";
        public readonly static string Chunks        = "Chunks";
    };

    public sealed class DXManager
    {
        public const string kVersionString       = "v1.0.9";
        public const string kProfilerLogFileName = "profilerLog.txt";
        public const string kPlayerLogFileName   = "Player.Log";
        public const string kHeartbeatFileName   = "heartbeat.txt";

        string[] _uploadsBlackList = new string[]
        {
            kProfilerLogFileName,
            kHeartbeatFileName
        };

        private static readonly DXManager _instance = new DXManager();

        private static int kMaxTimeBeforeShutdown = 120;

        private float _shutdownTimer = 0;

        public bool ProfilerEnabled { get; set; }

        public string ProfilerPath
        {
            get
            {
                return Path.Combine(GetDirectoryFor(DataCapturePaths.Logs), kProfilerLogFileName);
            }
        }

        ConcurrentDictionary<Type, ConcurrentBag<AsyncRequest>> _requestPool = new ConcurrentDictionary<Type, ConcurrentBag<AsyncRequest>>();

        List<IDataProduced> _dataConsumers = new List<IDataProduced>();

        public void RegisterDataConsumer(IDataProduced consumer)
        {
            if (!_dataConsumers.Contains(consumer))
            {
                if (consumer != null && consumer.Initialize())
                {
                    Log.V($"Registered consumer {consumer.GetType().Name}.");
                    _dataConsumers.Add(consumer);
                }
                else
                {
                    Log.E($"Failed to register consumer {consumer.GetType().Name}. Initialize failed.");
                }
            }
        }

        public void UnregisterDataConsumer(IDataProduced consumer)
        {
            if (_dataConsumers.Contains(consumer))
                _dataConsumers.Remove(consumer);
        }

        public int requestPoolCount
        {
            get
            {
                var count = 0;
                foreach (var kv in _requestPool)
                    count += kv.Value.Count;
                return count;
            }
        }

        public delegate void TickDelegate(float dt);
        public TickDelegate Tick;

        public delegate void NotificationDelegate();
        public NotificationDelegate StartNotification;
        public NotificationDelegate ShutdownNotification;

        static Forward _forward;

        static bool _shutdownRequested = false;
        static bool _finalUploadsDone = false;

        public bool FinalUploadsDone { get { return _finalUploadsDone; } }

        double _simulationElapsedTime = 0;
        public double SimulationElapsedTime
        {
            get { return _simulationElapsedTime; }
        }

        double _simulationElapsedTimeUnscaled = 0;
        public double SimulationElapsedTimeUnscaled
        {
            get { return _simulationElapsedTimeUnscaled; }
        }

        Stopwatch _wallElapsedTime = new Stopwatch();
        public double WallElapsedTime
        {
            get { return _wallElapsedTime.Elapsed.TotalSeconds; }
        }

        bool ConsumptionStillInProgress()
        {
            foreach (var consumer in _dataConsumers)
                if (consumer.ConsumptionStillInProgress())
                    return true;
            return false;
        }

        private bool readyToQuit
        {
            get
            {
                _shutdownTimer += Time.deltaTime;
                _shutdownRequested = true;
                return (_finalUploadsDone && !ConsumptionStillInProgress()) || _shutdownTimer >= kMaxTimeBeforeShutdown;
            }
        }

        static DXManager()
        {}

        DXManager()
        {
            _simulationElapsedTime = 0;
            _simulationElapsedTimeUnscaled = 0;
            _wallElapsedTime.Restart();

            Log.level = Log.Level.All;
            _shutdownRequested = false;
            Application.wantsToQuit += () =>
            {
                return readyToQuit;
            };

#if (PLATFORM_STANDALONE_OSX || PLATFORM_STANDALONE_LINUX) && !ENABLE_IL2CPP
            InstallSigTermHandler();
            InstallSigAbortHandler();
#endif

            Directory.CreateDirectory(Configuration.Instance.GetStoragePath());
        }

        public static DXManager Instance
        {
            get
            {
                return _instance;
            }
        }

        class Forward : MonoBehaviour
        {
            public DXManager client { get; set; }
            void Awake()
            {
                gameObject.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(gameObject);
            }
            void Start()
            {
                client.Start();
            }
            void Update()
            {
                client.Update(Time.deltaTime);
            }

            public static IEnumerator QueueEndOfFrameItem(Action<object> callback, object functor)
            {
                yield return new WaitForEndOfFrame();
                callback(functor);
            }
        }

        [RuntimeInitializeOnLoadMethod]
        static void OnRuntimeMethodLoad()
        {
            var go = new GameObject("DXManager");
            _forward = go.AddComponent<Forward>();
            _forward.client = DXManager.Instance;
        }

        public void QueueEndOfFrameItem(Action<object> callback, object functor)
        {
            _forward.StartCoroutine(Forward.QueueEndOfFrameItem(callback, functor));
        }

        public void ConsumerFileProduced(string filePath, bool synchronous = false)
        {
            if (_finalUploadsDone)
            {
                Log.I("Shutdown in progress, ignoring file produced " + filePath);
                return;
            }

            foreach (var consumer in _dataConsumers)
                consumer.Consume(filePath, synchronous || _shutdownRequested);
        }

        public void Start()
        {
            Log.I($"Simulation SDK {kVersionString} Started.");

            StartNotification?.Invoke();

            if (DXOptions.uploadFilesFromPreviousRun && Configuration.Instance.IsSimulationRunningInCloud())
            {
                UploadTracesFromPreviousRun(Configuration.Instance.GetStorageBasePath());
            }

            var chunk_size_bytes = Configuration.Instance.SimulationConfig.chunk_size_bytes;
            var chunk_timeout_ms = Configuration.Instance.SimulationConfig.chunk_timeout_ms;
            if (chunk_size_bytes > 0 && chunk_timeout_ms > 0)
            {
                DXChunkedUnityLog.CaptureToFile
                (
                    Path.Combine(GetDirectoryFor(DataCapturePaths.Chunks), kPlayerLogFileName),
                    true,
                    chunk_size_bytes,
                    (float)chunk_timeout_ms / 1000.0f
                );
            }
        }

        public void Shutdown()
        {
            _shutdownRequested = true;
        }

        public void Update(float dt)
        {
            _simulationElapsedTime += dt;
            _simulationElapsedTimeUnscaled += Time.unscaledDeltaTime;

            Tick?.Invoke(dt);

            if (_shutdownRequested)
            {
                if (!_finalUploadsDone)
                {
                    ShutdownNotification?.Invoke();

                    if (ProfilerEnabled)
                    {
                        Log.V("Disabling the Profiler to flush it down to the file system");
                        Profiler.enabled = false;
                        Profiler.enableBinaryLog = false;
                    }

                    try
                    {
                        var profilerLog = Path.Combine(GetDirectoryFor(DataCapturePaths.Logs), kProfilerLogFileName + ".raw");
                        if (File.Exists(profilerLog))
                        {
                            Log.V("Profiler file length: " + new FileInfo(profilerLog).Length);
                            ConsumerFileProduced(profilerLog, true);
                        }

                        if (!string.IsNullOrEmpty(Application.consoleLogPath))
                        {
                            var internalLogPath = Path.Combine(GetDirectoryFor(DataCapturePaths.Logs), kPlayerLogFileName);

                            if (File.Exists(internalLogPath))
                                Log.V($"Player.Log is already present at {internalLogPath} was this execution accidentally run twice?");
                            else
                                File.Copy(Application.consoleLogPath, internalLogPath);

                            Log.V("DXManager upload player log: " + Application.consoleLogPath);
                            ConsumerFileProduced(internalLogPath, true);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.V($"Exception ocurred uploading profiler/player logs: {e.ToString()}");
                    }

                    // Note: It's super important that this flag is set after the two calls to ConsumerFileProduced.
                    // If set before, they will be rejected, and if not set after, then the next frame, they will be queued again.
                    // This has the effect of preventing a shutdown indefinitely. Anything that interrupts the control flow like
                    // await can cause the setting of this flag to be deferred, allowing the next tick to queue another file.
                    _finalUploadsDone = true;
                    Log.V("Final uploads completed.");
                }

                if (readyToQuit)
                {
                    Log.V("Application quit");
                    Application.Quit();
                }
            }
        }

        bool IsFileBlacklisted(string file)
        {
            return Array.Find(_uploadsBlackList, item => item.Equals(Path.GetFileName(file))) != null;
        }

        public void UploadTracesFromPreviousRun(string path)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    if (!IsFileBlacklisted(f))
                        ConsumerFileProduced(f);
                }
            }
        }

        public T CreateRequest<T>() where T : AsyncRequest
        {
            ConcurrentBag<AsyncRequest> bag;
            _requestPool.TryGetValue(typeof(T), out bag);

            if (bag == null)
            {
                bag = new ConcurrentBag<AsyncRequest>();
                _requestPool[typeof(T)] = bag;
            }

            AsyncRequest request;
            if (bag.TryTake(out request))
            {
                request.Reset();
                return request as T;
            }

            return (T)Activator.CreateInstance(typeof(T));
        }

        public void RecycleRequest<T>(T request) where T : AsyncRequest
        {
            _requestPool[typeof(T)].Add(request);
        }

        public string GetDirectoryFor(string type = "", string userPath = "")
        {
            string basePath;

            if (string.IsNullOrEmpty(userPath))
            {
                basePath = Path.Combine(Configuration.Instance.GetStoragePath(), type);
            }
            else
            {
                if (Path.HasExtension(userPath))
                    basePath = Path.Combine(Path.GetDirectoryName(userPath), Configuration.Instance.GetAttemptId());
                else
                    basePath = Path.Combine(userPath, Configuration.Instance.GetAttemptId(), type.ToString().ToLower());
            }

            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            return basePath;
        }

#if (PLATFORM_STANDALONE_OSX || PLATFORM_STANDALONE_LINUX) && !ENABLE_IL2CPP
        public delegate void SignalHandlerDelegate();

        [DllImport ("__Internal")]
        static extern IntPtr signal(int signum, SignalHandlerDelegate handler);

        [DllImport ("__Internal")]
        static extern IntPtr signal(int signum, int foo);

        [DllImport ("__Internal")]
        static extern void abort();

        static readonly string _consoleLogPath = Application.consoleLogPath;

        SignalHandlerDelegate InstallSigTermHandler()
        {
            IntPtr previous = signal(15/*SIGTERM*/, () =>
            {
                _shutdownRequested = true;
            });
            return previous != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<SignalHandlerDelegate>(previous) : null;
        }

        static bool _abortUploadLog = false;
        SignalHandlerDelegate InstallSigAbortHandler()
        {
            if (!Configuration.Instance.IsSimulationRunningInCloud())
                return null;
            IntPtr previous = signal(6/*SIGABRT*/, () =>
            {
                if (!_abortUploadLog)
                {
                    _abortUploadLog = true;

                    if (!string.IsNullOrEmpty(_consoleLogPath))
                    {
                        foreach (var consumer in _dataConsumers)
                        {
                            if (File.Exists(_consoleLogPath))
                            {
                                var internalLogPath = Path.Combine(GetDirectoryFor(DataCapturePaths.Logs),
                                    kPlayerLogFileName);
                                File.Copy(_consoleLogPath, internalLogPath);
                                consumer.Consume(internalLogPath, true);
                            }

                            if (ProfilerEnabled)
                            {
                                Profiler.enabled = false;
                                Profiler.enableBinaryLog = false;
                                ProfilerEnabled = false;
                            }

                            var profilerLog = Path.Combine(GetDirectoryFor(DataCapturePaths.Logs), kProfilerLogFileName + ".raw");
                            if (File.Exists(profilerLog))
                                consumer.Consume(profilerLog, true);
                        }
                    }
                }
                else
                {
                    signal(6/*SIGABRT*/, 0);
                    abort();
                }
            });
            return previous != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<SignalHandlerDelegate>(previous) : null;
        }
#endif//PLATFORM_STANDALONE_OSX || PLATFORM_STANDALONE_LINUX && !ENABLE_IL2CPP
    }
}
