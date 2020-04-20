using System;
using System.IO;

using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.Simulation
{
    /// <summary>
    /// File write utility class that reports data written to the manager for uploading.
    /// </summary>
    [Obsolete("Obsolete msg -> FileProducer (UnityUpgradable)", true)]
    public static class DXFile {}

    /// <summary>
    /// File write utility class that reports data written to the manager for uploading.
    /// </summary>
    public static class FileProducer
    {
        const int kFileDataBufferSize = 4096;

        /// <summary>
        /// Write the data to the file system and inform the consumers for uploading it to the cloud.
        /// </summary>
        /// <param name="path">Full path to the file.</param>
        /// <param name="data">An array of data</param>
        /// <param name="uploadSynchronously">boolean indicating if the upload needs to happen synchronously.</param>
        /// <returns>boolean indicating if the write was successful</returns>
        public static bool Write(string path, Array data, bool uploadSynchronously = false)
        {
            Debug.Assert(!string.IsNullOrEmpty(path), "Write path cannot be empty or null.");
            Debug.Assert(data != null, "Array data cannot be null.");

            if (Options.debugDontWriteFiles)
            {
                Manager.Instance.ConsumerFileProduced(path, uploadSynchronously);
                return true;
            }

            FileStream file = null;

            try
            {
                var bytes = ArrayUtilities.Cast<byte>(data);

                file = File.Create(path, kFileDataBufferSize);
                file.Write(bytes, 0, bytes.Length);
                file.Close();
                file = null;
                Manager.Instance.ConsumerFileProduced(path);
                return true;
            }
            catch (Exception e)
            {
                Log.E("FileProducer.Write exception : " + e.ToString());
                return false;
            }
            finally
            {
                if (file != null)
                    file.Close();
            }
        }
    }
}
