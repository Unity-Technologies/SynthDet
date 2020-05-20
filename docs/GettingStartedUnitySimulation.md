# Unity Simulation Quick Start Guide

The goal of this guide is to walk through the absolute minimum steps required to execute a simulation on the Unity Simulation Cloud Service while introducing the core components and requirements of Unity Simulation.

Following this quick start guide you will execute an instance of a simple Unity project, RollABall, with a single application parameter to the Unity Simulation Cloud Service and then download the generated data.

Prior to starting ensure that you have read through the Unity Simulation  [Taxonomy Guide](taxonomy.md) and that all prerequisites have been met by referencing the [Requirements Guide](requirements.md).

## Guide

Following are the set of steps in this quick start guide

---

| Section | Name                                                                                                              | Description                                                           |
| ------- | ----------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| 1.      | [Download Unity Simulation Bundle Quick Start Materials](#download-unity-simulation-bundle-quick-start-materials) | Download the Unity Simulation CLI with quick start materials          |
| 2.      | [Login](#login)                                                                                                   | Login to Unity Simulation via the CLI                                 |
| 3.      | [Activate Unity Project](#activate-unity-project)                                                                 | Activate a Unity project with Unity Simulation                        |
| 4.      | [Just the Commands](#just-the-commands)                                                                           | Just the commands to upload and execute a simulation                  |
| 5.      | [Upload build](#upload-build)                                                                                     | Upload a Unity player simulation linux build                          |
| 6.      | [Upload Application Parameter](#upload-application-parameter)                                                     | Upload Application Parameters associated with your simulation Run     |
| 7.      | [Define Run](#define-run)                                                                                         | Define a Simulation Run that ties your build, sys-param and app-param |
| 8.      | [Upload Run Definition](#upload-run-definition)                                                                   | Upload a previously generated run definition file                     |
| 9.      | [Execute Run](#execute-run)                                                                                       | Execute an uploaded Run definition                                    |
| 10.     | [Check Run Status](#check-run-status)                                                                             | Check the status of an executing Run                                  |
| 11.     | [Download data](#download-data)                                                                                   | Download data associated with a Run                                   |
| ---     |                                                                                                                   |                                                                       |

### Download Unity Simulation Bundle Quick Start Materials

The latest quick start materials, `unity_simulation_bundle.zip`, and Unity Simulation SDK can be downloaded from [here](https://github.com/Unity-Technologies/Unity-Simulation-Docs/releases) under the `Assets` drop down of the latest release. These instructions are also available for download as either of the `Source code` options.

> ![unity_simulation_bundle](images/assets-bundle.png "unity_simulation_bundle")

Unity Simulation Bundle Quick Start Materials includes:

- Sample Application Parameter file
- Sample RunDefinition file  # For reference purposes only.
- Sample Dockerfile file # Used for local testing scenarios
- Zipped SynthDet Linux build
- Unity Simulation CLI for OS X and Windows 10

Unzip the quick start materials, `unity_simulation_bundle.zip`, to a well known directory, open a terminal window and navigate to the unzipped directory using the `cd` command.

*NOTE*: All of the following commands will be run from the unzipped `unity_simulation_bundle` directory.

For example, if the quick start materials were downloaded and unzipped in the `Downloads` directory one of the following commands will change to the correct directory to execute the Unity Simulation CLI commands in the terminal window.

macOS:

```console
$ cd ~/Downloads/unity_simulation_bundle
```

Windows:

```posh
> cd C:\Users\MyName\Downloads\unity_simulation_bundle
```

*NOTE*: macOS users may encounter issues with Gatekeeper; see the [troubleshooting guide](https://github.com/Unity-Technologies/Unity-Simulation-Docs/blob/master/doc/troubleshooting.md#macos-gatekeeper) for help.

### Login

Use your Unity credentials to login via the CLI.

macOS:

```console
$ USimCLI/mac/usim login auth
```

```console
$ USimCLI/mac-legacy/usim login auth
```

Ubuntu:

```console
$ USimCLI/ubuntu18.04/usim login auth
```

```console
$ USimCLI/ubuntu16.04/usim login auth
```

Windows:

```posh

> USimCLI\windows\usim.exe login auth
```

This will either ask you to enter your Unity credentials in a browser or if you are already logged in to the Unity cloud developer console you will automatically be redirected to the ‘Login Successful’ page. On a successful login you will see the following returned back in the terminal.

```You have been successfully authorized to use Unity Simulation services.```

*NOTE*: In the event that your token expires you can run the following command to refresh your authentication.

macOS:

```console
$ USimCLI/mac/usim refresh auth
```

```console
$ USimCLI/mac-legacy/usim refresh auth
```

Ubuntu:

```console
$ USimCLI/ubuntu18.04/usim refresh auth
```

```console
$ USimCLI/ubuntu16.04/usim refresh auth
```

Windows:

```posh
> USimCLI\windows\usim.exe refresh auth
```

### Activate Unity Project

All runs and data created in Unity Simulation must be associated with a Unity cloud project Id for storage and retrieval purposes.

Prior to starting the run please ensure that you have a new or existing unity project available within your organization. If you need to create a Unity cloud project navigate to the [Unity developer dashboard](https://developer.cloud.unity3d.com/projects/) and click the `Create New Project` button in the upper right corner.

![unity project page](images/qs-1.png "Unity Project page")

Back in the terminal run the following command to set the active Unity Project ID in Unity Simulation.

macOS:

```console
$ USimCLI/mac/usim activate project
```

```console
$ USimCLI/mac-legacy/usim activate project
```

Ubuntu:

```console
$ USimCLI/ubuntu18.04/usim activate project
```

```console
$ USimCLI/ubuntu16.04/usim activate project
```


Windows:

```posh
>USimCLI\windows\usim.exe activate project
```

If there is a Project ID that is currently active you will receive a deactivation prompt.

```console
Project 286e8fb9-1694-4574-97ec-25903395ca71 is currently active. Deactivate it (Y/n)? y
```

Enter the number corresponding with the Unity Cloud Project you would like to associate with Unity Simulation.

```console
Active Project ID: 286e8fb9-1694-4574-97ec-25903395ca7d

choice   name                                  id                                     creation time
-------- ------------------------------------- -------------------------------------- ---------------------------
1        PrepDemo                              ec38c5c0-c94a-4688-91a2-2d45c8078691   2019-04-18T03:57:23+00:00
2        Demo2                                 784ee19c-115f-448a-8ba4-cc459de5f271   2019-04-04T04:19:01+00:00

Enter choice number: 1


-- Returns

Project ec38c5c0-c94a-4688-91a2-2d45c8078693 activated.
```

Once a Unity Cloud Project ID has been activated you can begin uploading the necessary files to execute a simulation.

#### Just The Commands



All commands must be executed from the `unity_simulation_bundle` directory downloaded in the [Download](#download-unity-simulation-bundle-quick-start-materials) step of this guide and user must have at least one Unity cloud project described in the [Activate](#activate-unity-project) step.

macOS 10.15 Catalina (replace `mac` with `mac-legacy` for 10.13 High Sierra and 10.14 Mojave)
```shell
$ USimCLI/mac/usim login auth
$ USimCLI/mac/usim activate project

$ USimCLI/mac/usim upload build ./RollaballLinuxBuild/rollaball_linux_build.zip
    # Outputs  Build ID

$ USimCLI/mac/usim upload app-param ./AppParams/app-param.json
    #  Outputs AppParam ID

$ USimCLI/mac/usim define run
    # Outputs Run Definition ID

# Replace <run-def-id> with the ID output by the previous command
$ USimCLI/mac/usim execute run <run-def-id>
    # Outputs Run Execution ID

# Simulation Status
$ USimCLI/mac/usim summarize run-execution <exec-id>
$ USimCLI/mac/usim describe run-execution <exec-id>

# After Simulation shows a 'Complete' status
$ USimCLI/mac/usim download manifest <exec-id> --save-in=RunExecutionData
```
Ubuntu (replace `ubuntu18.04` with `ubuntu16.04` as needed)

```shell
$ USimCLI/ubuntu18.04/usim login auth
$ USimCLI/ubuntu18.04/usim activate project

$ USimCLI/ubuntu18.04/usim upload build ./RollaballLinuxBuild/rollaball_linux_build.zip
    # Outputs  Build ID

$ USimCLI/ubuntu18.04/usim upload app-param ./AppParams/app-param.json
    #  Outputs AppParam ID

$ USimCLI/ubuntu18.04/usim define run
    # Outputs Run Definition ID

# Replace <run-def-id> with the ID output by the previous command
$ USimCLI/ubuntu18.04/usim execute run <run-def-id>
    # Outputs Run Execution ID

# Simulation Status
$ USimCLI/ubuntu18.04/usim summarize run-execution <exec-id>
$ USimCLI/ubuntu18.04/usim describe run-execution <exec-id>

# After Simulation shows a 'Complete' status
$ USimCLI/ubuntu18.04/usim download manifest <exec-id> --save-in=RunExecutionData
```

Windows

```posh
> USimCLI\windows\usim.exe login auth
> USimCLI\windows\usim.exe activate project

> USimCLI\windows\usim.exe upload build RollaballLinuxBuild\rollaball_linux_build.zip
    # Outputs  Build ID

> USimCLI\windows\usim.exe upload app-param AppParams\app-param.json
    #  Outputs AppParam ID

> USimCLI\windows\usim.exe define run
    # Outputs Run Definition ID

> USimCLI\windows\usim.exe execute run <run-def-id>
    # Outputs Run Execution ID

# Simulation Status
> USimCLI\windows\usim.exe summarize run-execution <exec-id>
> USimCLI\windows\usim.exe describe run-execution <exec-id> --states=in-progress,failed

# After Simulation shows a 'Complete' status download data
> USimCLI\windows\usim.exe download manifest <exec-id> --save-in=RunExecutionData
```

### Upload Build

A zipped Linux build of the Unity tutorial scene [RollABall](https://learn.unity.com/project/roll-a-ball-tutorial) has been included with the quick start materials.
The following commands will upload this zip to the Unity Simulation service so the scene can be executed on Unity Simulation.

macOS:

```console
$ USimCLI/mac/usim upload build RollaballLinuxBuild/rollaball_linux_build.zip
```

Windows:

```posh
> USimCLI\windows\usim.exe upload build RollaballLinuxBuild\rollaball_linux_build.zip
```

```
-- Returns

RollaballLinuxBuild/rollaball_linux_build.zip successfully uploaded with ID <build-id>
```

*NOTE*: The output will provide you with the ID for this particular build.

### Upload Application Parameter

Upload an Application Parameter file which contains values used to set variables during the execution of the RollABall simulation. For example, number of players and simulation run time are variables set from this file.

macOS:

```console
$ USimCLI/mac/usim upload app-param AppParams/app-param.json
```

Windows:

```
> USimCLI\windows\usim.exe upload app-param AppParams\app-param.json
```

```
--Returns

app-param.json successfully uploaded with ID <app-param-id>
```

The app-param that we provided will generate around 30 images in 30 seconds and then exit the RollABall application.

*NOTE*: Every sim execution must have at least one app-param associated with it. In the event that you have no need to integrate app-params an empty JSON file, a file with only {}, must be uploaded.

### Define Run

This step will launch you into a text-based wizard to define a simulation run. This definition will contain the build id, app-param id(s), system parameters to execute the build with, and number of instances the simulation should execute with each app-param.

macOS:

```console
$ USimCLI/mac/usim define run
```

Windows:

```
>  USimCLI\windows\usim.exe define run
```

Your input should be similar to the following responses:

```console
$ USimCLI/mac/usim define run
This command will walk you through defining your run.

Name: my-new-simulation

Description: This is the quick start rundef

Choose build:
choice   name                        id       creation time
-------- --------------------------- -------- --------------------------
1        rollaball_linux_build.zip   awade0   2019-05-06T18:29:52.595Z
Enter choice number: 1
```

Enter the number corresponding to the build uploaded in the previous step. If this is your first time uploading a build it should be the only choice available.

```console
Choose sys-params:
 choice   id                                   description
-------- ------------------------------------ -------------------------------------------
 1        gcp@cpu:6                            gcp: 6 vCPU, 22.5 GB
 2        gcp@cpu:12                           gcp: 12 vCPU, 45 GB
 3        gcp@cpu:18                           gcp: 18 vCPU, 67.5 GB
 4        gcp@cpu:24                           gcp: 24 vCPU, 90 GB
 5        gcp@cpu:30                           gcp: 30 vCPU, 112.5 GB
Enter choice number: 1
```

**Note**: You may see additional sys-params, please choose only from the above list.

Next enter the number corresponding to the sys-param you would like to use.

```console
Select an app-param to include in this run:
choice   name             id       creation time
-------- ---------------- -------- --------------------------
1        app-param.json   Oe8GY9   2019-05-06T18:30:00.452Z
Enter choice number: 1
Number of instances to run with app-param.json: 2
app-param.json:2 added to run definition.

Would you like to include more app_params (Y/n)? n
```

Since we have only uploaded one app-param we will select the only app-param listed. We also want to run two executions of RollABall so we will enter `2` as number of instances. At this point if we had wanted to run additional instances with different app-params we would choose `y` when prompted to include more app_params, for this tutorial we choose `n` indicating we have added all app-params for this run-definition. This step will repeat so that builds can be run with different app-param files each with a particular number of instances.

*NOTE*: If the same app-param file is selected multiple times only the last defined number of instances will be used.

macOS and Ubuntu:

```console
Directory name where this run definition (my-new-simulation.json) will be saved: ./RunDefinitions/
Run definition saved as ./RunDefinitions/my-new-simulation.json
```

Windows:

```posh
Directory name where this run definition (my-new-simulation.json) will be saved: .\RunDefinitions\
Run definition saved as .\RunDefinitions\my-new-simulation.json
```

Enter a valid directory to save the run definition as a JSON file on the local filesystem in the directory specified.

macOS and Ubuntu:

```console
Would you like to upload the run definition now? (Y/n) y
./RunDefinitions/my-new-simulation.json successfully uploaded with ID <run-definition-id>
```

Windows:

```posh

Would you like to upload the run definition now? (Y/n) y
.\RunDefinitions\my-new-simulation.json successfully uploaded with ID <run-definition-id>
```

Finally there will be a prompt asking to upload the run-definition we just created. Enter `y` and take note of the returned run definition ID.

### Upload Run Definition

In the event that you did not upload the run definition in the previous step or you need to upload a run definition that already exist on the filesystem you can do so by entering the following command.

macOS and Ubuntu:

```console
$ USimCLI/mac/usim upload run RunDefinitions/my-new-simulation.json
```

Windows:

```posh
$ USimCLI\windows\usim.exe upload run RunDefinitions\my-new-simulation.json
```

```
-- Returns
quickstart-rundef.json successfully uploaded with ID <run-def-id>
```

This will output the run definition ID that you will need in the next step.

### Execute Run

In this step you will submit the run definition uploaded in the previous step to receive an execution id of the submitted run definition.

Replace <run-def-id> with the run ID returned in the previous step for the following commands.

macOS:

```console
$ USimCLI/mac/usim execute run <run-def-id>
```

```console
$ USimCLI/mac-legacy/usim execute run <run-def-id>
```

Ubuntu:

```console
$ USimCLI/ubuntu18.04/usim execute run <run-def-id>
```
```console
$ USimCLI/ubuntu16.04/usim execute run <run-def-id>
```

Windows:

```
> USimCLI\windows\usim.exe execute run <run-def-id>
```

```
-- Returns
Run has been scheduled with execution ID <exec-id>
```

This will output the run execution ID that you can use to [inspect the run-execution](#check-run-status) and [download data generated](#download-data) by the run execution.

### Check Run Status

This run execution will take around seven to ten minutes to complete.

To periodically check on the run execution status the following two commands can be used with your run execution ID.

#### Summarize Run Execution

macOS and Ubuntu:

```console
$ USimCLI/mac/usim summarize run-execution <exec-id>
```

Windows:

```posh
> USimCLI\windows\usim.exe summarize run-execution <exec-id>
```

```
-- Returns
Execution status: In_Progress (SchedulerService)
 state         count
------------- -------
 Successes     0
 In Progress   2
 Failures      0
 Not Run       0
```

The first line describes the overall status and stage of the run-execution. It will change on subsequent queries as the run-execution progresses to a `Completed` state. The table beginning on the second line describes the state and count of the run-instances requested across all app-params of this run-definition.

Successes - Run-instances that have successfully exited. \
In Progress - Run-instances that are currently executing. \
Failures - Run-instances that have failed and completed executing. \
Not Run - Run-instances that were not executed when when the run-execution completed.

Once you see counts greater than zero for an instance state in the summary table, you may want to see a more detailed description of your instances. For this, use the `usim describe run-execution <exec-id>` command. You may also begin to inspect the logs of a running simulation instance with the `usim logs` command. See the [CLI logs](cli.md#usim-logs) command for more information.

**Note:** When the summary counts are all zero you will receive a message indicating that there is no instance-level status information to report. (example: `0 instances matching this instance state filter`)

#### Describe Run Execution

macOS and Ubuntu:

```console
$ USimCLI/mac/usim describe run-execution <exec-id> --states=in-progress,failed
```

Windows:

```posh
> USimCLI\windows\usim.exe describe run-execution <exec-id> --states=in-progress,failed
```

**Note:** Permitted states are comma separated values from [all,ok,failed,not-run,pending,in-progress] (See `usim -h`)

```
-- Returns
 Instance #   App Param ID    Attempt #   Start Time            Duration (ms)   State        Message
------------ --------------- ----------- --------------------- --------------- ------------ ---------
 1            <app-param-id>  1           2019-08-15 19:56:30   None            InProgress
```

**NOTE:** Duration will only be updated after completion and is a rough estimate of the instance's run duration with approximately 30-second precision.

After the summary status is `Completed` you can proceed to the [Download Data](#download-data) section. An example of a `Completed` status is below.

```
Execution status: Completed (SchedulerService)
 state         count
------------- -------
 Successes     2
 In Progress   0
 Failures      0
 Not Run       0
```

### Download Data

Data generated by the simulation will be available for download for 90 days. After that period, it will be automatically deleted.

This command will not actually download the data directly and instead it will download a manifest file in CSV format with signed URLs to the data that this run has produced; such as images and logs. You will need to provide the path to the directory where you want the CLI to save the manifest file. The signed URL's in the `download_url` column of the manifest file will only be valid for a limited time, after which you will need to generate a new manifest. You may specify the duration of this period by passing the --expires=<seconds> flag. The maximum duration is one week (604,800 seconds).

macOS and Ubuntu:

```console
$ USimCLI/mac/usim download manifest <exec-id> --save-in=RunExecutionData
```

Windows:

```posh
> USimCLI\windows\usim.exe download manifest <exec-id> --save-in=RunExecutionData
```

Now the `unity_simulation_bundle/RunExecutionData` directory should have a single CSV file with approximately 30 lines where each line contains a signed url to the generated image. Additionally, there will be a line which contains the output of the Player log generated during the run execution.

Please reference the [Anatomy of a Manifest](taxonomy.md#anatomy-of-a-manifest) guide for more information on the layout and types of files that could be present within a manifest.