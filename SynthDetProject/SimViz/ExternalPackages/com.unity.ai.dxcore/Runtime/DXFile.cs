using System;
using System.IO;

using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.AI.Simulation
{
    public static class DXFile
    {
        const int kFileDataBufferSize = 4096;

        public static bool Write(string path, Array data, bool uploadSynchronously = false)
        {
            Debug.Assert(path != null);
            Debug.Assert(data != null);

            if (DXOptions.debugDontWriteFiles)
            {
                DXManager.Instance.ConsumerFileProduced(path, uploadSynchronously);
                return true;
            }

            FileStream file = null;

            try
            {
                var bytes = ArrayUtilities.Cast<byte>(data);

                try
                {
                    file = File.Create(path, kFileDataBufferSize);
                    file.Write(bytes, 0, bytes.Length);
                    file.Close();
                    file = null;
                    DXManager.Instance.ConsumerFileProduced(path);
                    return true;
                }
                catch (Exception e)
                {
                    Log.E("DXFile.Write exception : " + e.ToString());
                    return false;
                }
            }
            finally
            {
                if (file != null)
                    file.Close();
            }
        }
    }
}
