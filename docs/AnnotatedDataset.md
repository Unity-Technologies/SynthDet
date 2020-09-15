# Annotated dataset 

<img src="images/annotatedPicture.png" align="middle"/>

<!--## Real World Annotated Dataset 
You can download and explore a public annotated dataset, hosted in [Google Cloud Storage](https://storage.cloud.google.com/thea-dev/data/groceries/v1.zip?authuser=0).

<img src="images/realImage.png" align="middle"/>-->

## Synthetic data
The advantage of training with synthetic data models is that you can generate large numbers of datasets to train models without the cost of an entire team creating the data. When the SynthDet team created the background images in this sample project, they applied the following rendering processes to the images, to provide a more robust environment for the model to train on: 

- Random lighting
- Random occlusion and depth layers
- Random noise
- Random blur 

Users can create synthetic data that allows the precise control of the image rendering process to include various properties of the image in the simulation.

## Real-world data
Real-world data is error-prone and time-consuming to produce. This is a significant limitation on creating real-world datasets that can train models. This can be a long process because a human has to take pictures of the objects in various lighting, poses, and layouts that they or somebody else can later annotate with labels.

## Labeling images
Users can label images with a labeling tool that allows users to draw 2D bounding boxes around objects in an image. When the box is around the image the user can  add a label to the bounding box (for example, food-cereal-luckycharms). The user must do this for each object in the image. To build a dataset of images you must have a large number of images with different objects and layouts. When you complete this step you can start training a model with these real images to check the performance of the model on Tensor board or another notebook service

## Domain randomization 
Currently, the strategy for creating purely synthetic images for training object detection is showing promising results. This strategy uses a large amount of created 3D assets and randomly arranges the objects in different poses and depth layers. After the user creates an image a few things happen to the image to alter the rendering: a random noise filter, a random blur, and a random color illumination are added to the image. 

## Enumeration of exceptions and assumptions
There are some places where the Unity SynthDet team made slight adjustments to the implemented methodology compared to what the authors of the paper describe. The team made these changes either because there was not enough detail to definitively infer what the exact implementation entailed, or because the process the authors described seemed either more complex or more performance-intensive than the actual implementation. Modifications the team have made do not substantially alter the results of the experiment. The team logged any changes they made and adjusted any variances that the team identified as being insufficiently similar. In the team's code, these deviations are identified with an "// XXX:" comment. 

### "Filling" the background
The paper describes a process by which the authors render background objects randomly within the background until the entire screen space is covered. They do this by only placing items in regions of the screen not already occupied by an object.  This requires a significant amount of rendering, checking and re-rendering, and is not particularly performant. In the `BackgroundGenerator` class, the SynthDet team instead divided the space into cells of sufficient density to ensure that with several objects rendered in a random position and orientation in each cell, the background is completely filled. This avoids the need to do multiple rendering passes or any per-frame introspection on the rendered pixels, saving performance resources at the cost of potentially creating a background that is insufficiently dense or too dense.

### Background object scaling
A slightly randomized scaling factor is applied to the background objects, such that their projected size is within a factor of between 0.9 and 1.5 times the foreground object that is being placed. The authors describe a strategy for generating subsets of scaling factors from within the 0.9-1.5 range in order to create backgrounds with "primarily large" or "primarily small" objects. However, given that the subset is randomly selected and values from within the subset are also randomly selected, the step is omitted, because the SynthDet team thought it was extraneous.  

Also, the "projected size" mentioned in the paper was not described in detail. The team understood this to  to mean "surface area in pixels," and are approximating this projected size by computing the surface area of the 2D polygon formed by the 3D axis-aligned bounding box of the object after rotation.

### Computing clipping/occlusion percentages
When placing foreground and occluding objects, the paper often mentions determining how much one object occludes or clips with another, or how much it clips with the edges of the screen area. However, the authors do not describe whether this is a pixel-level comparison or an approximation, and the thresholds it uses to make decisions for whether or not to reposition an object are all presented with a single significant digit. The SynthDet team understood this to mean that the computations are approximate, and are using the projected rectangular bounding boxes in the `ForegroundObjectPlacer` to do these computations to save on complexity and compute.

### Camera projection model
The camera uses a perspective camera model to capture images. Visual inspection suggests that there might be vanishing points along parallel lines, indicating that a perspective transform is in play, but given the resolution and field of view in use, there shouldn't be any differences between perspective and orthographic projection that are significant enough to justify the more complex model.