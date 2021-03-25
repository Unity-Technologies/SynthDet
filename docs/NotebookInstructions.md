SynthDet Statistics Jupyter Notebook
====================================

This example notebook demonstrates how to use the [Dataset Insights](https://github.com/Unity-Technologies/datasetinsights) Python package to load and analyze synthetic datasets generated with the SynthDet project, which utilizes the Unity [Perception package](https://github.com/Unity-Technologies/com.unity.perception). This notebook includes statistics and visualizations for Labelers (ground-truth generators) included in the Perception package, as well as additional metrics that are specific to SynthDet.

## Workflow

* Make sure [Docker Desktop](https://www.docker.com/products/docker-desktop) is installed.

* Open a command line and use the following command to download and run the Dataset Insights Docker image, and mount your local folders to it:

```
docker run -p 8888:8888 -v <dataset_path>:/data -v <synthdet_notebook_path>:/tmp -t unitytechnologies/datasetinsights:latest
```

In the above command, `<dataset_path>` is the path to the top level folder of the dataset you generated using SynthDet, and `<synthdet_notebook_path>` is the location at which the `SynthDet_Statistics.ipynb` notebook file is located inside your SynthDet repository (`repository_root/Notebooks`). 

You can copy the dataset path to clipboard using the _**Copy Path**_ button in the `Perception Camera` UI in the SynthDet Unity project.

An example dataset path on OSX is:
```
/Users/username/Library/Application\ Support/UnityTechnologies/SynthDet/f3763556-355f-4303-9acd-32334fda51aa
```

> :information_source: If using a Linux/Unix based OS, make sure the spaces in both paths are escaped with backslashes, as shown in the above example.

> :information_source: If you get an error about the format of the command, try the command again **with quotation marks** around the folder mapping arguments, i.e. `"<dataset_path>:/data"`.


* You will now see a file explorer in Jupyter:
<p align="center">
<img src="images/jupyter.PNG"/>
</p> 

* Navigate to the `tmp` folder and click `SynthDet_Statistics.ipynb` to open the notebook.

* Follow the instructions in the notebook to run the code cells and visualize statistics for your SynthDet dataset.

**[Continue to instructions for running SynthDet on Unity Simulation](RunningSynthDetCloud.md)**