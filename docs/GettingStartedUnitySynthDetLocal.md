# Getting Started with SynthDet

The goal of the workflow steps is to provide you with everything that you need to get started using the SynthDet project to create a synthetic dataset of grocery products and use that dataset to train a Faster R-CNN object detection model. 

## Workflow (Step-by-step)

### Step 1: Open the SynthDet Sample project
Verify you can open the project and the example scene runs and generates data locally on your machine.

1. In the SynthDet [repository you cloned](https://github.cds.internal.unity3d.com/unity/google-dr-paper/tree/master)
    1. In a command prompt navigate to the root folder and run the command **git submodule update --init --recursive**
    2. Open the example project in the root called SynthDet 
2. In the Scenes folder open the MainScene 

### Step 2: Generating data locally 
The project when you press play will generate a local dataset to your local data folder. 

1. With the MainScene open press play and observe the different products quickly being generated in the game view
2. The MainScene will continue for ~1 minute and exit playmode, allow the scene to run until play mode is exited
2. To view the files being generated in a file explorer navigate to the following location: Application Perstaint file path + .\UnityTechnologies\SynthDet
    1. OSX: ./Library/Application Support/UnityTechnologies/SynthDet
    2. Linux: $XDG_CONFIG_HOME/unity3d/UnityTechnologies/SynthDet
    3. Windows: %userprofile%\AppData\LocalLow\UnityTechnologies\SynthDet

<img src="images/dataset.png" align="middle"/>

### Step 3: Creating and using a Notebook for Model Training 
Once the data is generated locally you can use the created dataset to run a local docker image to train a model based on the dataset.

1. This command mounted directory $home/data in your local filesystem to /data inside the container. If you have saved your data to a different location, change the local directory path to match the directory where the synthetic data is stored
    1. docker run -p 8888:8888 -v <Synthetic Data File Path/data>:/data -t unitytechnologies/datasetinsights:0.0.1

## Opening the docker image locally
1. There are two options for opening the docker image locally  
    1. In a internet browser go to http://localhost:8888 in a web browser to open the notebook
    
    <img src="images/LocalWebpageThea.jpg" align="middle"/>
    
    2. Open the Docker Dashboard and select the image that was created using thea, then select open in browser in the top right ribbon 

    <img src="images/DockerDashboard.PNG" align="middle"/>

### Running the Notebook
1. Make sure that the local webpage for the image is open

2. Open the folder thea/notebooks in the local page directory 
    1. Locate the notebook called SynthDet_Statistics 
    <img src="images/theaNotebook.PNG" align="middle"/>

3. Open the SynthDet_Statistics.ipynb notebook

4. Click Cell->Run All at the top of the notebook, this will run through the notebook sections
<img src="images/RunAll.PNG" align="middle"/>

5. The notebook will start generating graphs to organize the metrics for the model
