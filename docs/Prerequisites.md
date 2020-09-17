# Prerequisites
To run SynthDet and dataset statistics: 

1. Download and install [Unity 2019.3](https://unity3d.com/get-unity/download)
2. Clone the [SynthDet repository](https://github.com/Unity-Technologies/SynthDet) and initialize the LFS (large file storage) submodules:
```
git lfs install
git clone https://github.com/Unity-Technologies/SynthDet
cd SynthDet
git submodule update --init --recursive
```
>The submodule in this repository uses HTTPS authentication. If it prompts you for a username and password, create a [personal authentication token](https://help.github.com/en/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line).

3. Install [Docker Desktop](https://www.docker.com/products/docker-desktop) to run `datasetinsights` Jupyter notebooks locally

The SynthDet repository uses Git LFS for many of the files in the project. To verify that LFS is properly initialized, you can check the behavior of these files in the Unity Editor. In the sample project, open **MainScene** and enter Play mode. 

- If LFS is correctly initialized, **MainScene** should run as normal. 
- If LFS is not correctly initialized, a message saying "Display 1 No Cameras Rendering" appears. 

If LFS is not correctly initialized, return to the command line and enter the following commands: 

​	`git lfs install`
​	`	git lfs pull`

This should re-initialize LFS.

## Additional requirements for running in Unity Simulation

Unity simulation is a beta feature that is still in development. To use it, sign up for the Unity Simulation Beta. 

To run SynthDet in Unity Simulation, you need the following: 

- A Unity account
- Unity Simulation beta access and CLI
- A Unity Cloud Project
- Linux build support
- Windows10+ or later, or OS X 10.13+ or later

### Unity account
You need a Unity account in order to submit simulations to the Unity Simulation service. If you do not have a Unity account, [sign up](https://id.unity.com) for a free account.

### Unity Simulation account and CLI
To use Unity Simulation, sign up for access on the [Unity Simulation](https://unity.com/products/simulation) page. When you have access, download the [Unity Simulation CLI](https://github.com/Unity-Technologies/Unity-Simulation-Docs/releases) and follow the instructions to set it up.

### Unity Cloud Project ID
To use Unity Simulation, you need a Unity Cloud Project ID. For instructions on how to enable Unity Cloud Services for a project and how to find the Cloud Project ID, see [Setting up your project for Unity Services](https://docs.unity3d.com/Manual/SettingUpProjectServices.html).

### Linux build support

To use Unity Simulation, you must install Linux build support to the Unity Editor.

To install Linux build support open the Unity Hub and navigate to the **Installs** tab:

![Linux build support](images/req-2.png "Linux build support")

Next to your project’s Unity version, click the Options menu icon (...) and select **Add Component**:

![Linux build support component](images/req-3.png "Linux build support component")

In the components menu, enable **Linux Build Support** and select **Done** to begin the installation.

![Linux build support fna](images/req-4.png "Linux build support component")
