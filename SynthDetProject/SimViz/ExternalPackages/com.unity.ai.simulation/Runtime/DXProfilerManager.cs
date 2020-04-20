using UnityEngine.Profiling;

namespace Unity.AI.Simulation
{
    public static class DXProfilerManager
    {
        public static void EnableProfiling(ProfilerArea[] areas)
        {
            if (Profiler.supported)
            {
                Profiler.logFile = DXManager.Instance.ProfilerPath;
                Profiler.enableBinaryLog = true;
                if (areas != null)
                {
                    foreach (var a in areas)
                        Profiler.SetAreaEnabled(a, true);
                }
                Profiler.enabled = true;
                DXManager.Instance.ProfilerEnabled = true;
            }
            else
            {
                Log.W("Enabling profiling is not supported.");
            }
        }
    }
}

