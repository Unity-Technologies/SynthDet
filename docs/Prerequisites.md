# Prerequisites

## Essentials

1. Download and install the latest version of **[Unity 2020.3](https://unity3d.com/get-unity/download)**. When prompted during installation, make sure the **Linux Build Support (Mono)** module is checked.   
2. Use the commands below to clone the SynthDet repository and initialize the LFS (large file storage) submodules:
```
git lfs install
git clone https://github.com/Unity-Technologies/SynthDet
cd SynthDet
git submodule update --init --recursive
```
>The submodule in this repository uses HTTPS authentication. If the commands prompts you for a username and password, create a [personal authentication token](https://help.github.com/en/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line).

The SynthDet repository uses Git LFS for many of the files in the project. To verify that LFS is properly initialized, you can check the behavior of these files in Unity Editor. In the sample project, open the **SynthDet** Scene and enter Play mode. 

* If LFS is correctly initialized, the **SynthDet** Scene should run as normal. 
* If LFS is not correctly initialized, a message saying "Display 1 No Cameras Rendering" appears. 

If LFS is not correctly initialized, return to the command line and enter the following commands: 

```bash
git lfs install
git lfs pull
```

This should re-initialize LFS.

## Additional Requirements

### For using Dataset Insights
In order to use the provided SynthDet Statistics Jupyter notebook to visualize statistics for your datasets, you will need to install Conda. We recommend [Miniconda](https://docs.conda.io/en/latest/miniconda.html).


### For running in Unity Simulation

#### Unity account
You need a Unity account in order to submit simulations to the Unity Simulation service. If you do not have a Unity account, [sign up](https://id.unity.com) for a free account.

#### Unity Simulation account and command line interface
Follow these instructions to gain access to Unity Simulation and set up its command line interface:
1. Sign up for access on the [Unity Simulation](https://unity.com/products/simulation) page. 
2. Download the latest version of `unity_simulation_bundle.zip` from [here](https://github.com/Unity-Technologies/Unity-Simulation-Docs/releases).

> :information_source: If you are using a MacOS computer, we recommend using the _**curl**_ command from the Terminal to download the file, in order to avoid issues caused by the MacOS Gatekeeper when using the CLI. You can use these commands:
```
curl -Lo ~/Downloads/unity_simulation_bundle.zip <URL-unity_simulation_bundle.zip>
unzip ~/Downloads/unity_simulation_bundle.zip -d ~/Downloads/unity_simulation_bundle
```
The `<URL-unity_simulation_bundle.zip>` address can be found at the same page linked above.

3. Extract the zip archive you downloaded.

#### Linux build support

To use Unity Simulation, your Unity installation needs to have Linux Build Support. If you did not check the related option when installing Unity, you can add the modules now. 
1. In Unity Hub, go the the ***Installs*** tab.
2. Click on the options menu icon (...) for your desired Unity version, and then click ***Add Modules***.
3. If the Linux Build Support (Mono) option is already checked, it means you have already installed this module. If not, check the option and click ***Done***.

<p align="center">
    <img src="images/linux_build_support.png" width = "800"/>
</p>