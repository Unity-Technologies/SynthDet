# Setting up the SynthDet Unity project

These workflow steps describe the process of using the sample SynthDet project to create a synthetic dataset of grocery products.

<p align="center">
<img src="images/Synthetic Data pipeline-SynthDet local.png"/>
</p>

## Workflow

### Step 1: Open the SynthDet sample project

1. Open the Unity Hub
2. Click ***Add*** and select the `(repo root)/SynthDet` folder

The project will now be listed in Unity Hub. If you see a warning regarding Unity version, make sure you have the listed Unity version installed before proceeding.

3. In Unity Editor's ***Project*** window, find the **Scenes** folder and open the **SynthDet** Scene.
   
<p align="center">
<img src="images/SynthDetScene.png" width = "500"/>
</p>

### Step 2: Generate data locally 
1. With the `SynthDet` Scene open, press the the **▷** (play) button located at the top middle section of the editor to run your simulation. The sample project quickly generates a collection of random images, which you can see in the ***Game*** view. 

<p align="center">
    <img src="images/play.png" width = "400"/>
</p>

2. The `SynthDet` Scene continues for about one minute, then exits Play mode. Allow the Scene to run until it exits Play mode.

3. To view the dataset of generated data, navigate to the following location on your computer:
    - macOS: `~/Library/Application Support/UnityTechnologies/SynthDet`
    - Linux: `$XDG_CONFIG_HOME/unity3d/UnityTechnologies/SynthDet`
    - Windows: `%userprofile%\AppData\LocalLow\UnityTechnologies\SynthDet`

The image below is a sample RGB frame generated with SynthDet:

<p align="center">
<img src="images/dataset.png" width = "900"/>
</p>

You can now visualize the statistics of your dataset using a Jupyter notebook that we have prepared for SynthDet. 

**[Continue to instructions for using the SynthDet Statistics Jupyter notebook](NotebookInstructions.md)**