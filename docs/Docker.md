# Docker
This is a guide for navigating issues in Docker that can block your workflow when using the SynthDet Sample Project.

## Authentication
When setting up Docker to push to a Google Cloud project you might not be able to push because of an authentication issue. If this happens it is most likely an issue where Docker has not been configured with Google Cloud. To fix this, in a cmd window, run  `gcloud auth configure-docker` and follow the prompts.

For more information on using Docker with Google services see [Google advanced authentication](https://cloud.google.com/container-registry/docs/advanced-authentication). 

## Docker start error on Windows
If you are running Docker on a Windows, you might experience an issue where Docker is unable to start because vmcompute can’t start. There is a bug in Docker version 2.2.0.3 on Windows OS 1909 that is causing issues with Docker. 

Example of the error:

>Docker.Core.Backend.BackendDestroyException: Unable to stop Hyper-V VM: Service 'Hyper-V Host Compute Service (vmcompute)' cannot be started due to the following error: Cannot start service vmcompute on computer '.'.

To fix the error:

1. Go to Start and open Windows Security
2. Open App & Browser control
3. At the bottom of the App & Browser control window, click **Exploit protection settings**
4. On the Exploit protection window, click the Program Settings tab
5. Find "C:\Windows\System32\vmcompute.exe" in the list and click it
6. Click **Edit** to open the Program settings: vmcomute.exe window
7. Scroll down to Control flow guard (CFG) and un-check **Override system settings**
8. Open PowerShell as an administrator
9. From the PowerShell, start vmcompute by running `net start vmcompute`
10. Start the Docker Desktop

If this method doesn’t work, in the Docker error window, select **Factory Reset**. This should keep settings intact.

If you are still facing issues with Docker there are some known Git issues with the Linux subsystem that might require you to downgrade to 2.1.0.5