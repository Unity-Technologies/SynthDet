# Setting up the SynthDet Unity project

## Step 1: Setup

1. Download and install the latest version of **[Unity 2021.3](https://unity3d.com/get-unity/download)**  
2. Use the commands below to clone the SynthDet repository:
```
git lfs install
git clone https://github.com/Unity-Technologies/SynthDet
```
The SynthDet repository uses Git LFS for many of the files in the project. To verify that LFS is properly initialized, you can check the behavior of these files in Unity Editor. In the sample project, open the **SynthDet** Scene and enter Play mode. 

* If LFS is correctly initialized, the **SynthDet** Scene should run as normal. 
* If LFS is not correctly initialized, a message saying "Display 1 No Cameras Rendering" appears. 

If LFS is not correctly initialized, return to the command line and enter the following commands: 

```bash
git lfs install
git lfs pull
```
This should re-initialize LFS.

## Step 2: Open the SynthDet sample project

1. Open the Unity Hub
2. Click ***Add*** and select the `(repo root)/SynthDet` folder

The project will now be listed in Unity Hub. If you see a warning regarding Unity version, make sure you have the listed Unity version installed before proceeding.

3. In Unity Editor's ***Project*** window, find the **Scenes** folder and open the **SynthDet** Scene.
   
<p align="center">
<img src="images/SynthDetScene.png" width = "500"/>
</p>

## Step 3: Generate data locally 
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


> :information_source: [Visit the Unity project documentation page](UnityProjectOverview.md) for a brief overview on how ground truth generation and domain randomization are achieved in SynthDet.