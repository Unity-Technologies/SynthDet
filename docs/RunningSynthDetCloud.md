# Running SynthDet in Unity Simulation

This walkthrough shows how to generate a dataset at scale using Unity Simulation.

If you would like to use Unity Simulation please sign up for the [Unity Simulation Beta.](https://unity.com/products/simulation)

## Workflow (Step-by-step)

### Step 1: Set up additional prerequisites
See the "Additional requirements for running in Unity Simulation" section in [Prerequisites](Prerequisites.md)

### Step 2: Open the SynthDet Sample project
Please follow the Steps 1 & 2 in the [Getting Started with SynthDet](GettingStartedSynthDet.md)

### Step 3: Connect to Cloud Services
The project will need to be connected to cloud services and a org id in order to access Unity Simulations in the cloud services

1. To run the app on Unity Simulation, connect to cloud services and create a new Unity Project ID using the following steps:
    1. In the top right corner of the editor click the cloud button
        1. This will open the “Services” tab

<img src="images/OpenCloudServices.png" align="middle"/>

2. Make sure you are logged into your Unity account as well
3. Create a new Unity Project ID

<img src="images/CreateNewUnityProjectID.png" align="middle"/>

4. When creating your project ID make sure select the desired organization for the project

<img src="images/UnityProjectIdOrg.PNG" align="middle"/>

5. Here is a [Unity link](https://docs.unity3d.com/Manual/SettingUpProjectServices.html) for the services creation in case further information is needed

### Step 4: Run SynthDet in Unity Simulation

1. Start a run in Unity Simulation using the Run in USim window, once the run is executed it will take time ~ 10 mins for the run to complete
    1. Under Window -> Run in USim…
    2. Fill out the Run Name with an example name i.e. SynthDetTestRun
    3. If you are curious about the parameters in this window check out the [Unity Simulation information guide](UnitySimulationHelpInformation.md)

<img src="images/USimRunWindow.PNG" align="middle"/>

3. Click “Execute in Unity Simulation”
    1. This will take some time for the runs to complete and the editor may appear frozen however it is executing the run
4. Once the run is complete check the console log and take note and copy down the run-execution id and build id from the debug message

<img src="images/NoteExecutionID.PNG" align="middle"/>

If you run into issues, check [Unity Simulation Help and Information](UnitySimulationHelpInformation.md)

### Step 5: Monitor status using Unity Simulation CLI
Once the Unity Simulation run has been executed, the run needs to be verified that it has completed.

1. Check the current summary of the execution run in Unity Simulation since we need the run to be completed
    1. Open a command line interface and navigate to the USim CLI for your platform
    2. Run the command `usim login auth`, this will authorize your account and log in
    3. In the command window run this command `summarize run-execution <execution id>`
        1. If you receive an error about the active project please go to [Unity Simulation Help](UnitySimulationHelpInformation.md)
        2. The command may need to be ran few times because you don’t want to continue until the run reports that it has completed

<img src="images/usimSumExecution.PNG" align="middle"/>

2. (Optional) Download the manifest and check the generated data
    1. Run the cmd `usim download manifest <execution id>`
    2. This will download a csv file that will contain links to the generated data
    3. Verify some of the data looks good before continuing

### Step 6: Run dataset statistics using the datasetinsights jupyter notebook

1. Run the `datasetinsights` docker image from DockerHub using the following command:

```docker run -p 8888:8888 -v $HOME/data:/data -t unitytechnologies/datasetinsights:latest```

Replace `$HOME/data` with the path where you want the dataset to be downloaded.

> See [Docker Troubleshooting](DockerTroubleshooting.md) if you hit issues with Docker on Windows.

2. Open jupyter by navigating to http://localhost:8888 in a web browser.

    <img src="images/jupyterFolder.PNG" align="middle"/>

3. Navigate to `datasetinsights/notebooks` in jupyter
4. Open the SynthDet_Statistics.ipynb notebook

    <img src="images/theaNotebook.PNG" align="middle"/>

5. Follow the Unity Simulation instructions in the notebook to compute dataset statistics
