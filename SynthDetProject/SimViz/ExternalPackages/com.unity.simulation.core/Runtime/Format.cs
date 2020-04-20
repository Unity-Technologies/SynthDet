using System;
using System.Text;

using UnityEngine;

namespace Unity.Simulation
{
    /// <summary>
    /// Format utility class for floats that truncates string representation to a certain precision for logging.
    /// </summary>
    [Obsolete("Obsolete msg -> Format (UnityUpgradable)", true)]
    public static class DXFormat {}

    /// <summary>
    /// Format utility class for floats that truncates string representation to a certain precision for logging.
    /// </summary>
    public static class Format
    {
        /// <summary>
        /// Formats the floats input as per given precision and stride.
        /// </summary>
        /// <param name="floats">Input float array</param>
        /// <param name="stride">Number of floating points in a single stride.</param>
        /// <param name="precision">Precision for floating point numbers.</param>
        /// <returns>Formatted string of floating point numbers.</returns>
        public static string Floats(float[] floats, int stride = 0, int precision = 3)
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