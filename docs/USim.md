# USim Help and Information 
Guide for navigating issues in USim that can block work flow in using the SynthDet Sample Project.

## Run in USim Window
<img src="images/USimRunWindow.PNG" align="middle"/>

This section to help explain the parameters in this window and what they mean and how they are used.

Run Name is used by USim to label the run 
Path to Build Zip is used for location of the linux player build of the target project, this will be unzipped and ran on a USim node
Scale factor range is a graph mapping the scale factor values from 0-1 range
Scale factor steps is the numver of samples between 0-1 that we will simulate on
Another way to think of scale factor range and steps is a change in the graph that min and max values in the graph range

## USim can’t find my project
If USim can’t find your execution id within the active project you may need to activate the Sample Project in order to find your execution id. This mainly applies if you have already been using Unity Simulation with other project 

You might see an error message like this:
usim summarize run-execution Ojbm1n0
{"message":"Could not find project=595c36c6-a73d-4dfd-bd8e-d68f1f5f3084, executionId=Ojbm1n0"}

Follow the steps for activate Unity Project in this [Link](https://github.com/Unity-Technologies/Unity-Simulation-Docs/blob/master/doc/quickstart.md#activate-unity-project) to help activate the correct project 