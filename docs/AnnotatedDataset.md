# Annotated Dataset 

<img src="images/annotatedPicture.png" align="middle"/>

## Real World Annotated Dataset 
As a part of this project there is a public annotated dataset that is currently being hosted in [gcs](https://storage.cloud.google.com/thea-dev/data/groceries/v1.zip?authuser=0) for users to download and explore.

<img src="images/realImage.png" align="middle"/>

## Synthetic Data
Training using synthetic data models is very attractive because large amounts of datasets can be generated to train models without a large cost of an entire team creating the data. While creating the background images seen in this sample project the following was applied. Random lighting, random occlusion and depth layers, random noise, and random blur to the images to provide a more robust environment for the model to train on. This allows users to create synthetic data that allows the precise control of the rendering process of the images to include various properties of the image in the simulation.

## Real World Data
Real world data is extremely costly and time consuming to produce and error prone. This can become a large limitation on creating real world datasets that can be used 
to train models. This can be a long procces because a human has to take pictures of the objects in various lighting, poses, and layouts that can be later used for annotating labels onto the images.

## Labeling Images
This is done using a labeling tool that allows users to draw 2D bounding boxes around objects in an image. Once the box is around the image the user can then go and add a label to the bounding box (i.e. food-ceral-luckycharms), this has to be done for each object in the image. To build a dataset of images you need to have a large amount of images with different objects, layouts, etc... Once you complete this step you can begin to train a model using these real images to check the performance of the model on Tensor board or another notebook service

## Domain Randomization 
Currently the strategy for creating purely synthetic images to be used for training object detection that is showing promising results. This is done by using a large amount of created 3D assets and randomly disturbing the objects in different poses and depth layers. After the image is created a few things happen to the image to alter the rendering, a random noise filter, a random noise filter, a random blur, and a random color illumination is added to the image. 

## Enumeration of Exceptions & Assumptions
There are some places where adjustments where made to the implemented methodology slightly from what is described in the paper.These changes where made either because there was not enough detail to definitively infer what the exact implementation entailed, or because the process described seemed either more complex or performance intensive than what has been implemented.  Currently any modifications made will not substantially alter the results of the experiment, and changes made will be kept track of and adjust any variances that identified as being insufficiently similar. In our code, these deviations are identified with an "// XXX:" comment

### "Filling" the background
The paper describes a process by which they render background objects randomly within the background until the entire screen space is covered, and they do so by only placing items in regions of the screen not already occupied by an object.  This requires quite a bit of rendering, checking, re-rendering, and so on and is not particularly performant. In the BackgroundGenerator class, we instead divide the space into cells of sufficient density to ensure that with several objects rendered in a random position and orientation in each cell, the background will be completely filled. This circumvents having to do multiple rendering passes or any per-frame introspection on the rendered pixels, saving quite a bit of compute at the cost of potentially creating a background that is insufficiently OR overly dense.

### Background object scaling
A slightly randomized scaling factor is applied to the background objects such that their projected size is within a factor of between 0.9 and 1.5 times the foreground object being placed. The authors describe a strategy for generating subsets of scaling factors from within the 0.9-1.5 range in order to create backgrounds with "primarily large" or "primarily small" objects. However, given that the subset is randomly selected and values from within the subset are also randomly selected, currently the step is omited for the time being as it seemed extraneous.  
In addition, the "projected size" mentioned in the paper was not described in detail. This is understood to mean "surface area in pixels," and are approximating this projected size by computing the surface area of the 2-dimensional polygon formed by the 3D axis-aligned bounding box of the object after rotation.

### Computing clipping/occlusion percentages
When placing foreground and occluding objects, the paper often mentions determining the percent one object occludes or clips with another, or how much is clips with the edges of the screen area. However, it does not describe whether this is a pixel-level comparison or an approximation, and the thresholds it uses to make decisions for whether or not to reposition an object are all presented with a single significant digit. This is understood to mean that the computations are approximate, and are using the projected rectangular bounding boxes in the ForegroundObjectPlacer to do these computations to save on complexity and compute.

### Camera projection model
The camera is currently capturing images using an perspective camera model. Visual inspection hints that there may be vanishing points along parallel lines, indicating a perspective transform is in play, but given the resolution and field of view in use, there shouldn't be any differences between perspective and orthographic projection to be significant enough to justify the more complex model.