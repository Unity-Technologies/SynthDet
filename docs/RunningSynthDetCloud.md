# Running SynthDet in Unity Simulation

<p align="center">
<img src="images/Synthetic Data pipeline-SynthDet cloud.png"/>
</p>
These instructions demonstrate how to use Unity Simulation to generate a dataset at scale.

Before following the instructions below, make sure you have:
* Set up all the requirements for running in Unity Simulation as explained [here](Prerequisites.md).
* Set up the SynthDet Unity project by following the instructions [here](GettingStartedSynthDet.md).

## Workflow

### Step 1: Unity Cloud Project

To access Unity Simulation, the project must be connected to Unity Cloud Services.

1. Follow the steps outlined [here](https://docs.unity3d.com/Manual/SettingUpProjectServices.html) to create a Unity Cloud organization and project, and connect your local project to the cloud project.

### Step 2: Run SynthDet in Unity Simulation

1. From the top menu bar in Unity Editor, open _**Window -> Run in Unity Simulation**_.

<p align="center">
<img src="images/runinusim.png" width="600"/>
</p>

Here, you can specify a name for the run, the number of images that will be generated (Iterations), and the number of simulation nodes the work will be distributed across (Instances) for the run. 

2. Name your run `FirstRun`, set the number of Iterations to `1000`, and Instances to `20`. 
3. Click _**Build and Run**_.

Your project will now be built and then uploaded to Unity Simulation and run. This may take a few minutes to complete, during which the editor may become frozen; this is normal behaviour.

4. Once the operation is complete, you can find the **Execution ID** of this Unity Simulation run in the **Console** tab and the ***Run in Unity Simulation*** Window: 

<p align="center">
<img src="images/build_uploaded.png" width="600"/>
</p>

### Step 3: Monitor status using Unity Simulation CLI
You can now use the Unity Simulation CLI, which you previously downloaded as part of the prerequisites, to keep track of the run you just initiated.

1. Open a command line interface and navigate to the Unity Simulation CLI for your platform.
2. Enter the command `usim login auth` and follow the instructions. This authorizes your account and logs in.
3. Get a list of cloud projects associated with your Unity account using the `usim get projects` command.
   
Example output:

```
 name                  id                                       creation time             
--------------------- ---------------------------------------- --------------------------- 
 Perception Tutorial   acd31956-582b-4138-bec8-6670be150f09    2020-09-30T00:33:41+00:00 
 SynthDet              9ec23417-73cd-becd-9dd6-556183946153 *  2020-08-12T19:46:20+00:00  
 ```

> :information_source: In case you have more than one Unity Cloud project, you will need to "activate" the one corresponding with your SynthDet project. If there is only one project, it is already activated, and you will not need to execute the command below.

4. Activate the relevant project with the command `usim activate project <project id>`.
5. Get a summary for your run execution using the command `usim summarize run-execution <execution id>`.

An example output of the above command looks like this:
```
 state         count 
------------- -------
 Successes     0     
 In Progress   20     
 Failures      0     
 Not Run       0    
 ```

 At this point, we will need to wait until the execution is complete. Check your run with the above command periodically until you see a 20 for `Successes` and 0 for `In Progress`. Given the relatively small size of our Scenario (1,000 Iterations), this should take less than 5 minutes.

6. When execution is complete, use the `usim download manifest <execution-id>` command to download the execution's manifest. 

The manifest is a `.csv` formatted file and will be downloaded to the same location from which you execute the above command. This file does **not** include actual data, rather, it includes links to the generated data, including the JSON files, the logs, the images, and so on.

7. Open the manifest file to check it. Make sure there are links to various types of output and check a few of the links to see if they work.

### Step 6: Visualize dataset statistics using the SynthDet Statistics Jupyter notebook

You can now use our provided Jupyter notebook, which uses the Dataset Insights Python package, to visualize the statistics of the dataset you just generated on Unity Simulation. If you previously set this up for a local dataset, you can just open the same notebook. If not, please follow the instructions [here](NotebookInstructions.md) to set up the notebook on your computer.

Once in the notebook, you will need to run a few lines of code that are bespoke to Unity Simulation datasets. You will find these lines of code commented in the notebook.

1. In the block of code titled "Unity Simulation [Optional]", uncomment the lines that assign values to variables, and insert the correct values, based on information from your Unity Simulation run. 

We have previously learned how to obtain the `run_execution_id` and `project_id`. You can remove the value already present for `annotation_definition_id` and leave it blank. What's left is the `access_token`.

2. Return to your command-line interface and run the `usim inspect auth` command.

If you receive errors regarding authentication, your token might have expired. Repeat the login step (`usim login auth`) to login again and fix this issue.

A sample output from `usim inspect auth` will look like below:

```
Protect your credentials. They may be used to impersonate your requests.
access token: Bearer 0CfQbhJ6gjYIHjC6BaP5gkYn1x5xtAp7ZA9I003fTNT1sFp
expires in: 2:00:05.236227
expired: False
refresh token: FW4c3YRD4IXi6qQHv3Y9W-rwg59K7k0Te9myKe7Zo6M003f.k4Dqo0tuoBdf-ncm003fX2RAHQ
updated: 2020-10-02 14:50:11.412979
```

The `access_token` you need for your Dataset Insights notebook is the access token shown by the above command, minus the `'Bearer '` part. So, in this case, we should input `0CfQbhJ6gjYIHjC6BaP5gkYn1x5xtAp7ZA9I003fTNT1sFp` in the notebook. 

3. Copy the access token excluding the `'Bearer '` part to the corresponding field in the Jupyter notebook.
4. Execute the rest of the code blocks to visualize all available statistics for your dataset. Where applicable, uncomment parts of the code that are bespoke to Unity Simulation.