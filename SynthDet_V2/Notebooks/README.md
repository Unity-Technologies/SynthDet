Notebook Instruction
======================

This example notebook shows how to use datasetinsights to load synthetic datasets generated from the [SythDet](https://github.com/Unity-Technologies/SynthDet) and visualize dataset statistics. It includes statistics and visualizations of the outputs built into the SynthDet and should give a good idea of how to use datasetinsights to visualize custom annotations and metrics.

Prerequisites
------------

* Create a virtual environment using conda.

```bash
conda create -n yourenvname python=3.7
conda activate yourenvname
```

* Install `datasetinsights` package.

This notebook requires the `datasetinsights` package. You can install `datasetinsights` using the following command.

```bash
pip install datasetinsights
```

* Install Jupyter notebook.

Install Jupyter Notebook in your environment.

```bash
conda install jupyter
```

* Install the IPython kernel.

This step is to create a kernel for your virtual environment, and add your virtual environment to Jupyter Notebook.

```bash
conda install -c anaconda ipykernel
python -m ipykernel install --user --name=yourenvname
```

How to use
------------

You can open this notebook using:

```bash
jupyter notebook
```

Once you are in the notebook. Make sure that the kernel is set to yourenvname. (Kernel -> Change kernel)

Following the instructions in the notebook, you can visualize statistics for the SynthDet dataset.
