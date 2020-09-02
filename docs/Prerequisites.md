# Prerequisites
To run SynthDet and dataset statistics, do the following setup: 

1. Download and install [Unity 2019.3](https://unity3d.com/get-unity/download)
2. Clone the [SynthDet repository](https://github.com/Unity-Technologies/SynthDet) and initialize the submodules. Follow these steps to ensure a fully initialized clone:
```
git lfs install
git clone https://github.com/Unity-Technologies/SynthDet
cd SynthDet
git submodule update --init --recursive
```
>The submodule used in this repository is configured using HTTPS authentication. If you are prompted for a username and password in your shell, create a [personal authentication token](https://help.github.com/en/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line).

>This repository uses Git LFS for many of the files in the project. In MainScene, in Play mode, if a message saying "Display 1 No Cameras Rendering" appears, ensure LFS is properly initialized by running `git lfs install` followed by `git lfs pull`.
3. Install [Docker Desktop](https://www.docker.com/products/docker-desktop) to run `datasetinsights` Jupyter notebooks locally

## Additional requirements for running in Unity Simulation
- Unity account
- Unity Simulation beta access and CLI
- Unity Cloud Project
- Linux build support
- Supported platforms: Windows10+ and OS X 10.13+

### Unity account
You need a Unity account in order to submit simulations to the Unity Simulation service. If you do not have a Unity account, [sign up](https://id.unity.com) for a free account.

### Unity Simulation account and CLI
To use Unity Simulation, sign up for access on the [Unity Simulation](https://unity.com/products/simulation) page. When you have access, download the [Unity Simulation CLI](https://github.com/Unity-Technologies/Unity-Simulation-Docs/releases) and follow the instructions to set it up.

### Unity Cloud Project
To use Unity Simulation, you need a Unity Cloud Project ID. For instructions on how to enable Unity Cloud Services for a project and how to find the Cloud Project ID, see [Setting up your project for Unity Services](https://docs.unity3d.com/Manual/SettingUpProjectServices.html).

### Linux build support

To use Unity Simulation, in the Unity Editor you must install Linux build support.

To install Linux build support open the Unity Hub and navigate to the Installs tab:

![Linux build support](images/req-2.png "Linux build support")

To the right of your project’s Unity version, click the options menu icon (...) and select **Add Component**:

![Linux build support component](images/req-3.png "Linux build support component")

In the components menu, enable **Linux Build Support** and click **Done** to begin the install.

![Linux build support fna](images/req-4.png "Linux build support component")