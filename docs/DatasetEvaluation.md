# Dataset Evaluation

This guide shows you how to evaluate the value/quality of a synthetic dataset by training a faster-RCNN object detection model on it and testing the performance of that model on a well-known held out dataset of real images

## Part 1: Datasets

TODO: finalize name for datasets (i.e. real, synthetic large and small) and make sure the name is consistently used throughout the guide and blog post.

### Synthetic dataset sample

We've made a small sample synthetic dataset of 5k images generated using the [SynthDet](https://github.com/Unity-Technologies/SynthDet) Unity environment. To train a model on this dataset, you can skip directly to [Part 2](#part-2-training-pipeline) of this guide where you'll use a pre-compiled kubeflow pipeline that is already configured to fetch and then train on this sample dataset.

A larger dataset of 400k we used in our experiments can be made available [upon request](link to form here).

### GroceriesReal dataset

We've also made a new [dataset of 1.5k real images](add links here) which contain groceries and corresponding bounding boxes. You can look at it if you wish, or simply [skip ahead](#part-2-training-pipeline) if you're interested in training a model on this dataset.

### Create a new synthetic dataset using Unity Simulation (optional)
If you want to run a †he full end-to-end pipeline including synthetic dataset generation you can follow [this guide](https://github.com/Unity-Technologies/SynthDet/blob/master/docs/RunningSynthDetCloud.md) and then continue to run [this training pipeline]

## Part 2: Training a model

Once you know which dataset you want to train on, you can follow the instruction below to train a model on it.

Note that these instructions focus on the recommended containerized approach to run a training job on a [Kubeflow](https://www.kubeflow.org/docs/gke/gcp-e2e) cluster on Google Kubernetes Engine ([GKE](https://cloud.google.com/kubernetes-engine)). We do this to avoid reproducibility issues people may encounter on different platforms with different dependencies etc. You can use our [docker image](https://hub.docker.com/r/unitytechnologies/datasetinsights) if you're interested in running the same container on your own infrastructure; otherwise you can run one of the pre-compiled Kubeflow Pipelines documented below.

### Train on the SynthDet sample

This section shows you how to train a model on the sample synthetic dataset. Note that this is a small dataset which is the fastest to train but won't produce the best results; for that, you can train a model that uses a larger synthetic dataset and [fine tunes the model on real images]. To observe the best results we have obtained, you can follow the instructions to run one of our [pre-trained models] below.

To train the model, simply [import](https://www.kubeflow.org/docs/pipelines/pipelines-quickstart/#deploy-kubeflow-and-open-the-pipelines-ui) the pre-compiled [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/train_on_synthdet_sample.yaml) into your kubeflow cluster. The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)


Once your pipeline has been imported, you can run it vis the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

[figure shows how to run the pipeline]

TODO: talk about the output model which will need to be used in the next pipeline

You'll want to change `tb-log-dir` to point to a location that is convenient for you and your have permission to write to. You can read the logs from this location to visualize the model's progress using tensorboard. Note that an invalid location will cause the job to fail, whereas a path to the local filesystem may run but will be hard to monitor as you won't have easy access to the logs.

TODO: explain `checkpoint-dir`

Next, follow the [instructions](#part-3-evaluate-the-model) to evaluate evaluate the performance of this model by running one more pipeline we have prepared. You'll need the location of your model in the next step.

### Train on Real World Dataset (optional)

This section shows you how to train a model on the UnityRealGroceries dataset. Note that this won't produce the best results; for that, you can train a model that uses a larger synthetic dataset and [fine tunes the model on real images]. To observe the best results we have obtained, you can follow the instructions to run one of our [pre-trained models] below.

To train the model, simply [import](https://www.kubeflow.org/docs/pipelines/pipelines-quickstart/#deploy-kubeflow-and-open-the-pipelines-ui) the pre-compiled [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/train_on_real_world_dataset.yaml). The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)


Once your pipeline has been imported, you can run it vis the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

[figure shows how to run the pipeline]

TODO: talk about the output model which will need to be used in the next pipeline

You'll want to change `tb-log-dir` to point to a location that is convenient for you and your have permission to write to. You can read the logs from this location to visualize the model's progress using Tensorboard. Note that an invalid location will cause the job to fail, whereas a path to the local filesystem may run but will be hard to monitor as you won't have easy access to the logs.

TODO: explain `checkpoint-dir`

Next, follow the [instructions](#part-3-evaluate-the-model) to evaluate evaluate the performance of this model by running one more pipeline we have prepared. You'll need the location of your model in the next step.

### Train on Synthetic + Real World Dataset (optional)

This section shows you how to train a model on the UnitySyntheticGroceriesSmall dataset and then fine tune that model on the UnityRealGroceries dataset. This approach generally produces the best results. In this particular case, however, you'll be using a sample dataset to run the full pipeline more quickly. To observe the best results we have obtained, you can follow the instructions to run one of our [pre-trained models] below.

To train the model, simply [import](https://www.kubeflow.org/docs/pipelines/pipelines-quickstart/#deploy-kubeflow-and-open-the-pipelines-ui) the pre-compiled [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/train_on_synthetic_and_real_dataset.yaml). The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)


Once your pipeline has been imported, you can run it vis the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

[figure shows how to run the pipeline]

TODO: talk about the output model which will need to be used in the next pipeline

You'll want to change `tb-log-dir` to point to a location that is convenient for you and your have permission to write to. You can read the logs from this location to visualize the model's progress using Tensorboard. Note that an invalid location will cause the job to fail, whereas a path to the local filesystem may run but will be hard to monitor as you won't have easy access to the logs.

TODO: explain `checkpoint-dir` and other params specific to this pipeline

Next, follow the [instructions](#part-3-evaluate-the-model) to evaluate evaluate the performance of this model by running one more pipeline we have prepared. You'll need the location of your model in the next step.

### Train on a synthetic dataset generated on Unity Simulation (optional)

This section shows you how to train a model on your own dataset generated by running the [SynthDet] environment on [Unity Simulation] at scale. You can follow [these instructions](https://github.com/Unity-Technologies/SynthDet/blob/master/docs/RunningSynthDetCloud.md) to generate the dataset if you haven't already.

To train the model, first [import](https://www.kubeflow.org/docs/pipelines/pipelines-quickstart/#deploy-kubeflow-and-open-the-pipelines-ui) the pre-compiled [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/train_on_synthetic_dataset_unity_simulation.yaml). The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)

Once your pipeline has been imported, you can run it via the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

[figure shows how to run the pipeline]

TODO: talk about the output model which will need to be used in the next pipeline

You'll want to change `tb-log-dir` to point to a location that is convenient for you and your have permission to write to. You can read the logs from this location to visualize the model's progress using tensorboard. Note that an invalid location will cause the job to fail, whereas a path to the local filesystem may run but will be hard to monitor as you won't have easy access to the logs.

TODO: explain `checkpoint-dir` and other params specific to this pipeline
TODO: explain `auth-token` using official Unity Simulation [doc](https://github.com/Unity-Technologies/Unity-Simulation-Docs/blob/master/doc/cli.md#usim-inspect-auth).

Next, follow the [instructions](#part-3-evaluate-the-model) to evaluate evaluate the performance of this model by running one more pipeline we have prepared. You'll need the location of your model in the next step.

## Part 3: Evaluate the model

In this section, we'll a trained model to generate predictions on a held out test set of real images and measure its performance using well-known performance metrics like [mAP](link to readthedocs) and [mAR](link to readthedocs). We have prepared another kubeflow [pipeline](https://raw.githubusercontent.com/Unity-Technologies/datasetinsights/master/kubeflow/compiled/evaluate_the_model.yaml) for this.

Simply import the [pre-compiled pipeline] into your kubeflow cluster. The figure below shows how to do this using the web UI. You can optionally use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

![upload pipeline](images/kubeflow/upload_pipeline.png)


Once your pipeline has been imported, you can run it vis the web UI as shown below. Alternatively, you can use the [KFP CLI Tool](https://www.kubeflow.org/docs/pipelines/sdk/sdk-overview/#kfp-cli-tool)

[figure shows how to run the evaluation pipeline]

Whether you trained a model on synthetic, real or multiple datasets, you'll need that model here to specify it as one of the parameters of this pipeline. TODO: mention specifically which field

TODO: mention/explain params specific to this pipeline.

Just like for the training pipeline, you'll want to change `tb-log-dir` to point to a location that is convenient for you and your have permission to write to. This is where you'll read the logs and see the **performance metrics** once the pipeline completes.

> Note that an invalid location will cause the job to fail, whereas a path to the local filesystem may run but will be hard to monitor as you won't have easy access to the logs.

In addition to the logs, the performance metrics are also available in a Jupyter Notebook we have prepared that includes code to visualize the predictions.

### visualizing predictions and performance

We recommend running our [docker image] which includes jupyter as well as our notebooks if you don't want to setup the environment on your own. You can follow the same [instructions](https://github.com/Unity-Technologies/SynthDet/blob/master/docs/RunningSynthDetCloud.md#step-6-run-dataset-statistics-using-the-datasetinsights-jupyter-notebook) you may have used to run our [statistics notebook], but select the newer [visual inspection notebook] called "something.ipynb" instead.

TODO: check if the notebook contain performance metrics

### Using our pre-trained models
We trained a model using ~400k synthetic examples and then fine tuned it using ~700 real images. You can use the same [visual inspection notebook] mentioned above, but use one of our models from the list below:

- [Real World (760)](https://storage.googleapis.com/datasetinsights/models/Real-World/FasterRCNN.estimator)
- [Synthetic (400,000)](https://storage.googleapis.com/datasetinsights/models/Synthetic/FasterRCNN.estimator)
- [Synthetic (400,000) + Real World (76)](https://storage.googleapis.com/datasetinsights/models/Synthetic-And-Real-World-76-images/FasterRCNN.estimator)
- [Synthetic (400,000) + Real World (380)](https://storage.googleapis.com/datasetinsights/models/Synthetic-And-Real-World-380-images/FasterRCNN.estimator)
- [Synthetic (400,000) + Real World (760)](https://storage.googleapis.com/datasetinsights/models/Synthetic-And-Real-World-760-images/FasterRCNN.estimator)

TODO: names of models should be consistent with blog post.
TODO: the notebooks should include all urls, pick best one by default
