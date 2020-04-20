using System;
using System.Text;

using UnityEngine;

using Unity.AI.Simulation;

namespace Unity.AI.ESimSDK
{       
    public static class CaptureUnityLog
    {
        public const int kDefaultBufferSize = CaptureStream.kDefaultBufferSize;
        public const float kDefaultMaxSecondsElapsed = CaptureStream.kDefaultMaxSecondsElapsed;

        static CaptureStream _captureBuffer;
        
        static bool _capturingLog = false;

        public static void Capture(
            int   bufferSize        = kDefaultBufferSize, 
            float maxElapsedSeconds = kDefaultMaxSecondsElapsed,
            Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null
        )
        {
            if (_capturingLog == false)
            {
                _capturingLog = true;
                _captureBuffer = new CaptureStream(bufferSize, maxElapsedSeconds);
                _captureBuffer.functor = functor;
                Application.logMessageReceivedThreaded += HandleLog;
            }
        }

        public static void CaptureToFile(string path, bool addSequenceNumber = true, int bufferSize = kDefaultBufferSize, float maxElapsedSeconds = kDefaultMaxSecondsElapsed)
        {
            if (_capturingLog == false)
            {
                _capturingLog = true;
                _captureBuffer = new CaptureStream(bufferSize, maxElapsedSeconds, functor:(AsyncRequest<object> request) =>
                {
                    DXFile.Write(path, request.data as Array);
                    return AsyncRequest.Result.Completed;
                });
                
                Application.logMessageReceivedThreaded += HandleLog;
            }
        }

        public static void EndCapture()
        {
            if (_capturingLog == true)
            {
                Application.logMessageReceivedThreaded -= HandleLog;
                _captureBuffer.Dispose();
                _capturingLog = false;
            }
        }

        public static void SetLogStackTracing(StackTraceLogType logType)
        {
            var logTypes = new LogType[]{LogType.Assert, LogType.Error, LogType.Exception, LogType.Log, LogType.Warning};
            foreach (var type in logTypes)
                Application.SetStackTraceLogType(type, logType);
        }

#pragma warning disable 0414
        [Serializable]
        struct Entry
        {
            public LogType type;
            public string  trace;
            public string  log;
            public Entry(LogType t, string bt, string l)
            {
                type = t;
                trace = bt;
                log = l;
            }
        }
#pragma warning restore 0414
        
        public static void HandleLog(string logString, string stackTrace, LogType type)
        {
            _captureBuffer.Append(Encoding.ASCII.GetBytes(JsonUtility.ToJson(new Entry(type, stackTrace, logString)) + "\n"));
        }
    }
}
