using UnityEngine.Profiling;

namespace Unity.Simulation
{
    /// <summary>
    /// Utility class to enable profiling for the application.
    /// </summary>
    public static class ProfilerManager
    {
        /// <summary>
        /// Enables the profiler (writing to the profiler log) for the profiling areas you are interested in.
        /// </summary>
        /// <param name="areas">Array of ProfilerArea enumeration values.</param>
        public static void EnableProfiling(ProfilerArea[] areas)
        {
            if (Profiler.supported)
            {
                Profiler.logFile = Manager.Instance.ProfilerPath;
                Profiler.enableBinaryLog = true;
                if (areas != null)
                {
                    foreach (var a in areas)
                        Profiler.SetAreaEnabled(a, true);
                }
                Profiler.enabled = true;
                Manager.Instance.ProfilerEnabled = true;
            }
            else
            {
                Log.W("Enabling profiling is not supported.");
            }
        }
    }
}

