<img src="docs/images/unity-wide.png" align="middle" width="3000"/>

<img src="docs/images/banner.PNG" align="middle"/>

# SynthDet: An end-to-end object detection pipeline using synthetic data  
[![license badge](https://img.shields.io/badge/license-Apache--2.0-green.svg)](LICENSE.md)
## Overview 
SynthDet is an open source project that demonstrates an end-to-end object detection pipeline using synthetic image data. The project includes all the code and assets for generating a synthetic dataset in Unity. Using recent [research](#Citation), SynthDet utilizes Unity Perception package to generate highly randomized images of 64 common grocery products (example: cereal boxes and candy) and export them along with appropriate labels and annotations (2D bounding boxes). The synthetic dataset generated can then be used to train a deep learning based object detection model.
This project is geared towards ML practitioners and enthusiasts who are actively exploring synthetic data or just looking to get started. 

[GTC 2020: Synthetic Data: An efficient mechanism to train Perception Systems](https://developer.nvidia.com/gtc/2020/video/s22700)

## Contents 
* SynthDet - Unity Perception sample project
* 3D Assets - High quality models of 64 commonly found grocery products
* Unity Perception package
* Python package for model training, testing, and dataset insights

## Release & Documentation
#### [Getting started with SynthDet](docs/Readme.md)

Version|Release Date |Source
-------|-------------|------
   V0.1  |May 26, 2020|[source](https://github.cds.internal.unity3d.com/unity/google-dr-paper)

## Citation
SynthDet was inspired by the following research paper from Google Cloud AI:  

Hinterstoisser, S., Pauly, O., Heibel, H., Marek, M., & Bokeloh, M. (2019). [*An Annotation Saved is an Annotation Earned: Using Fully Synthetic Training for Object Instance Detection.* ](https://arxiv.org/pdf/1902.09967.pdf)

<!--## Additional Resources 
---Annotated real dataset---

THIS DATA MAY BE USED FOR NON-COMMERCIAL PURPOSES ONLY AND IS PROVIDED "AS IS" AND "AS AVAILABLE", WITH NO REPRESENTATIONS OR WARRANTIES OF ANY KIND; SEE LICENSE FOR DETAILS. 
YOU ARE SOLELY RESPONSIBLE, AND ACCEPT FULL RESPONSIBILITY, FOR YOUR USE OF THIS DATA, INCLUDING ANY USE INFRINGING OF ANY THIRD PARTY'S INTELLECTUAL PROPERTY RIGHTS.
UNITY TECHNOLOGIES IS NOT AFFILIATED WITH, AND DOES NOT ENDORSE/SPONSOR AND IS NOT ENDORSED OR SPONSORED BY, ANY COMPANIES OR BRANDS IDENTIFIABLE IN THE DATA.

[Real world dataset link](https://storage.cloud.google.com/thea-dev/data/groceries/v1.zip?authuser=0) with object detection labels according to the [Coco data format](http://cocodataset.org/#format-data)!-->

## License
* [License](LICENSE.md)
