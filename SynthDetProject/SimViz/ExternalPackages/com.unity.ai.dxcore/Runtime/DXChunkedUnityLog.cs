using System;
using System.Text;
using System.IO;

using UnityEngine;

namespace Unity.AI.Simulation
{
    public static class DXChunkedUnityLog
    {
        public const int   kDefaultBufferSize        = DXChunkedStream.kDefaultBufferSize;
        public const int   kMaxLogLineLength         = 1024;
        public const float kDefaultMaxSecondsElapsed = DXChunkedStream.kDefaultMaxSecondsElapsed;

        static DXChunkedStream _captureBuffer;
        static StringBuilder _stringBuilder = new StringBuilder(kMaxLogLineLength);

        static bool _capturingLog = false;
        static SequencedPathName _logPath;

        static void ReplayExistingLog()
        {
            if (!Application.isEditor && !string.IsNullOrEmpty(Application.consoleLogPath) && File.Exists(Application.consoleLogPath))
            {
                var lines = File.ReadAllLines(Application.consoleLogPath);
                foreach (var l in lines)
                    _captureBuffer.Append(Encoding.ASCII.GetBytes(l));
            }
        }

        public static void Capture(
            int   bufferSize        = kDefaultBufferSize, 
            float maxElapsedSeconds = kDefaultMaxSecondsElapsed,
            Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null
        )
        {
            if (_capturingLog == false)
            {
                _capturingLog = true;
                _captureBuffer = new DXChunkedStream(bufferSize, maxElapsedSeconds);
                _captureBuffer.functor = functor;
                ReplayExistingLog();
                Application.logMessageReceivedThreaded += HandleLog;
            }
        }

        public static void CaptureToFile(string path, bool addSequenceNumber = true, int bufferSize = kDefaultBufferSize, float maxElapsedSeconds = kDefaultMaxSecondsElapsed)
        {
            if (_capturingLog == false)
            {
                _capturingLog = true;
                _logPath = new SequencedPathName(path, addSequenceNumber);
                _captureBuffer = new DXChunkedStream(bufferSize, maxElapsedSeconds, functor:(AsyncRequest<object> request) =>
                {
                    DXFile.Write(_logPath.GetPath(), request.data as Array);
                    return AsyncRequest.Result.Completed;
                });
                
                ReplayExistingLog();
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

        public static void HandleLog(string logString, string stackTrace, LogType type)
        {
            string s;
            lock (_stringBuilder)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append(logString);
                _stringBuilder.Append(Environment.NewLine);
                _stringBuilder.Append(stackTrace);
                s = _stringBuilder.ToString();
            }
            _captureBuffer.Append(Encoding.ASCII.GetBytes(s));
        }
    }
}
