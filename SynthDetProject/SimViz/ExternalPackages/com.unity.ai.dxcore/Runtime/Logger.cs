using System;
using System.Text;
using UnityEngine;

namespace Unity.AI.Simulation
{
    public static class Log
    {
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

        public static Level level { get; set; } = Level.All;

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

        public static void I(string message, bool logToConsole = false)
        {
            Write(Level.Info, message, logToConsole);
        }

        public static void W(string message, bool logToConsole = false)
        {
            Write(Level.Warning, message, logToConsole);
        }

        public static void E(string message, bool logToConsole = false)
        {
            Write(Level.Error, message, logToConsole);
        }

        public static void F(string message, bool logToConsole = false)
        {
            Write(Level.Fatal, message, logToConsole);
        }

        public static void V(string message, bool logToConsole = false)
        {
            Write(Level.Verbose, message, logToConsole);
        }
    }
}
