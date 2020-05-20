# Docker
Guide for navigating issues in docker that can block workflow in using the SynthDet Sample Project.

## Authentication
When setting up docker to push to a gcloud project you might run into an issue where you can’t push because of an authentication issue. If this happens it is most likely an issue where docker has not been configured with gcloud. You can fix this by running gcloud auth configure-docker in a cmd window going through the prompts.

[Google advanced authentication](https://cloud.google.com/container-registry/docs/advanced-authentication) for further documentation using docker with google services.

## Docker Start Error on Windows
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