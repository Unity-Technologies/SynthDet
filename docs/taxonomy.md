# Taxonomy

This document provides an overview of the core concepts used in Unity Simulation.

## Authentication

The Unity Simulation service uses your Unity Account to determine your identity and provide access to your Unity projects. Executing the [login command](cli.md#Authentication-Commands) initiates an OAuth flow that begins by opening your web browser to [https://id.unity.com/](https://id.unity.com/). After logging in using your Unity account credentials you will be redirected to a page indicating that authentication was successful. If you are already logged in, your logged-in account will be used to authenticate and you will be redirected immediately to the authentication success page. This flow will  create a token that will be stored locally on your machine for future interaction with the Unity Simulation API. Be sure to protect this token as it could be used maliciously to impersonate API calls against your Unity projects.

## Projects

The Unity Simulation service operates against a single Unity project at a time. You must have a Unity project created and associated with your Unity account use the Unity Simulation service.

## Data Retention

Users running simulations with Unity Simulation will keep two types of data on the cloud:
1. Parameter files (e.g. app-param, builds, etc)
2. Generated data (Unity log, profiling information, generated images)

Both types of data currently have a 90 days retention policy.

## Unity Build
A Linux build of a Unity project that has been integrated with the Unity Simulation SDK.

A build can be created from the Unity Editor by following the steps detailed [here](build.md)

## Simulation
One or more scenes developed within the Unity Editor that are assembled into a Unity Build

## Application Parameter (app-param)
A user defined JSON file defining parameter values exposed to the executing Unity Simulation through SDK integration.

As an example, an app-param where the total simulation execution time allowed, the cadence at which screen captures are taken, and the scenario being executed are variables controlled outside of the simulation might look like the following.

```json
{
    "quitAfterSeconds": 60,
    "screenCaptureInterval": 1,
    "scenario": "intersection"
}
```

## System Parameter (sys-param)

A specification defining the computation resources available to each Run Instance where the Unity Simulation is executed. Valid values are listed below.
```console
 id                                   description
------------------------------------ -------------------------------------------
gcp@cpu:6                            gcp: 6 vCPU, 22.5 GB
gcp@cpu:12                           gcp: 12 vCPU, 45 GB
gcp@cpu:18                           gcp: 18 vCPU, 67.5 GB
gcp@cpu:24                           gcp: 24 vCPU, 90 GB
gcp@cpu:30                           gcp: 30 vCPU, 112.5 GB
```
The id values are used when creating a Run Definition as the value of the `sys_param_id` key.

## Run Definition (run-def)

A JSON file defining a particular sys-param ID, one or more app-param IDs with the number of times that the simulation should be ran with that particular app-param, and a single build ID.

**NOTE:** A simulation will be executed the same number of times as the sum of all `num_instances` values for all app-params in a run-definition. The example below will have a simulation that executes a total of 9 times. Once with `APP_PARAM_ID_0` and eight times with `APP_PARAM_ID_1`.

```json
{
    "name": "simulation_name",
    "description": "Description of simulation name.",
    "app_params": [
        {
            "id": "APP_PARAM_ID_0",
            "num_instances": 1
        },
        {
            "id": "APP_PARAM_ID_1",
            "num_instances": 8
        }
    ],
    "sys_param_id": "SYS_PARAM_ID",
    "build_id": "BUILD_ID"
}
```

## Run Execution (run-exec)
An execution of a particular Run Definition on the Unity Simulation service.

## Run Instance
A single machine that can execute one to many simulations each associated with a single app-param.

## Simulation Instance
A single simulation executing on a Run Instance.

## Anatomy of a Manifest

Columns:
- `run_execution_id` - The run execution ID that generated the data available on this line.
- `app_param_id` - The app-param used during the simulation that generated the data available on this line.
- `instance_id` - The unique simulation instance ID that generated the data on this line.
- `attempt_id` - Each simulation will retry a number of times in the event of a simulation failure. This ID denotes which attempted execution generated the data.
- `file_name` -  Name of the file to be downloaded.
- `download_uri` - The url used to download the file referenced by file_name.

Files and file types available on each line:
- Images saved as JPG, TGA, or RAW.
- `profilerLog.txt.raw` is raw binary file that can only be opened in the Unity Editor using the [Unity Profiler](https://docs.unity3d.com/Manual/Profiler.html).
- `Player.Log` is a log file generated from each simulation execution.
- `.json` files contain serialized class data written to file using the Unity Simulation DataLogger API.
