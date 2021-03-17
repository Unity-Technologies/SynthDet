# SynthDet Documentation
<p align="center">
<img src="images/Synthetic Data pipeline-Perception Workflow.png"/>
</p>

## Installation & setup
* [Prerequisites](Prerequisites.md)

## Getting started with data generation in Unity

* Data generation example project - [Setting up the SynthDet Unity project](GettingStartedSynthDet.md)
* Visualizing dataset statistics - [Using the SynthDet Statistics Jupyter notebook](NotebookInstructions.md)
* Scaling up data generation - [Running SynthDet in Unity Simulation](RunningSynthDetCloud.md)
* Evaluating a dataset - [Dataset Evaluation with Dataset Insights framework](https://datasetinsights.readthedocs.io/en/0.2.5/Evaluation_Tutorial.html)
* Running your model with a mobile app - [SynthDet Viewer AR App](https://github.com/Unity-Technologies/perception-synthdet-demo-app)

## How does the SynthDet Unity project work?
This project utilizes the Unity [Perception](https://github.com/Unity-Technologies/com.unity.perception) package for randomizing the environment and capturing ground-truth on each frame. Randomization includes elements such as lighting, camera post processing, object placement, and background. Visit [this page](SynthDetRandomizations.md) for a brief overview on how ground truth generation and domain randomization are achieved in SynthDet.

Furthermore, in order to learn how to create a project like SynthDet from scratch using the Perception package, we recommend you follow the [Perception Tutorial](https://github.com/Unity-Technologies/com.unity.perception/blob/master/com.unity.perception/Documentation~/Tutorial/TUTORIAL.md).

## Additional documentation
* [The Randomizers used in SynthDet](HowSynthDetWorks.md)
* [Unity Perception package](https://github.com/Unity-Technologies/com.unity.perception)
* [Unity Dataset Insights Python package](https://github.com/Unity-Technologies/datasetinsights)
* [Background on Unity](BackgroundUnity.md)
* [Creating your own 3D assets](CreatingAssets.md)

## Help
* [Docker Help](Docker.md)
