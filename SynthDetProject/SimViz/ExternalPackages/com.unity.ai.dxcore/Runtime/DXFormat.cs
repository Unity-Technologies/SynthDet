using System.Text;

using UnityEngine;

namespace Unity.AI.Simulation
{
    public static class DXFormat
    {
        public static string FormatFloats(float[] floats, int stride = 0, int precision = 3)
        {
            if (stride == 0)
                stride = floats.Length;

            Debug.Assert(floats.Length % stride == 0);

            var estimatedCapacity = (8 + precision) * floats.Length;
            var sb = new StringBuilder(estimatedCapacity);

            var spec = "N" + precision.ToString();
            var lines = floats.Length / stride;

            for (var i = 0; i < floats.Length; ++i)
            {
                sb.Append(floats[i].ToString(spec));
                if (i < floats.Length - 1)
                    sb.Append(", ");
                if (i % stride == stride - 1)
                    sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}