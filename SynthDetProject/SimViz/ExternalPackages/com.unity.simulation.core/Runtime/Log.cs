using System;
using System.Text;
using UnityEngine;

namespace Unity.Simulation
{
    /// <summary>
    /// Logging class that the SDK uses for logging.
    /// Essentially a wrapper around Debug.Log/Console.WriteLine.
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// An enum describing different logging levels.
        /// </summary>
        public enum Level
        {
            None,
            Info,
            Warning,
            Error,
            Fatal,
            Verbose,
            All
        }

        const int kDefaulLogLineCapacity = 4096;

        static StringBuilder _stringBuilder = new StringBuilder(kDefaulLogLineCapacity);

        /// <summary>
        /// Get/Set the logging level.
        /// </summary>
        public static Level level { get; set; } = Level.All;
        
        /// <summary>
        /// Write the log message to the player log file and/or console.
        /// </summary>
        /// <param name="level">Log Level</param>
        /// <param name="message">Log message</param>
        /// <param name="logToConsole">boolean indicating if the log message is to be displayed on the editor console.</param>
        public static void Write(Level level, string message, bool logToConsole)
        {
            if (Debug.unityLogger.logEnabled && level <= Log.level)
            {
                lock (_stringBuilder)
                {
                    _stringBuilder.Clear();
                    _stringBuilder.Append("DC[");
                    _stringBuilder.Append(level.ToString()[0]);
                    _stringBuilder.Append("]: ");
                    _stringBuilder.Append(message);
                    var line = _stringBuilder.ToString();

#if !UNITY_EDITOR
                    if (logToConsole)
                    {
                        switch (level)
                        {
                            case Level.Error:
                                Console.Error.WriteLine(line);
                                break;
                            default:
                                Console.WriteLine(line);
                                break;
                        }
                    }
                    else
#endif
                    {
                        switch (level)
                        {
                            case Level.Warning:
                                Debug.LogWarning(line);
                                break;
                            case Level.Error:
                                Debug.LogError(line);
                                break;
                            default:
                                Debug.Log(line);
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Log info level message to the file.
        /// </summary>
        /// <param name="message">Info log message.</param>
        /// <param name="logToConsole">boolean indicating if the log message is to be displayed on the console.</param>
        public static void I(string message, bool logToConsole = false)
        {
            Write(Level.Info, message, logToConsole);
        }

        /// <summary>
        /// Log Warning level message to the file.
        /// </summary>
        /// <param name="message">Info warning message.</param>
        /// <param name="logToConsole">boolean indicating if the log message is to be displayed on the console.</param>
        public static void W(string message, bool logToConsole = false)
        {
            Write(Level.Warning, message, logToConsole);
        }

        /// <summary>
        /// Log Error level message to the file.
        /// </summary>
        /// <param name="message">Error log message.</param>
        /// <param name="logToConsole">boolean indicating if the log message is to be displayed on the console.</param>
        public static void E(string message, bool logToConsole = false)
        {
            Write(Level.Error, message, logToConsole);
        }

        /// <summary>
        /// Log Fatal level message to the file.
        /// </summary>
        /// <param name="message">Fatal log message.</param>
        /// <param name="logToConsole">boolean indicating if the log message is to be displayed on the console.</param>
        public static void F(string message, bool logToConsole = false)
        {
            Write(Level.Fatal, message, logToConsole);
        }

        /// <summary>
        /// Log Verbose level message to the file.
        /// </summary>
        /// <param name="message">Verbose log message.</param>
        /// <param name="logToConsole">boolean indicating if the log message is to be displayed on the console.</param>
        public static void V(string message, bool logToConsole = false)
        {
            Write(Level.Verbose, message, logToConsole);
        }
    }
}
