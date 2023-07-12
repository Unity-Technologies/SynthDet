<p align="center">
<img src="docs/images/unity-wide.png" width="3000"/>
<img src="docs/images/banner.PNG" align="middle"/>
</p>

# Branch description
Added Rigidbody component and Collision to prefabs. Edited file ForegroundObjectPlacementRandomizer.cs (removed Normalization and added item spawn by range on all axes)


# SynthDet: An end-to-end object detection pipeline using synthetic data  
[![license badge](https://img.shields.io/badge/license-Apache--2.0-green.svg)](LICENSE.md)
## Overview 
SynthDet is an open source project that demonstrates an end-to-end object detection pipeline using synthetic image data. The project includes all the code and assets for generating a synthetic dataset in Unity. Based on recent [research](#Citation), SynthDet utilizes Unity's [Perception](https://github.com/Unity-Technologies/com.unity.perception) package to generate highly randomized images of 63 common grocery products (example: cereal boxes and candy) and export them along with appropriate labels and annotations (2D bounding boxes). The synthetic dataset generated can then be used to train a deep learning based object detection model. This project is geared towards ML practitioners and enthusiasts who are actively exploring synthetic data or just looking to get started. 


### [Getting started with SynthDet](docs/GettingStartedSynthDet.md)

## Components 
* SynthDet Unity Project - Sample computer vision data generation project, demonstrating proper integration and usage of the Perception package for environment randomization and ground-truth generation. 
* 3D Assets - High quality models of 63 commonly found grocery products
* Unity's [Perception](https://github.com/Unity-Technologies/com.unity.perception) package.

## Unity Project overview
This project utilizes the Unity [Perception](https://github.com/Unity-Technologies/com.unity.perception) package for randomizing the environment and capturing ground-truth on each frame. Randomization includes elements such as lighting, camera post processing, object placement, and background. 

[Visit the Unity project documentation page](docs/UnityProjectOverview.md) for a brief overview on how ground truth generation and domain randomization are achieved in SynthDet.

## Tutorials
* [Setting up the SynthDet Unity project](docs/GettingStartedSynthDet.md)
* [Analyzing datasets with Pysolotools](https://github.com/Unity-Technologies/com.unity.perception/tree/main/com.unity.perception/Documentation~/Tutorial/pysolotools.md)
* [Visualizing a dataset with Voxel51 Viewer](https://github.com/Unity-Technologies/com.unity.perception/tree/main/com.unity.perception/Documentation~/Tutorial/pysolotools-fiftyone.md)
* [Converting to COCO](https://github.com/Unity-Technologies/com.unity.perception/tree/main/com.unity.perception/Documentation~/Tutorial/convert_to_coco.md)

In addition to the above, in order to learn how to create a project like SynthDet from scratch using the Perception package, we recommend you follow the [Perception Tutorial](https://github.com/Unity-Technologies/com.unity.perception/tree/main/com.unity.perception/Documentation~/Tutorial/TUTORIAL.md).

## Additional documentation
* [The real world groceries dataset](docs/UnityGroceriesReal.md)
* [Creating your own 3D assets](docs/CreatingAssets.md)
* [Overview on how the SynthDet Unity project works](docs/UnityProjectOverview.md)
* [Unity Perception package](https://github.com/Unity-Technologies/com.unity.perception)
* [Background on Unity](docs/BackgroundUnity.md)


## Inspiration
SynthDet was inspired by the following research paper from Google Cloud AI:  

Hinterstoisser, S., Pauly, O., Heibel, H., Marek, M., & Bokeloh, M. (2019). [*An Annotation Saved is an Annotation Earned: Using Fully Synthetic Training for Object Instance Detection.* ](https://arxiv.org/pdf/1902.09967.pdf)

## Unity project development
The original version of the SynthDet Unity project was developed in tandem with the early versions of Unity's Perception package. This project closely followed the synthetic data generation method introduced by the above mentioned Google Cloud AI paper. However, the original project did not use the randomization toolset that was introduced in later versions of the Perception package. To access this original project, and for more details on how it was implemented to replicate the research paper, please visit the [SynthDet_Original](https://github.com/Unity-Technologies/SynthDet/tree/SynthDet_Original) branch of this repository. The results reported in our related blog posts were based on this original project. That said, early experiments with datasets generated using the current version of the project have shown very similar model-training performance to that of the original one.

## Support
For general questions or concerns please contact the Unity Computer Vision team at computer-vision@unity3d.com.

For feedback, bugs, or other issues please file a GitHub issue and the Unity Computer Vision team will investigate the issue as soon as possible.

## Citation
If you find this project useful, consider citing it using:

```
@misc{synthdet2020,
    title={Training a performant object detection {ML} model on synthetic data using {U}nity {P}erception tools},
    author={You-Cyuan Jhang and Adam Palmar and Bowen Li and Saurav Dhakad and Sanjay Kumar Vishwakarma and Jonathan Hogins and Adam Crespi and Chris Kerr and Sharmila Chockalingam and Cesar Romero and Alex Thaman and Sujoy Ganguly},
    howpublished = {\url{https://blogs.unity3d.com/2020/09/17/training-a-performant-object-detection-ml-model-on-synthetic-data-using-unity-computer-vision-tools/}},
    journal={Unity Technologies Blog},
    publisher={Unity Technologies},
    year={2020},
    month={Sep}
}
```

## Additional Resources
[GTC 2020: Synthetic Data: An efficient mechanism to train Perception Systems](https://developer.nvidia.com/gtc/2020/video/s22700)

[Synthetic data: Simulating myriad possibilities to train robust machine learning models](https://blogs.unity3d.com/2020/05/01/synthetic-data-simulating-myriad-possibilities-to-train-robust-machine-learning-models/)

[Use Unity’s computer vision tools to generate and analyze synthetic data at scale to train your ML models](https://blogs.unity3d.com/2020/06/10/use-unitys-computer-vision-tools-to-generate-and-analyze-synthetic-data-at-scale-to-train-your-ml-models/)

[Training a performant object detection ML model on synthetic data using Unity computer vision tools](https://blogs.unity3d.com/2020/09/17/training-a-performant-object-detection-ml-model-on-synthetic-data-using-unity-computer-vision-tools/)

## License
[Apache License 2.0](LICENSE.md)
