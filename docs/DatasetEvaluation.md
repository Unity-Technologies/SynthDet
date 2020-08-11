# Dataset Evaluation (Public Dataset)

This walkthrough shows how to run dataset evaluation consuming public dataset.

## Workflow (Step-by-step)

### Step 1: Set up additional prerequisites

See the "Additional requirements for running Kubeflow Pipeline" section in [Prerequisites](Prerequisites.md).

### Step2: Train on Synthetic Dataset (SynthDet)

Training a model using SynthDet subset dataset

1. Short description about SynthDet subset dataset and provide link to public SynthDet subset dataset.
2. Contact us if you want to have access to the full 400k dataset.
3. Provide link to a pre-compiled Kubeflow Pipeline for SyntDet subset training using Kubeflow.
4. Screenshots how to run such a deployed pipeline.
    - document default parameter/variable here.

### Step3: Train on Real World Dataset (GroceriesReal)

GroceriesReal training

1. Short description about GroceriesReal dataset and provide link to public GroceriesReal dataset.
2. Same as SynthDet training 3 & 4, point to different pre-compiled pipeline and parameters.


### Step4: Train on Synthetic + Real World Dataset

Take pre-trained model from step 2 and fine-tune using small subset of GroceriesReal dataset.

- Same as SynthDet training 3 & 4, point to different pre-compiled pipeline and parameters.

### Step5: Evaluation

Take trained model from step 2, 3, 4 and run model evaluation

- Same as SynthDet training 3 & 4, point to different pre-compiled pipeline and parameters.

#### Using pre-trained model

<!-- This section should align with blog post Table1 -->

We have released the following pre-trained models:

- [Real World (760)](http://url)
- [Synthetic (400,000)](http://url)
- [Synthetic (400,000) + Real World (76)](http://url)
- [Synthetic (400,000) + Real World (380)](http://url)
- [Synthetic (400,000) + Real World (760)](http://url)

Users can follow [Evaluation Step](#step5-evaluation) to evaluate pre-trained models.

# Dataset Evaluation (Unity Simulation)

## Workflow (Step-by-step)

### Step 1: Set up additional prerequisites

See both "Additional requirements for running in Unity Simulation" and "Additional requirements for running Kubeflow Pipeline" section in [Prerequisites](Prerequisites.md).

### Step2: Train on Synthetic Dataset (SynthDet)

Training a model using dataset generated in [GettingStartedSynthDet](GettingStartedSynthDet.md).

- Provide link to a pre-compiled Kubeflow Pipeline for SyntDet training using Kubeflow (with Unity Simulation download)
- Screenshots how to run such a deployed pipeline.
    - document default parameter/variable here.

Follow similar steps in Step 3-5 of [Dataset Evaluation (Public Dataset)](#dataset-evaluation-public-dataset) section to train, fine-tuning and evaluate using GroceriesReal Dataset.
