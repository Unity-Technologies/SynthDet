# Running SynthDet in Unity Simulation

This walkthrough shows you how to generate a dataset at scale using Unity Simulation.

If you would like to use Unity Simulation sign up for the [Unity Simulation Beta.](https://unity.com/products/simulation)

## Workflow

### Step 1: Set up additional prerequisites
See the Additional requirements for running in Unity Simulation section in [Prerequisites](Prerequisites.md).

### Step 2: Open the SynthDet sample project
Follow steps 1 and 2 in the [Getting Started with SynthDet](GettingStartedSynthDet.md).

### Step 3: Connect to Cloud Services 
To access Unity Simulations in Cloud Services, the project must be connected to Cloud Services and an org ID. To run the app on Unity Simulation, connect to Cloud Services and create a new Unity Project ID using the following steps:

1. In the top-right corner of the Editor click the cloud button. This opens the Services tab. 

<img src="images/OpenCloudServices.png" align="middle"/>

2. Ensure you are logged into your Unity account
3. Create a new Unity Project ID 

<img src="images/CreateNewUnityProjectID.png" align="middle"/>

4. When creating your project ID, select the organization you want for the project

<img src="images/UnityProjectIdOrg.PNG" align="middle"/>

If you need more information, see [Setting up your project for Unity Services](https://docs.unity3d.com/Manual/SettingUpProjectServices.html) in the Unity manual. 

### Step 4: Run SynthDet in Unity Simulation

1. Start a run in Unity Simulation using the Run in Unity Simulation window. When the run is executed it takes approximately ten minutes to complete.  
    1. Click **Window** > **Run in USim…**
    2. Enter a name in the Run Name field, for example "SynthDetTestRun"
    3. For more information on the parameters in this window see the [Unity Simulation information guide](UnitySimulationHelpInformation.md).

<img src="images/USimRunWindow.PNG" align="middle"/>

3. Click **Execute on Unity Simulation**. It takes some time for the runs to complete and the Editor may seem frozen. However, the Editor is executing the run. 
4. When the run is complete check the console log and take note down the run-execution ID and build ID from the debug message: 

<img src="images/NoteExecutionID.PNG" align="middle"/>

If you run into issues, see [Unity Simulation help and information](UnitySimulationHelpInformation.md). 

### Step 5: Monitor status using Unity Simulation CLI
When the Unity Simulation run has been executed, its completion must be verified.

1. Check the current summary of the execution run in Unity Simulation because the run must be completed
    1. Open a command line interface and navigate to the USim CLI for your platform 
    2. Run the`usim login auth` command: this authorizes your account and logs in
    3. In the command window run this command: `summarize run-execution <execution id>`
        1. If you receive an error about the active project please see [Unity Simulation Help](UnitySimulationHelpInformation.md)
        2. You might need to run the command a few times because you don’t want to continue until the run reports that it has completed 

<img src="images/usimSumExecution.PNG" align="middle"/>

2. (Optional) Download the manifest and check the generated data 
    1. Run the cmd `usim download manifest <execution id>`
    2. This downloads a CSV file that contains links to the generated data
    3. Verify that the data looks good before continuing

### Step 6: Run dataset statistics using the datasetinsights Jupyter notebook

1. Run the `datasetinsights` docker image from DockerHub using the following command:

```docker run -p 8888:8888 -v $HOME/data:/data -t unitytechnologies/datasetinsights:0.0.1```

Replace `$HOME/data` with the path where you want the dataset to be downloaded.

> If you hit issues with Docker on Windows, see [Docker](Docker.md).

2. Open Jupyter by navigating to http://localhost:8888 in a web browser.
   
    <img src="images/jupyterFolder.PNG" align="middle"/>

3. Navigate to `datasetinsights/notebooks` in Jupyter 
4. Open the SynthDet_Statistics.ipynb notebook

    <img src="images/theaNotebook.PNG" align="middle"/>

5. Follow the Unity Simulation instructions in the notebook to compute dataset statistics
