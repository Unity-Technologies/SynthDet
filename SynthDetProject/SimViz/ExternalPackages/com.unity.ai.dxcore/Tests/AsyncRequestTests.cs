using System.Collections;

using UnityEngine;

using Unity.AI.Simulation;

using UnityEngine.TestTools;

public class AsyncRequestTests
{
    [UnityTest]
    public IEnumerator AsyncRequest_AllocatesAndReturnsToPool()
    {
        using (var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>())
        {
            req.Start( (AsyncRequest<object> r) =>
            {
                return AsyncRequest<object>.Result.Completed;
            });

            while (!req.completed)
                yield return null;
        }

        Debug.Assert(DXManager.Instance.requestPoolCount == 1, "requestPoolCount == 1");

        using (var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>())
        {
            Debug.Assert(DXManager.Instance.requestPoolCount == 0, "requestPoolCount == 0");

            req.Start( (AsyncRequest<object> r) =>
            {
                return AsyncRequest<object>.Result.Completed;
            });

            while (!req.completed)
                yield return null;
        }

        Debug.Assert(DXManager.Instance.requestPoolCount == 1, "requestPoolCount == 1");
    }

    [UnityTest]
    public IEnumerator AsyncRequest_StartingRequestNTimesProducesNResults()
    {        
        using (var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>())
        {
            var N = UnityEngine.Random.Range(10, 1000);

            for (int i = 0; i < N; ++i)
            {
                req.Start( (AsyncRequest<object> r) =>
                {
                    return AsyncRequest<object>.Result.Completed;
                });
            }

            while (!req.completed)
                yield return null;

            Debug.Assert(req.results.Length == N);
        }
    }
}
