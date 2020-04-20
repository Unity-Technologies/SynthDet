using UnityEngine;

using Unity.Simulation;

using NUnit.Framework;
using NUnit.Framework.Internal;

public class ConfigurationTests
{
    [Test]
    public void BucketName_And_StoragePath_Returns_AsExpected()
    {
        var expectedBucketName = "test-bucket";
        var expectedStoragePath = "folder1/folder2";

        Configuration.Instance.SimulationConfig = new Configuration.SimulationConfiguration()
        {
            storage_uri_prefix = "gs://test-bucket/folder1/folder2"
        };

        Debug.Log("Storage Prefix: " + Configuration.Instance.SimulationConfig.storage_uri_prefix);

        string actualBucketName = Configuration.Instance.SimulationConfig.bucketName;
        string actualStoragePath = Configuration.Instance.SimulationConfig.storagePath;

        Debug.Assert(actualBucketName == expectedBucketName, $"Bucket name returned: {actualBucketName} is not as expected");
        Debug.Assert(actualStoragePath == expectedStoragePath, $"Storage Path returned: {actualStoragePath} is not as expected");
    }
}
