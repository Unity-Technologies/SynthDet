# Annotated Dataset 

<img src="images/annotatedPicture.png" align="middle"/>

## Real World Annotated Dataset 
We have a public annotated dataset that is currently being hosted in [gcs](https://storage.cloud.google.com/thea-dev/data/groceries/v1.zip?authuser=0) for users to download and explore.

<img src="images/realImage.png" align="middle"/>

## Synthentic Data
Training using synthetic data models is very attrective because large amount of datasets can be generated to train models without of a large cost of a entire team creating the data. While creating the background images seen in this sample project we applied random lighting, random occlusion and depth layers, random noise, and random blur to the images to provide a more robust enviroment for the model to train on. This is allowed users to create synthentic data that allows the precise control of the the rendering procces of the images to include various properties of the image.

## Real World Data
Real world data is extremely costly and time consuming to produce and error prone. This can become a large limitation on creating real world datasets that can be used 
to train models. This can be a long procces because a human has to take pictures of the objects in various lighting, poses, and layouts that can be later used for annotating labels onto the images.

## Labeling Images
This is done using a labeling tool that allows user's to dary 2D bounding boxes around objects in a image. Once the box is around the image the user can then go and add a label to the bounding box (i.e. food-ceral-luckycharms), this has to be done for each object in the image. To build a dataset of images you need to have a large amount of images with different objects, layouts, etc... Once you complete this step you can begain to train a model using these real images to check the performance of the model on Tensor board or another notebook service

## Domain Randomization 
Currently the stragety for creating purely synthetic images to be used for training object detection that is showing promising results. This is done by using a large amount of created 3D assets and randomly distrubing the objects in different poses and depth layers. After the image is created a few things happen to the image to alter the rendering, a random noise filter, a random noise filter, a random blur, and a random color illumination is added to te image. 
