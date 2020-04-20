using System;
using System.Collections;
using System.IO;
using UnityEngine;

using Unity.Simulation;

using NUnit.Framework;
using UnityEngine.TestTools;

public class ChunkedLoggerTests
{
    public IEnumerator ChunkedLogger_ShouldFlushToFileSystem_AtSpecifiedChunkSize()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        string basePath = Path.Combine(Configuration.Instance.GetStoragePath(), "Tests");
        string logFilepath = Path.Combine(basePath, "log.txt");

        if (Directory.Exists(basePath))
        {
            Directory.Delete(basePath, true);
        }
        Directory.CreateDirectory(basePath);

        ChunkedUnityLog.CaptureToFile(logFilepath, true, 26);
        Debug.Log("This is a test chunked log file");
        Debug.Log("This is another test chunked log file");
        Debug.Log("Another test chunk of logs");
        yield return new WaitUntil(() => 
        {
            var len = Directory.GetFiles(basePath).Length;
            return len >= 2;
        });
        ChunkedUnityLog.EndCapture();

        var logFiles = Directory.EnumerateFiles(basePath);
        foreach(var f in logFiles)
        {
            var info = new FileInfo(f);
            Debug.Log("file Length: " + info.Length);
            Assert.True(info.Length >= 26);
        }

        Directory.Delete(basePath, true);
    }

    [UnityTest]
    [Timeout(10000)]
    public IEnumerator ChunkedLogger_ShouldFlushToFileSystem_AtSpecifiedChunkTimeout()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        string basePath = Path.Combine(Configuration.Instance.GetStoragePath(), "Tests");
        string logFilepath = Path.Combine(basePath, "log.txt");

        if (Directory.Exists(basePath))
        {
            Directory.Delete(basePath, true);
        }
        Directory.CreateDirectory(basePath);

        ChunkedUnityLog.CaptureToFile(logFilepath, true, 512, 2);
        var testLog = "This is a test log";
        Debug.Log(testLog);
        yield return new WaitUntil(() => Directory.GetFiles(basePath).Length == 1);
        ChunkedUnityLog.EndCapture();

        var fileInfo = new FileInfo(Directory.GetFiles(basePath)[0]);
        
        Assert.IsTrue(fileInfo.Length <= testLog.Length + 2 && fileInfo.Length > testLog.Length);
        
        Directory.Delete(basePath, true);
    }
}
