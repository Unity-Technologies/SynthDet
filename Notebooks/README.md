SynthDet Statistics Jupyter Notebook
====================================

This example notebook demonstrates how to use the [Dataset Insights](https://github.com/Unity-Technologies/datasetinsights) Python package to load and analyze synthetic datasets generated with the SynthDet project, which utilizes the Unity [Perception package](https://github.com/Unity-Technologies/com.unity.perception). this notebook includes statistics and visualizations for Labelers included in the Perception package, as well as additional metrics that are specific to SynthDet.

## Instructions

* Install Conda if not already installed. We recommend [Miniconda](https://docs.conda.io/en/latest/miniconda.html).

* Once Conda is installed: 
  * On Mac OS, open a new terminal window.
  * On Windows, you will need to open either Anaconda Prompt or Anaconda Powershell Prompt. These can be found in the Start menu.

* Create a virtual environment using Conda, and name it `synthdet_env`:

```bash
conda create -n synthdet_env python=3.7
conda activate synthdet_env
```

* Install Dataset Insights:

```bash
pip install datasetinsights
```

* Install Jupyter:

```bash
conda install jupyter
```

* You now need to create a kernel for your virtual environment, and add your virtual environment to Jupyter. Install the IPython kernel:

```bash
conda install -c anaconda ipykernel
python -m ipykernel install --user --name=synthdet_env
```

* Navigate to the folder where the `SynthDet_Statistics.ipynb` notebook file is located.
  
* You now can open the notebook using the command:

```bash
jupyter notebook
```

* Once you are in the notebook. Make sure that the kernel is set to `synthdet_env`. (Kernel -> Change kernel)
  * The notebook may ask you to set this environment when you first open it.

* Follow the instructions in the notebook to run the code cells and visualize statistics for your SynthDet dataset.
