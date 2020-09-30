# Unity Simulation Help and Information 
Guide for navigating issues in Unity Simulation that can block work flow in using the SynthDet Sample Project.

## Run in USim Window

This section will explain the Run in USim window and the meanings behind the parameters that are needed to create a Unity Simulation run

<img src="images/USimRunWindow.PNG" align="middle"/>

## Parameters
You can refer to [Unity Simulation Randomization Parameters](UnitySimulationRandomizationParameters.md) 
doc for each randomization parameter definition.

## Unity Simulation can’t find my project
If Unity Simulation can’t find your execution id within the active project you may need to activate the Sample Project in order to find your execution id. This mainly applies if you have already been using Unity Simulation with other project 

You might see an error message like this:
usim summarize run-execution Ojbm1n0
{"message":"Could not find project=595c36c6-a73d-4dfd-bd8e-d68f1f5f3084, executionId=Ojbm1n0"}

Follow the steps for activate Unity Project in this [Link](https://github.com/Unity-Technologies/Unity-Simulation-Docs/blob/master/doc/quickstart.md#activate-unity-project) to help activate the correct project 
