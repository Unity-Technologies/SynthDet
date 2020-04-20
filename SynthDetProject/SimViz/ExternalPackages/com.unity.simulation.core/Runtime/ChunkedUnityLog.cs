using System;
using System.Text;
using System.IO;

using UnityEngine;

namespace Unity.Simulation
{
    /// <summary>
    /// Unity log handler that dispatches in chunks.
    /// </summary>
    [Obsolete("Obsolete msg -> ChunkedUnityLog (UnityUpgradable)", true)]
    public static class DXChunkedUnityLog {}

    /// <summary>
    /// Unity log handler that dispatches in chunks.
    /// </summary>
    public static class ChunkedUnityLog
    {
        /// <summary>
        /// Default buffer size in bytes.
        /// </summary>
        public const int kDefaultBufferSize = ChunkedStream.kDefaultBufferSize;

        /// <summary>
        /// Max log line length in bytes.
        /// </summary>
        public const int kMaxLogLineLength = 1024;

        /// <summary>
        /// Time after which log lines will be flushed.
        /// </summary>
        public const float kDefaultMaxSecondsElapsed = ChunkedStream.kDefaultMaxSecondsElapsed;

        static ChunkedStream _captureBuffer;
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

        /// <summary>
        /// Enable capture log with bufferSize and maxTimeElapsed.
        /// </summary>
        /// <param name="bufferSize">Maximum buffer size to hold in memory before the data is flushed down to the file system.</param>
        /// <param name="maxElapsedSeconds">Maximum time to hold the data in the buffer before it is flushed down to the file system.</param>
        /// <param name="functor">Callback to be invoked to consume the log produced.</param>
        public static void Capture(
            int   bufferSize        = kDefaultBufferSize, 
            float maxElapsedSeconds = kDefaultMaxSecondsElapsed,
            Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null
        )
        {
            if (_capturingLog == false)
            {
                _capturingLog = true;
                _captureBuffer = new ChunkedStream(bufferSize, maxElapsedSeconds);
                _captureBuffer.functor = functor;
                ReplayExistingLog();
                Application.logMessageReceivedThreaded += HandleLog;
            }
        }

        /// <summary>
        /// Capture chunked logging to the file at the given path 
        /// </summary>
        /// <param name="path">Path to the chunked log file</param>
        /// <param name="addSequenceNumber">boolean indicating if sequence number needs to be appended at the end</param>
        /// <param name="bufferSize">Maximum buffer size to hold in memory before the data is flushed down to the file system</param>
        /// <param name="maxElapsedSeconds">Maximum time to hold the data in the buffer before it is flushed down to the file system.</param>
        public static void CaptureToFile(string path, bool addSequenceNumber = true, int bufferSize = kDefaultBufferSize, float maxElapsedSeconds = kDefaultMaxSecondsElapsed)
        {
            if (_capturingLog == false)
            {
                _capturingLog = true;
                _logPath = new SequencedPathName(path, addSequenceNumber);
                _captureBuffer = new ChunkedStream(bufferSize, maxElapsedSeconds, functor:(AsyncRequest<object> request) =>
                {
                    FileProducer.Write(_logPath.GetPath(), request.data as Array);
                    return AsyncRequest.Result.Completed;
                });
                
                ReplayExistingLog();
                Application.logMessageReceivedThreaded += HandleLog;
            }
        }

        /// <summary>
        /// End capturing of the chunked logs.
        /// </summary>
        public static void EndCapture()
        {
            if (_capturingLog == true)
            {
                Application.logMessageReceivedThreaded -= HandleLog;
                _captureBuffer.Dispose();
                _capturingLog = false;
            }
        }

        /// <summary>
        /// Set the log stack trace type. 
        /// </summary>
        /// <param name="logType">Stack trace logging option.</param>
        public static void SetLogStackTracing(StackTraceLogType logType)
        {
            var logTypes = new LogType[]{LogType.Assert, LogType.Error, LogType.Exception, LogType.Log, LogType.Warning};
            foreach (var type in logTypes)
                Application.SetStackTraceLogType(type, logType);
        }

        private static void HandleLog(string logString, string stackTrace, LogType type)
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
