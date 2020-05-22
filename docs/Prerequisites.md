# Prerequisites
The following setup is required for running SynthDet and running dataset statistics:

1. Download and install [Unity 2019.3](https://unity3d.com/get-unity/download)
2. Clone the [SynthDet](https://github.com/Unity-Technologies/SynthDet) repository and initialize submodules. Follow these steps to ensure a fully initialized clone:
```
git lfs install
git clone https://github.com/Unity-Technologies/SynthDet
cd SynthDet
git submodule update --init --recursive
```
>The submodule used in this repository is configured using https authentication. If you are prompted for a username and password in your shell, you will need to supply a [personal authentication token](https://help.github.com/en/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line).

>This repository uses Git LFS for many of the files in the project. If a message saying "Display 1 No Cameras Rendering" shows up when in Play mode in MainScene, ensure LFS is properly initialized by running `git lfs install` followed by `git lfs pull`.
3. Install [Docker Desktop](https://www.docker.com/products/docker-desktop) for running `datasetinsights` jupyter notebooks locally

## Additional requirements for running in Unity Simulation
- Unity Account
- Unity Simulation beta access and CLI
- Unity Cloud Project
- Linux build support
- Supported platforms: Windows10+ and OS X 10.13+

### Unity Account
A Unity account is required to submit simulations to the Unity Simulation service. If you do not already have a Unity account please follow this [link](https://id.unity.com) to sign up for a free account

### Unity Simulation account and CLI
To use Unity Simulation, sign up for access on the [Unity Simulation](https://unity.com/products/simulation) page. Once you have access, download the [Unity Simulation CLI](https://github.com/Unity-Technologies/Unity-Simulation-Docs/releases) and follow the instructions to set it up.

### Unity Cloud Project
A Unity Cloud Project Id is required to use Unity Simulation. Please reference the [Setting up your project for Unity Services](https://docs.unity3d.com/Manual/SettingUpProjectServices.html) for instructions on how to enable Unity Cloud Services for a project and how to find the Cloud Project Id.

### Linux build support

Unity Simulation requires Linux build support installed in the Unity Editor.

To install Linux build support open Unity Hub and navigate to the Installs tab.

![Linux build support](images/req-2.png "Linux build support")

Click on the options menu icon, (...) to the right of your project’s Unity version, and select Add Component.

![Linux build support component](images/req-3.png "Linux build support component")

Ensure that Linux Build Support is checked in the components menu and click done to begin the install.

![Linux build support fna](images/req-4.png "Linux build support component")