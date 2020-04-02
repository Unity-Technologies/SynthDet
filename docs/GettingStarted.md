# Getting Started with Unity SynthDet

The purpose of this document is to provide you with everything that you need to get started in using the sample project. 

## Table of Contents

## What is included with this release?
The deliverable of the SynthDet alpha includes the following components:

1. SynthDet  example project
2. Perception SDK 0.1.0

## What is outside of the release?

Other products that will be while using the SynthDet  alpha:
1. Annotation of Data 
2. AR Example App
3. Thea<Link>
4. Annotated real-world dataset<Link>
5. Google Cloud Services 

## What do we expect you to be evaluating?
This is our Sample Project and we expect that this will get you familiar with the way that assets, scene creation, annotation, Thea, and the running of simulations. In this current deliverable, the primary elements that we expect alpha participants to test and provide feedback for are the Perception SDK, annotation data,using Thea for creating trained data models, and execution of simulations on the local machine or Unity Simulation platform.

## Prerequisites
### Background
If you haven’t used Unity before, it’s important to familiarize yourself with the interface and a few key concepts. One great way to do this is to go through the [roll-a-ball tutorial](https://learn.unity.com/project/roll-a-ball-tutorial). There are three concepts that we want to bring particular attention to for the SynthDet Alpha:

1. A Unity [Package](https://docs.unity3d.com/Manual/Packages.html) is the means by which we are delivering the alpha software.
2. [Scenes](https://docs.unity3d.com/Manual/CreatingScenes.html) are how Unity represents environments and will be the output for the tool.
3. [Unity Simulation](https://unity.com/products/simulation) for running simulations

### Things that you will need for this evaluation
Tools for installation and configuration: 
1. Unity Account to connect to Unity Services 
2. [Unity Hub](https://docs.unity3d.com/Manual/GettingStartedInstallingHub.html) for obtaining the appropriate editor version (might change per release)
3. Unity Editor 2019.3 release (2019.3.6f1) installed from Unity Hub
    1. Please install target platform support i.e. Mac, Linux or both platform support
3. A GitHub account. The SynthDet github repository will be a public repository for users to download and use as needed 
5. The Sample project, downloaded from the GitHub Repository.
6. A Unity Simulation Account 
  1. In order to run on Unity Simulation, you need an account in the Unity Simulation Beta program. Please see this [sign-up](https://unity.com/products/simulation) page to get started on Unity Simulation Beta. Simulations can be run on the local machine without Unity Simulation but will execute at much higher scale on Unity Simulation.
7. USim CLI installed with CLI tool 
8. Claim the credit for workflow costs on USim
9. Docker Installed
10. Google Cloud CLI and a GCP account 
  1. You will need to create a gcloud project to use for Thea
11. Python 3.7

Hardware needed for the project:
1. Webcam connected to your machine

You may optionally prepare your own assets for use , including any of the free assets from the [Unity Asset Store](https://api.unity.com/v1/oauth2/authorize?client_id=asset_store_v2&locale=en_US&redirect_uri=https%3A%2F%2Fassetstore.unity.com%2Fauth%2Fcallback%3Fredirect_to%3D%252F&response_type=code&state=111348b2-64c1-4b78-8456-9a1888b218c1). We provide instructions for preparing assets for import and usage in the appendix of this document.

## Workflow (Step-by-step)
### Step 1: Open the SynthDet Sample project
1. Git clone the SynthDet repo from the [GitHub](https://github.com/Unity-Technologies/SynthDet) repo
2. In the scenes open the SampleScene 
3. Press play and observe the different products quickly being generated in the game view

<img src="images/dataset.png" align="middle"/>

### Step 2: Connect to Cloud Services 
1. In order to run USim from the project you need to connect to cloud services and create a new Unity Project ID. Please follow the steps:
  1. In the top right corner of the editor click the cloud button
    1. This will open the “Services” tab

<img src="images/OpenCloudServices.png" align="middle"/>

2. Make sure you are logged into your unity Account as well
3. Create a new Unity Project ID 

<img src="images/CreateNewUnityProjectID.png" align="middle"/>

4. When creating your project ID make sure select the desired organization for the project

<img src="images/UnityProjectIdOrg.png" align="middle"/>

5. Here is a [unity link](https://docs.unity3d.com/Manual/SettingUpProjectServices.html) for the services creation in case further information is needed

### Step 3: Preparing a Build in Unity Editor 
#### Creating the Build
1. Open up the Build Settings under the File or by using the ctrl + B shortcut
2. Switch the Target platform to Linux 

<img src="images/targetingLinuxPlatform.png" align="middle"/>

3. Create a Linux build of the project with Click build

#### Preparing the USim Build
1. Once the Linux build is complete, navigate to <Project>\Build\LinuxBuild and use a utility to zip the build
  1. It is important to zip the build so the root folder contains the files for the build and not contain a extra folder in the path
  2. You can do this by selecting all the files in the Linux build directory and then right clicking <PlayerBuild>.x86_64 and send to a Zip folder
  3. If the build contains a folder with the build files inside of that folder the build will fail in USim

<img src="images/exampleLinuxZipBuild.png" align="middle"/>

5. Start a run in USim using the USim run window 
  1. Under Window click Run in USim…
  2. Fill out the Run name  
  3. Fill out the path to player build.zip you created in step 4

<img src="images/USimRunWindow.png" align="middle"/>

6. Click “Execute in Unity Simulation”
7. Take note and copy down the run-execution id from the Console window

<img src="images/NoteExecutionID.png" align="middle"/>

### Step 4: Download manifest from USim
1. First we want to check the current summary of the execution run in the console window for USim
  1. Open a cmd line and navigate to the USim CLI for your platform 
  2. In the cmd window run summarize run-execution <execution id>
  3. You may need to run this a few times because you don’t want to continue until the run is completed 

<img src="images/usimSumExecution.png" align="middle"/>

2. Next we need to download the data manifest from the run and check the data 
  1. Run the cmd “usim download manifest <execution id>
  2. This will download a csv file that will contain links to the generated data
  3. Verify some of the data looks good before continuing

### Step 5: Creating the trained model using Thea
#### Docker Setup
1. Make sure to clone the github repo for Thea to access to the docker image
2. Build and push Docker image to use for GCP platform 
3. In <Thea Repo File Path>\thea\configs open up the file faster_rcnn_synthetic.yaml
  1. Modify the run_execution_id: to execution id from Step 4 when you created the executed the USim build
4. In a Cmd console of your choice follow the steps below:
  1. Index for docker cmd examples
  2. TAG = name you want to tag your image with 
  3. GCP_PROJECT_ID - glcoud project id that you set up 
  4. glcoud config project set <GCP_PROJECT_ID>  
  5. docker build -t thea:$TAG <file path to the target docker image>
  6. docker tag thea:$TAG gcr.io/$GCP_PROJECT_ID/thea:$TAG 
  7. docker push gcr.io/$GCP_PROJECT_ID/thea:$TAG 

####Submit CloudML Jobs
1. In the cmd window we need to submit a job to the ML cloud 
2. In a Cmd console of your choice follow the steps below:
  1. Index for docker cmd examples
  2. JOB_NAME = deeplabv3_$(date +%Y%m%d_%H%M%S)
  3. gcloud ai-platform jobs submit training $JOB_NAME \
  --region us-central1 \
  --master-image-uri gcr.io/$GCP_PROJECT_ID/thea:$TAG \
  --scale-tier custom \
  --master-machine-type standard_v100 \
  -- \
  1 train \
  --config=thea/configs/deeplabv3.yaml \
  --logdir=gs://thea-dev/runs/$JOB_NAME \
  --val-interval=1 \
  train.epochs 100

### Step 6: AR Example application
1. In the SynthDet repo from the GitHub you cloned in Step 1 there where will be a folder names Model Demo
  1. Take note of the file path 
2. Inside of a python environment run the cmd python run_demo.py
3. After a short delay, you should see a window open up with your webcam stream 
4. Hold up a cereal box or any other grocery item to your webcam and you should see boxes draw around any detected objects in the stream.

# Appendix: 
## File structure 
1. Images/
  1. 123.PNG
2. annotations.json
3. train.txt
4. val.txt
5. test.txt

## Supported Platforms 
1. Windows?
2. Linux 
3. Mac

## Issues with Docker
If you are running on a Windows platform you might run into an issue where Docker is unable to start because vmcompute can’t start. It seems there is a bug in Docker ver 2.2.0.3 on Windows OS 1909 that is causing issues with docker

Example of the error:
Docker.Core.Backend.BackendDestroyException: Unable to stop Hyper-V VM: Service 'Hyper-V Host Compute Service (vmcompute)' cannot be started due to the following error: Cannot start service vmcompute on computer '.'.

### Steps:
1. Open “Windows Security”
2. Open “App & Browser control”
3. Click “Exploit protection settings” at the bottom
4. Switch to “Program Settings tab”
5. Locate “C:\Windows\System32\vmcompute.exe” in the list and expand it
6. If the “Edit” window didn’t open click “Edit”
7. Scroll down to “Clode flow guard (CFG) and uncheck “Override system settings”
8. Open a administrator powershell
9. Start vmcompute from powershell “net start vmcompute”
10. Start the Docker Desktop

If this method doesn’t work for you may have to select “Factory Reset” in the Docker error window, this should keep settings intact.

If you are still facing issues with Docker there are some known git issues with Linux subsystem which may require you to downgrade to 2.1.0.5



## Getting Started with USim
There are a few steps in order to get started with unity Simulation, please follow the links and instructions below.

Getting started with [Unity Simulation](https://forum.unity.com/threads/getting-started-with-unity-simulation.748778/?_ga=2.138985715.1907021259.1585344388-335557205.1582053166), follow this link to the forum post with step by step instructions. Below is a quick list of things you will need to have completed in order to start.
You need to sign up for Beta access to Unity simulation
Have a supported editor installed 
Install and setup the Unity Simulation prerequisites 

USim can’t find my project
If USim can’t find your execution id within the active project you may need to activate the Sample Project in order to find your execution id. This mainly applies if you have already been using Unity Simulation with other project 

You might see an error message like this:
usim summarize run-execution Ojbm1n0
{"message":"Could not find project=595c36c6-a73d-4dfd-bd8e-d68f1f5f3084, executionId=Ojbm1n0"}

Follow the steps for activate [Unity Project](https://github.com/Unity-Technologies/Unity-Simulation-Docs/blob/master/doc/quickstart.md#activate-unity-project) in this link to help activate the correct project 


## Docker 
When setting up docker to push to a gcloud project you might run into an issue where you can’t push because of an authentication issue. If this happens it is most likely an issue where docker has not been configured with gcloud. You can fix this by running gcloud auth configure-docker in a cmd window going through the prompts.

[Google advanced authentication](https://cloud.google.com/container-registry/docs/advanced-authentication) for further documentation using docker with google services.

## Modifying the AR app example 
If you would like to change the clases or models that the app detects you can do so by modifying object_detector/class_labels.py script
