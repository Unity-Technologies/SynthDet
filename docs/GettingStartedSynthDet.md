# Getting started with SynthDet

These workflow steps describe the process of using the sample SynthDet project to create a synthetic dataset of grocery products and explore statistics on that dataset. 

## Workflow

### Step 1: Open the SynthDet sample project

1. Open the Unity Hub
2. Click **Add** and select the (repo root)/SynthDet folder
3. Select the project to open it
4. In the Unity Editor's Project window, find the **Scenes** folder and open the **MainScene** 

<img src="images/MainScene.PNG" align="middle"/>

### Step 2: Generating data locally 
1. With **MainScene** open, press the Play button. The sample project quickly generates a collection of random images, which you can see in the Game view. 
    <img src="images/PlayBttn.png" align="middle"/>
2. The MainScene continues for about one minute, then exists Play mode. Allow the scene to run until it exits Play mode.
3. To view the dataset of generated data, navigate to the following location on your computer:
    - macOS: `~/Library/Application Support/UnityTechnologies/SynthDet`
    - Linux: `$XDG_CONFIG_HOME/unity3d/UnityTechnologies/SynthDet`
    - Windows: `%userprofile%\AppData\LocalLow\UnityTechnologies\SynthDet`

<img src="images/dataset.png" align="middle"/>

### Step 3: View statistics using datasetinsights
Once the data is generated locally, you can use`datasetinsights`  to show dataset statistics in a Jupyter notebook via Docker.

1. Use the following command to run the `datasetinsights` docker image from DockerHub:

```docker run -p 8888:8888 -v "<Synthetic Data File Path>":/data -t unitytechnologies/datasetinsights:0.1.0```

Replace `<Synthetic Data File Path>` with the path to the local datasets (listed in step 2.3).

> If you experience issues with Docker on Windows, see [the Docker documentation](Docker.md).

2. Open Jupyter. To do this, open a web browser and navigate to http://localhost:8888
   
    <img src="images/jupyterFolder.PNG" align="middle"/>

3. In Jupyter, navigate to `datasetinsights/notebooks` 
4. Open the notebook called SynthDet_Statistics.ipynb

    <img src="images/theaNotebook.PNG" align="middle"/>

5. Follow the instructions in SynthDet_Statistics.ipynb to compute dataset statistics
