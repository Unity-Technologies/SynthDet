using System;
using System.Collections;
using System.IO;
using UnityEngine;

using Unity.Simulation;

using NUnit.Framework;
using UnityEngine.TestTools;

using Logger = Unity.Simulation.Logger;

public class DataLoggerTests
{
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
        var logger = new Logger("log.txt", 20);
        logger.Log(new TestLog() { msg = "Test"});
        logger.Log(new TestLog() { msg = "UnityTest"});
        while (!System.IO.File.Exists(path))
            yield return null;
        var fileInfo = new FileInfo(path);
        Assert.AreEqual(JsonUtility.ToJson(inputLog).Length + 1, fileInfo.Length);
    }
}