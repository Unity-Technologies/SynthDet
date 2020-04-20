using System;
using System.Collections;
using System.Text;
using System.IO;

using Unity.Simulation;

using NUnit.Framework;
using UnityEngine.TestTools;

public class ChunkedStreamTests
{
    [UnityTest]
    [Timeout(10000)]
    public IEnumerator ChunkedStream_AppendsBytesToBuffer_FlushesToFileSystem()
    {
        string path = Path.Combine(Configuration.Instance.GetStoragePath(), "log_0.txt");
        ChunkedStream producer = new ChunkedStream(8, 1, functor:(AsyncRequest<object> request) =>
        {
            FileProducer.Write(path, request.data as Array);
            return AsyncRequest.Result.Completed;
        });
        producer.Append(Encoding.ASCII.GetBytes("Test"));
        producer.Append(Encoding.ASCII.GetBytes("Unit"));
        while (!System.IO.File.Exists(path))
            yield return null;
        Assert.True(System.IO.File.ReadAllText(path) == "TestUnit");
    }
}
