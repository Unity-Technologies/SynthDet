# SynthDet Documentation
<p align="center">
<img src="images/Synthetic Data pipeline-Perception Workflow.png"/>
</p>

## Project overview
This project utilizes the Unity [Perception](https://github.com/Unity-Technologies/com.unity.perception) package for randomizing the environment and capturing ground-truth on each frame. Randomization includes elements such as lighting, camera post processing, object placement, and background. Visit [this page](UnityProjectOverview.md) for a brief overview on how ground truth generation and domain randomization are achieved in SynthDet.

## Tutorials
1. [Prerequisites](Prerequisites.md)
2. [Setting up the SynthDet Unity project](GettingStartedSynthDet.md)
3. [Visualizing Dataset Statistics with the SynthDet Statistics Jupyter notebook](NotebookInstructions.md)
4. [Scaling up data generation by running SynthDet in Unity Simulation](RunningSynthDetCloud.md)
5. [Dataset evaluation with the Dataset Insights framework](https://datasetinsights.readthedocs.io/en/0.2.5/Evaluation_Tutorial.html)
6. [Running your trained model in the SynthDet Viewer AR App](https://github.com/Unity-Technologies/perception-synthdet-demo-app)

In addition to the above, in order to learn how to create a project like SynthDet from scratch using the Perception package, we recommend you follow the [Perception Tutorial](https://github.com/Unity-Technologies/com.unity.perception/blob/master/com.unity.perception/Documentation~/Tutorial/TUTORIAL.md).

## Additional documentation
* [Overview on how the SynthDet Unity project works](UnityProjectOverview.md)
* [Unity Perception package](https://github.com/Unity-Technologies/com.unity.perception)
* [Unity Dataset Insights Python package](https://github.com/Unity-Technologies/datasetinsights)
* [Background on Unity](BackgroundUnity.md)
* [Creating your own 3D assets](CreatingAssets.md)
