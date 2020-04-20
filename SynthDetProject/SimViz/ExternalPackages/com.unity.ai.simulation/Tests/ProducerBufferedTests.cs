using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Linq;
using UnityEngine;

using Unity.AI.Simulation;

using NUnit.Framework;
using UnityEngine.TestTools;

public class ChunkedStreamTests
{
    [UnityTest]
    [Timeout(10000)]
    public IEnumerator ProducerBuffer_AppendsBytesToBuffer_FlushesToFileSystem()
    {
        string path = Path.Combine(Configuration.Instance.GetStoragePath(), "log_0.txt");
        DXChunkedStream producer = new DXChunkedStream(8, 1, functor:(AsyncRequest<object> request) =>
        {
            DXFile.Write(path, request.data as Array);
            return AsyncRequest.Result.Completed;
        });
        producer.Append(Encoding.ASCII.GetBytes("Test"));
        producer.Append(Encoding.ASCII.GetBytes("Unit"));
        while (!System.IO.File.Exists(path))
            yield return null;
        Assert.True(System.IO.File.ReadAllText(path) == "TestUnit");
    }
    
    public IEnumerator DXChunkedLogger_ShouldFlushToFileSystem_AtSpecifiedChunkSize()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        string basePath = Path.Combine(Configuration.Instance.GetStoragePath(), "Tests");
        string logFilepath = Path.Combine(basePath, "log.txt");

        if (Directory.Exists(basePath))
        {
            Directory.Delete(basePath, true);
        }
        Directory.CreateDirectory(basePath);

        DXChunkedUnityLog.CaptureToFile(logFilepath, true, 26);
        Debug.Log("This is a test chunked log file");
        Debug.Log("This is another test chunked log file");
        Debug.Log("Another test chunk of logs");
        yield return new WaitUntil(() => 
        {
            var len = Directory.GetFiles(basePath).Length;
            return len >= 2;
        });
        DXChunkedUnityLog.EndCapture();

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
    public IEnumerator DXChunkedLogger_ShouldFlushToFileSystem_AtSpecifiedChunkTimeout()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        string basePath = Path.Combine(Configuration.Instance.GetStoragePath(), "Tests");
        string logFilepath = Path.Combine(basePath, "log.txt");

        if (Directory.Exists(basePath))
        {
            Directory.Delete(basePath, true);
        }
        Directory.CreateDirectory(basePath);

        DXChunkedUnityLog.CaptureToFile(logFilepath, true, 512, 2);
        var testLog = "This is a test log";
        Debug.Log(testLog);
        yield return new WaitUntil(() => Directory.GetFiles(basePath).Length == 1);
        DXChunkedUnityLog.EndCapture();

        var fileInfo = new FileInfo(Directory.GetFiles(basePath)[0]);
        
        Assert.IsTrue(fileInfo.Length <= testLog.Length + 2 && fileInfo.Length > testLog.Length);
        
        Directory.Delete(basePath, true);
    }

    struct TestLog
    {
        public string msg;
    }
    [UnityTest]
    [Timeout(10000)]
    public IEnumerator ProducerBuffer_TrimsEmptySpaces_IfPresentBeforeFlush()
    {
        string path = Path.Combine(Configuration.Instance.GetStoragePath(), "Logs", "log_0.txt");
        var inputLog = new TestLog() {msg = "Test"};
        var logger = new Unity.AI.Simulation.Logger("log.txt", 20);
        logger.Log(new TestLog() { msg = "Test"});
        logger.Log(new TestLog() { msg = "UnityTest"});
        while (!System.IO.File.Exists(path))
            yield return null;
        var fileInfo = new FileInfo(path);
        Assert.AreEqual(JsonUtility.ToJson(inputLog).Length + 1, fileInfo.Length);
    }
}
