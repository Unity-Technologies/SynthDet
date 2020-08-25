# Dataset Evaluation

This guide shows you how to evaluate the value/quality of a synthetic dataset by training a [Faster-RCNN](https://arxiv.org/abs/1506.01497) object detection model on it and testing the performance of that model on a well-known held out dataset of real images

## Part 1: Datasets

TODO: finalize name for datasets (i.e. real, synthetic large and small) and make sure the name is consistently used throughout the guide and blog post. We use placeholders `UnityGroceries-Synthetic` and `UnityGroceries-RealWorld` in this document.

### UnityGroceries-Synthetic dataset

We've made a small sample from UnityGroceries-Synthetic dataset of 5k images generated using the [SynthDet](https://github.com/Unity-Technologies/SynthDet) Unity environment. To train a model on this dataset, you can skip directly to [Part 2](#part-2-training-a-model) of this guide where you'll use a pre-compiled kubeflow pipeline that is already configured to fetch and then train on this sample dataset.

A larger dataset of 400k we used in our experiments can be made available [upon request](https://forms.gle/2BmZYQJmziq3ipK88).

### UnityGroceries-RealWorld dataset

We've also made a new **[dataset of 1.5k real images](add links here)** which contain groceries and corresponding bounding boxes. You can look at it if you wish, or simply [skip ahead](#part-2-training-a-model) if you're interested in training a model on this dataset.

### Create a new synthetic dataset using Unity Simulation (optional)

If you want to run a the full end-to-end pipeline including synthetic dataset generation you can follow [this guide](https://github.com/Unity-Technologies/SynthDet/blob/master/docs/RunningSynthDetCloud.md) and then continue to run [this training pipeline](#train-on-synthetic-dataset-generated-on-unity-simulation-optional).

## Part 2: Training a model

Once you know which dataset you want to train on, you can follow the instruction below to train a model on it.

Note that these instructions focus on the recommended containerized approach to run a training job on a [Kubeflow](https://www.kubeflow.org/docs/gke/gcp-e2e) cluster on Google Kubernetes Engine ([GKE](https://cloud.google.com/kubernetes-engine)). We do this to avoid reproducibility issues people may encounter on different platforms with different dependencies etc. You can use our [docker image](https://hub.docker.com/r/unitytechnologies/datasetinsights) if you're interested in running the same container on your own infrastructure; otherwise you can run one of the pre-compiled Kubeflow Pipelines documented below.

### Train on UnityGroceries-Synthetic dataset

This section shows you how to train a model on the sample synthetic dataset. Note that this is a small dataset which is the fastest to train but won't produce the best results; for that, you can train a model that uses a larger synthetic dataset and [fine tunes the model on real images](#train-on-synthetic-and-real-world-dataset-optional). To observe the best results we have obtained, you can follow the instructions to run one of our [pre-trained models](#using-our-pre-trained-models) below.

To train the model, simply [import](https://www.kubeflow.org/docs/pipelines/pipelines-quickstart/#deploy-kubeflow-and-open-the-pipelines-ui) the pre-compiled [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/train_on_synthdet_sample.yaml) into your kubeflow cluster. The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)

Once your pipeline has been imported, you can run it vis the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![train on SynthDet sample](images/kubeflow/train_on_synthdet_sample.png)

You have to specify run parameters required by this pipeline:

- `docker`: Path to a Docker Registry. We suggest changing this parameter to pull our images on Docker Hub with a specific tag, such as `unitytechnologies/datasetinsights:0.2.0`
- `source_uri`: The dataset source uri. You can use the default value which points to the required dataset for this pipeline.
- `config`: Estimator config YAML file. You can use the default value which points to a YAML file packaged with our docker images.
- `tb_log_dir`: Path to store tensorboard logs used to visualize the training progress.
- `checkpoint_dir`: Path to store output Estimator checkpoints. You can use one of the checkpoints for estimator evaluation.
- `volume_size`: Size of the Kubernetes Persistent Volume Claims (PVC) that will be used to store the dataset. You can use the default value.

> You'll want to change `tb_log_dir`, `checkpoint_dir` to point to a location that is convenient for you and your Kubernetes cluster have permissions to write to. This is typically a GCS path under the same GCP project. Note that an invalid location will cause the job to fail, whereas a path to the local filesystem may run but will be hard to monitor as you won't have easy access to these files inside a docker container.

![pipeline graph](images/kubeflow/pipeline_graph.png)

To open tensorboard for training visualization, you can run the following command:

```bash
docker run -p 6006:6006 -v $HOME/.config:/root/.config:ro tensorflow/tensorflow tensorboard --host=0.0.0.0 --logdir=gs://<tb_log_dir>
```

This assumes you have already [logged in](https://cloud.google.com/sdk/gcloud/reference/auth/login) to GCP using a service account stored in the default location, such as `$HOME/.config`. This service account should have permissions to read `tb_log_dir` to download tensorboard files.

Next, follow the [instructions](#part-3-evaluate-a-model) to evaluate evaluate the performance of this model by running one more pipeline we have prepared. You'll need the location of your model in the next step.

### Train on UnityGroceries-RealWorld dataset (optional)

This section shows you how to train a model on the UnityGroceries-RealWorld dataset. Note that this won't produce the best results; for that, you can train a model that uses a larger synthetic dataset and [fine tunes the model on real images](#train-on-synthetic-and-real-world-dataset-optional). To observe the best results we have obtained, you can follow the instructions to run one of our [pre-trained models](#using-our-pre-trained-models) below.

To train the model, simply [import](https://www.kubeflow.org/docs/pipelines/pipelines-quickstart/#deploy-kubeflow-and-open-the-pipelines-ui) the pre-compiled [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/train_on_real_world_dataset.yaml). The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)

Once your pipeline has been imported, you can run it vis the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![train on real world daataset](images/kubeflow/train_on_real_world_dataset.png)

You have to specify run parameters required by this pipeline:

- `docker`: Path to a Docker Registry. We suggest changing this parameter to pull our images on Docker Hub with a specific tag, such as `unitytechnologies/datasetinsights:0.2.0`
- `source_uri`: The dataset source uri. You can use the default value which points to the required dataset for this pipeline.
- `config`: Estimator config YAML file. You can use the default value which points to a YAML file packaged with our docker images.
- `tb_log_dir`: Path to store tensorboard logs used to visualize the training progress.
- `checkpoint_dir`: Path to store output Estimator checkpoints. You can use one of the checkpoints for estimator evaluation.
- `volume_size`: Size of the Kubernetes Persistent Volume Claims (PVC) that will be used to store the dataset. You can use the default value.

> You'll want to change `tb_log_dir`, `checkpoint_dir` to point to a location that is convenient for you and your Kubernetes cluster have permissions to write to. This is typically a GCS path under the same GCP project. Note that an invalid location will cause the job to fail, whereas a path to the local filesystem may run but will be hard to monitor as you won't have easy access to these files inside a docker container.

![pipeline graph](images/kubeflow/pipeline_graph.png)

To open tensorboard for training visualization, you can run the following command:

```bash
docker run -p 6006:6006 -v $HOME/.config:/root/.config:ro tensorflow/tensorflow tensorboard --host=0.0.0.0 --logdir=gs://<tb_log_dir>
```

This assumes you have already [logged in](https://cloud.google.com/sdk/gcloud/reference/auth/login) to GCP using a service account stored in the default location, such as `$HOME/.config`. This service account should have permissions to read `tb_log_dir` to download tensorboard files.

Next, follow the [instructions](#part-3-evaluate-a-model) to evaluate evaluate the performance of this model by running one more pipeline we have prepared. You'll need the location of your model in the next step.

### Train on synthetic and real-world dataset (optional)

This section shows you how to train a model on the UnityGroceries-Synthetic dataset and then fine tune that model on the UnityGroceries-RealWorld dataset. This approach generally produces the best results. In this particular case, however, you'll be using a sample dataset to run the full pipeline more quickly. To observe the best results we have obtained, you can follow the instructions to run one of our [pre-trained models](#using-our-pre-trained-models)below.

To train the model, simply [import](https://www.kubeflow.org/docs/pipelines/pipelines-quickstart/#deploy-kubeflow-and-open-the-pipelines-ui) the pre-compiled [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/train_on_synthetic_and_real_dataset.yaml). The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)

Once your pipeline has been imported, you can run it vis the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![train on synthetic and real world dataset](images/kubeflow/train_on_synthetic_and_real_world_dataset.png)

You have to specify run parameters required by this pipeline:

- `docker`: Path to a Docker Registry. We suggest changing this parameter to pull our images on Docker Hub with a specific tag, such as `unitytechnologies/datasetinsights:0.2.0`
- `source_uri`: The dataset source uri. You can use the default value which points to the required dataset for this pipeline.
- `config`: Estimator config YAML file. You can use the default value which points to a YAML file packaged with our docker images.
- `checkpoint_file`: Path to the Estimator checkpoint file from previous training runs that you want to load and resume training.
- `tb_log_dir`: Path to store tensorboard logs used to visualize the training progress.
- `checkpoint_dir`: Path to store output Estimator checkpoints. You can use one of the checkpoints for estimator evaluation.
- `volume_size`: Size of the Kubernetes Persistent Volume Claims (PVC) that will be used to store the dataset. You can use the default value.

> You'll want to change `tb_log_dir`, `checkpoint_dir` to point to a location that is convenient for you and your Kubernetes cluster have permissions to write to. This is typically a GCS path under the same GCP project. Note that an invalid location will cause the job to fail, whereas a path to the local filesystem may run but will be hard to monitor as you won't have easy access to these files inside a docker container. You'll also want to change `checkpoint_file` to point to a estimator that give you the best validation result in [previous training run](#train-on-the-synthdet-sample). This pipeline will load this model and resume training using real world dataset.

![pipeline graph](images/kubeflow/pipeline_graph.png)

To open tensorboard for training visualization, you can run the following command:

```bash
docker run -p 6006:6006 -v $HOME/.config:/root/.config:ro tensorflow/tensorflow tensorboard --host=0.0.0.0 --logdir=gs://<tb_log_dir>
```

This assumes you have already [logged in](https://cloud.google.com/sdk/gcloud/reference/auth/login) to GCP using a service account stored in the default location, such as `$HOME/.config`. This service account should have permissions to read `tb_log_dir` to download tensorboard files.

Next, follow the [instructions](#part-3-evaluate-a-model) to evaluate evaluate the performance of this model by running one more pipeline we have prepared. You'll need the location of your model in the next step.

### Train on synthetic dataset generated on Unity Simulation (optional)

This section shows you how to train a model on your own dataset generated by running the [SynthDet] environment on [Unity Simulation](https://unity.com/products/unity-simulation) at scale. You can follow [these instructions](https://github.com/Unity-Technologies/SynthDet/blob/master/docs/RunningSynthDetCloud.md) to generate the dataset if you haven't already.

To train the model, first [import](https://www.kubeflow.org/docs/pipelines/pipelines-quickstart/#deploy-kubeflow-and-open-the-pipelines-ui) the pre-compiled [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/train_on_synthetic_dataset_unity_simulation.yaml). The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)

Once your pipeline has been imported, you can run it via the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![train on synthetic dataset unity simulation](images/kubeflow/train_on_synthetic_dataset_unity_simulation.png)

You have to specify run parameters required by this pipeline:

- `docker`: Path to a Docker Registry. We suggest changing this parameter to pull our images on Docker Hub with a specific tag, such as `unitytechnologies/datasetinsights:0.2.0`
- `project_id`: A Unity [project ID](https://docs.unity3d.com/Manual/SettingUpProjectServices.html). Example format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.
- `run_execution_id`: A 7-character Run Definition ID used by Unity Simulation. See Unity Simulation [documentation](https://github.com/Unity-Technologies/Unity-Simulation-Docs/blob/master/doc/cli.md#argument-descriptions).
- `access_token`: Unity Simulation access token. Please see the official Unity Simulation [doc](https://github.com/Unity-Technologies/Unity-Simulation-Docs/blob/master/doc/cli.md#usim-inspect-auth) to obtain a valid access token.
- `config`: Estimator config YAML file. You can use the default value which points to a YAML file packaged with our docker images.
- `tb_log_dir`: Path to store tensorboard logs used to visualize the training progress.
- `checkpoint_dir`: Path to store output Estimator checkpoints. You can use one of the checkpoints for estimator evaluation.
- `volume_size`: Size of the Kubernetes Persistent Volume Claims (PVC) that will be used to store the dataset. You should change this value according to the dataset that was generated. If you use default settings from [these instructions](https://github.com/Unity-Technologies/SynthDet/blob/master/docs/RunningSynthDetCloud.md), you should expect `1.2TiB` storage required for 400k images.

> You'll want to change `tb_log_dir`, `checkpoint_dir` to point to a location that is convenient for you and your Kubernetes cluster have permissions to write to. This is typically a GCS path under the same GCP project. Note that an invalid location will cause the job to fail, whereas a path to the local filesystem may run but will be hard to monitor as you won't have easy access to these files inside a docker container.

![pipeline graph](images/kubeflow/pipeline_graph.png)

To open tensorboard for training visualization, you can run the following command:

```bash
docker run -p 6006:6006 -v $HOME/.config:/root/.config:ro tensorflow/tensorflow tensorboard --host=0.0.0.0 --logdir=gs://<tb_log_dir>
```

This assumes you have already [logged in](https://cloud.google.com/sdk/gcloud/reference/auth/login) to GCP using a service account stored in the default location, such as `$HOME/.config`. This service account should have permissions to read `tb_log_dir` to download tensorboard files.

Next, follow the [instructions](#part-3-evaluate-a-model) to evaluate evaluate the performance of this model by running one more pipeline we have prepared. You'll need the location of your model in the next step.

## Part 3: Evaluate a model

In this section, we'll a trained model to generate predictions on a held out test set of real images and measure its performance using well-known performance metrics like [mAP](https://datasetinsights.readthedocs.io/en/latest/datasetinsights.evaluation_metrics.html#datasetinsights.evaluation_metrics.average_precision_2d.MeanAveragePrecisionAverageOverIOU) and [mAR](https://datasetinsights.readthedocs.io/en/latest/datasetinsights.evaluation_metrics.html#datasetinsights.evaluation_metrics.average_recall_2d.MeanAverageRecallAverageOverIOU). We have prepared another kubeflow [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/evaluate_the_model.yaml) for this.

Simply import the [pre-compiled pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/evaluate_the_model.yaml) into your kubeflow cluster. The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)

Once your pipeline has been imported, you can run it vis the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![evaluate the model](images/kubeflow/evaluate_the_model.png)

Whether you trained a model on synthetic, real or multiple datasets, you'll need to specify a model stored in the `checkpoint_dir` from previous pipelines.

You have to specify run parameters required by this pipeline:

- `docker`: Path to a Docker Registry. We suggest changing this parameter to pull our images on Docker Hub with a specific tag, such as `unitytechnologies/datasetinsights:0.2.0`
- `source_uri`: The dataset source uri. You can use the default value which points to the required dataset for this pipeline.
- `config`: Estimator config YAML file. You can use the default value which points to a YAML file packaged with our docker images.
- `checkpoint_file`: Path to the Estimator checkpoint file from previous training runs that you want to load for evaluation.
- `tb_log_dir`: Path to store tensorboard logs used to visualize the training progress.
- `volume_size`: Size of the Kubernetes Persistent Volume Claims (PVC) that will be used to store the dataset. You can use the default value.

Just like for the training pipeline, you'll want to change `tb_log_dir` to point to a location that is convenient for you and your have permission to write to. This is where you'll read the logs and see the **performance metrics** once the pipeline completes.

In addition to the logs, the performance metrics are also available in a Jupyter Notebook we have prepared that includes code to visualize the predictions.

### visualizing predictions and performance

We recommend running our [docker image](https://hub.docker.com/r/unitytechnologies/datasetinsights) which includes Jupyter as well as our notebooks if you don't want to setup the environment on your own. You can follow the same [instructions](https://github.com/Unity-Technologies/SynthDet/blob/master/docs/RunningSynthDetCloud.md#step-6-run-dataset-statistics-using-the-datasetinsights-jupyter-notebook) you may have used to run our [statistics notebook](https://github.com/Unity-Technologies/datasetinsights/blob/master/notebooks/SynthDet_Statistics.ipynb), but select the newer [visual inspection notebook](https://github.com/Unity-Technologies/datasetinsights/blob/master/notebooks/SynthDet_Evaluation.ipynb) called `SynthDet_Evaluation.ipynb` instead.

### Using our pre-trained models

We trained a model using `~400k` synthetic examples and then fine tuned it using `~700` real images. You can use the same [visual inspection notebook](https://github.com/Unity-Technologies/datasetinsights/blob/master/notebooks/SynthDet_Evaluation.ipynb) mentioned above, but use one of our models from the list below:

- [Real World (760)](https://storage.googleapis.com/datasetinsights/models/Real-World/FasterRCNN.estimator)
- [Synthetic (400,000)](https://storage.googleapis.com/datasetinsights/models/Synthetic/FasterRCNN.estimator)
- [Synthetic (400,000) + Real World (76)](https://storage.googleapis.com/datasetinsights/models/Synthetic-And-Real-World-76-images/FasterRCNN.estimator)
- [Synthetic (400,000) + Real World (380)](https://storage.googleapis.com/datasetinsights/models/Synthetic-And-Real-World-380-images/FasterRCNN.estimator)
- [Synthetic (400,000) + Real World (760)](https://storage.googleapis.com/datasetinsights/models/Synthetic-And-Real-World-760-images/FasterRCNN.estimator)
