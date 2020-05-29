# Getting Started with SynthDet

The goal of the workflow steps is to provide you with everything that you need to get started using the SynthDet project to create a synthetic dataset of grocery products and explore statistics on the dataset.

## Workflow (Step-by-step)

### Step 1: Open the SynthDet Sample project

1. Start the Unity Hub
2. Click Add... and select the (repo root)/SynthDet folder
3. Click the project to open it
4. In the project view in the editor find and locate the Scenes folder and open the MainScene

<img src="images/MainScene.PNG" align="middle"/>

### Step 2: Generating data locally
1. With MainScene open press play. You should see randomized images being quickly generated in the game view.
    <img src="images/PlayBttn.png" align="middle"/>
2. The MainScene will continue for ~1 minute before exiting. Allow the scene to run until play mode is exited.
3. To view the dataset, navigate to the following location depending on your OS:
    - OSX: `~/Library/Application\ Support/UnityTechnologies/SynthDet`
    - Linux: `$XDG_CONFIG_HOME/unity3d/UnityTechnologies/SynthDet`
    - Windows: `%userprofile%\AppData\LocalLow\UnityTechnologies\SynthDet`

<img src="images/dataset.png" align="middle"/>

### Step 3: View statistics using datasetinsights
Once the data is generated locally, `datasetinsights` can be used to show dataset statistics in a jupyter notebook via docker.

1. Run the `datasetinsights` docker image from DockerHub using the following command:

```docker run -p 8888:8888 -v <Synthetic Data File Path>:/data -t unitytechnologies/datasetinsights:0.1.0```

Replace `<Synthetic Data File Path>` with the path the local datasets (listed above in step 2.3).

> See [Docker Troubleshooting](DockerTroubleshooting.md) if you hit issues with Docker on Windows.

2. Open jupyter by navigating to http://localhost:8888 in a web browser.

    <img src="images/jupyterFolder.PNG" align="middle"/>

3. Navigate to `datasetinsights/notebooks` in jupyter
4. Open the SynthDet_Statistics.ipynb notebook

    <img src="images/theaNotebook.PNG" align="middle"/>

5. Follow the instructions in the notebook to compute dataset statistics
