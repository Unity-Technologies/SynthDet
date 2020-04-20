using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Simulation.Client;
using UnityEngine;

public class USimRunner
{
    public static Run StartUSimRun(string name, string sysParamId, string buildZipPath, AnimationCurve scaleFactorRange, int steps, int stepsPerJob, int maxFrames)
    {
        float stepSize = 1f / (steps + 1);
        float time = 0f;
        int stepsThisJob = 0;
        int jobIndex = 0;
        List<float> scaleFactors = new List<float>(stepsPerJob); 
        List<AppParam> appParamIds = new List<AppParam>(); 
        for (int i = 0; i < steps; i++)
        {
            var scaleFactor = scaleFactorRange.Evaluate(time);
            scaleFactors.Add(scaleFactor);
            stepsThisJob++;
            if (stepsThisJob == stepsPerJob)
            {   
                stepsThisJob = 0;
                var appParamName = $"{name}_{jobIndex}";
                Debug.Log($"Uploading app param {appParamName}");
                var appParamId = CreateAppParam(appParamName, scaleFactors.ToArray(), maxFrames);
                appParamIds.Add(new AppParam()
                {
                    id = appParamId,
                    name = appParamName,
                    num_instances = 1
                });
                jobIndex++;
                scaleFactors.Clear();
            }
            time += stepSize;
        }

        var buildId = API.UploadBuild(name, buildZipPath);
        
        var runDefinitionId = API.UploadRunDefinition(new RunDefinition
        {
            app_params = appParamIds.ToArray(),
            name = name,
            sys_param_id = sysParamId,
            build_id = buildId
        });

        var run = Run.CreateFromDefinitionId(runDefinitionId);
        run.Execute();
        return run;
    }

    static string CreateAppParam(string name, float[] scaleFactors, int maxFrames)
    {
        return API.UploadAppParam(name, new AppParams(scaleFactors, maxFrames));
    }
}
