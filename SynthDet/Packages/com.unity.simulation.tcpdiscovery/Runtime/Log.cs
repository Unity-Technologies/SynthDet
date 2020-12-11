using System;
using System.Text;

#if (UNITY_EDITOR || UNITY_STANDALONE)
using UnityEngine;
#endif

namespace Unity.Simulation.DistributedRendering
{
    /// <summary>
    /// Logging class for cluster logging.
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
            Verbose,
            All
        }

        /// <summary>
        /// Get/Set the logging level.
        /// </summary>
        public static Level level { get; set; } = Level.Error;
        public static bool logToConsole = false;
        
        /// <summary>
        /// Write the log message to the player log file and/or console.
        /// </summary>
        /// <param name="level">Log Level</param>
        /// <param name="message">Log message</param>
        public static void Write(Level level, string message)
        {
            if (Debug.unityLogger.logEnabled && level <= Log.level)
            {
#if (UNITY_EDITOR || UNITY_STANDALONE)
                {
                    switch (level)
                    {
                        case Level.Warning:
                            Debug.LogWarning(message);
                            break;
                        case Level.Error:
                            Debug.LogError(message);
                            break;
                        default:
                            Debug.Log(message);
                            break;
                    }
                }
#else
                if (logToConsole)
                {
                    switch (level)
                    {
                        case Level.Warning:
                            Console.Error.WriteLine(message);
                            break;
                        case Level.Error:
                            Console.Error.WriteLine(message);
                            break;
                        default:
                            Console.WriteLine(message);
                            break;
                    }
                }
#endif
            }
        }

        /// <summary>
        /// Log info level message to the file.
        /// </summary>
        /// <param name="message">Info log message.</param>
        public static void I(string message)
        {
            Write(Level.Info, message);
        }

        /// <summary>
        /// Log Warning level message to the file.
        /// </summary>
        /// <param name="message">Info warning message.</param>
        public static void W(string message)
        {
            Write(Level.Warning, message);
        }

        /// <summary>
        /// Log Error level message to the file.
        /// </summary>
        /// <param name="message">Error log message.</param>
        public static void E(string message)
        {
            Write(Level.Error, message);
        }

        /// <summary>
        /// Log Verbose level message to the file.
        /// </summary>
        /// <param name="message">Verbose log message.</param>
        public static void V(string message)
        {
            Write(Level.Verbose, message);
        }
    }
}
