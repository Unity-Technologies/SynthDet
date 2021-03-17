<p align="center">
<img src="docs/images/unity-wide.png" width="3000"/>
<img src="docs/images/banner.PNG" align="middle"/>
</p>

# SynthDet: An end-to-end object detection pipeline using synthetic data  
[![license badge](https://img.shields.io/badge/license-Apache--2.0-green.svg)](LICENSE.md)
## Overview 
SynthDet is an open source project that demonstrates an end-to-end object detection pipeline using synthetic image data. The project includes all the code and assets for generating a synthetic dataset in Unity. Based on recent [research](#Citation), SynthDet utilizes Unity's [Perception](https://github.com/Unity-Technologies/com.unity.perception) package to generate highly randomized images of 63 common grocery products (example: cereal boxes and candy) and export them along with appropriate labels and annotations (2D bounding boxes). The synthetic dataset generated can then be used to train a deep learning based object detection model.
This project is geared towards ML practitioners and enthusiasts who are actively exploring synthetic data or just looking to get started. 

### [Getting started with SynthDet](docs/Readme.md)

## Components 
* SynthDet Unity Project - Sample computer vision data generation project using Unity's Perception package
* 3D Assets - High quality models of 63 commonly found grocery products
* Unity's [Perception](https://github.com/Unity-Technologies/com.unity.perception) package.
* Unity's [Dataset Insights](https://github.com/Unity-Technologies/datasetinsights) Python package

## Inspiration
SynthDet was inspired by the following research paper from Google Cloud AI:  

Hinterstoisser, S., Pauly, O., Heibel, H., Marek, M., & Bokeloh, M. (2019). [*An Annotation Saved is an Annotation Earned: Using Fully Synthetic Training for Object Instance Detection.* ](https://arxiv.org/pdf/1902.09967.pdf)

## SynthDet Unity Project Development History
### Current version
In March 2021, we released a new version of the SynthDet Unity project. In this version, we have rebuilt the project in order to demonstrate proper integration and usage of the latest Perception package, including the new Randomization toolset. Additionally, users will find the code more accessible and extensible, and the UI more usable and intuitive toward customization.

Early experiments with datasets generated using the new project have shown very similar model-training performance to that of the original one.

### Original version
The original version of the SynthDet Unity project was developed in tandem with the early versions of Unity's Perception package. This project closely followed the synthetic data generation method introduced by the above mentioned Google Cloud AI paper. To access this original project, and for more details on how it was implemented to replicate the research paper, please visit the [SynthDet_Original](https://github.com/Unity-Technologies/SynthDet/tree/SynthDet_Original) branch of this repository. The results reported in our related blog posts were based on this original project.

## Support
For general questions or concerns please contact the Unity Computer Vision team at computer-vision@unity3d.com.

For feedback, bugs, or other issues please file a GitHub issue and the Unity Computer Vision team will investigate the issue as soon as possible.

## Citation
If you find this package useful, consider citing it using:

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
