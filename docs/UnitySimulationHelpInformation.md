# Unity Simulation troubleshooting
This is a guide for navigating issues in Unity Simulation that can block your workflow when using the SynthDet Sample Project.

## Run in Unity Simulation window

This section explains the Run in Unity Simulation window and the definitions of the parameters that SynthDet needs to create a Unity Simulation run:

<img src="images/USimRunWindow.PNG" align="middle"/>

### Parameters
* **Run Name** is used by Unity Simulation to label the run being executed on a node  
* **Path to build zip** is used for the location of the Linux player build of the target project: the user unzips this and runs it on a Unity Simulation node
* **Scale Factor Range** is a graph mapping the scale factor values in the 0-1 range
* **Scale Factor Steps** is the number of samples between 0-1 that the user simulates on
* Another way to think of Scale Factor Range and Scale Factor Steps is a change in the graph that min and max values in the graph range

## Unity Simulation can’t find my project
If Unity Simulation can’t find your execution ID in the active project you might need to activate the sample project in order to find your execution ID. This mainly applies if you have already used Unity Simulation with other projects. 

You might see an error message like this:
`usim summarize run-execution Ojbm1n0
{"message":"Could not find project=595c36c6-a73d-4dfd-bd8e-d68f1f5f3084, executionId=Ojbm1n0"}`

To ensure you activate the correct project, follow the steps in the Unity Simulation Quick Start Guide in the  [Activate Unity Project](https://github.com/Unity-Technologies/Unity-Simulation-Docs/blob/master/doc/quickstart.md#activate-unity-project) section. 

