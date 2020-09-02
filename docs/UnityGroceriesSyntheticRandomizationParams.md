#UnityGroceries - Synthetic Randomization Parameters

SynthDet follows a curriculum strategy for generating data. The full dataset is 
generated using a sequence of scale factors, generally starting with objects 
that are larger and gradually growing smaller. For a complete description of the 
SynthDet curriculum, see the following paper: https://arxiv.org/pdf/1902.09967.pdf. 
For public UnityGroceries - Synthetic dataset we generated nearly around 5k
 synthetic images. The dataset can be downloaded from [here](https://storage.googleapis.com/datasetinsights/data/synthetic/SynthDet.zip).

Following is the description of randomization parameters used for generating the
 synthetic
 data:
 
- Scale Factor Range: The distribution of scale factor values to distribute
 across steps in the curriculum.
 
- Scale Factor Steps: The number of scale factors to generate in the dataset. 
Scale factors will be uniformly distributed across the Scale Factor Range. 
This number is directly proportional to the total number of images in the 
resulting dataset.

- Steps Per Instance: The number of scale factors to assign to each instance
 on Unity Simulation.

- Max Frames Per Instance: An upper limit on the number of frames which are
 allowed to be generated on each instance in Unity Simulation. If scale factor 
 is very large, it can take thousands of images to progress through the 
 curriculum, so this limit ensures datasets do not grow too large.

- Max Objects Per Frame: The max number of foreground objects to place in each
 frame.

- Background Object Density: The number of objects per square meter to
 distribute in a background fill pass.

- Num Background Passes: Number of times the background generator will
 generate a collection of objects to fill background.

- Occluding Object Scaling Range Minimum/Size: The range of sizes of occluding
 objects relative to the size of the foreground objects. A minimum of 0.2 and
  range size of 0.1 means that occluding objects will be scaled to be between
   20% and 30% the size of the foreground objects.

- Light Color Minimum: Light color for each of its red, green, and blue
 components will be set to a random value between this value and 1.0 each frame. 
 Could also be considered minimum live intensity.

- Light Rotation Maximum: The maximum amount of rotation to apply to the light
 each frame off of the default orientation. The light's default orientation is 
 pointing in the same direction as the camera.

- Background Hue Maximum Offset: Maximum amount of hue shift, in degrees, to
 apply to textures on background objects. For the definition of this node, see 
 https://docs.unity3d.com/Packages/com.unity.shadergraph@6.9/manual/Hue-Node.html

- Occluding Hue Maximum Offset: Maximum amount of hue shift, in degrees, to
 apply to textures on background objects.

- Background in Foreground Chance: Probability of placing a background object
 whenever a foreground object would be placed. This is used to increase the 
 number of contiguous background object examples in the frames. This does not 
 "skip" any foreground objects in the curriculum.

- White Noise Maximum Strength: The maximum strength of the white noise 
post-processing effect.

- Blur Kernel Maximum Size: The maximum size of the Gaussian blur 
post-processing effect.

- Blur Standard Deviation Maximum: The maximum standard deviation of the
 Gaussian blur post-processing effect.
 
 
 
 Following are the value of the randomization parameters used to generate 
 public UnityGroceries - Synthetic dataset:
 

 | Parameter | Value |
|---|---|
|Scale Factor Range | 1-0.5  |
|Scale Factor Steps | 4  |
|Steps Per Instance | 1  |
|Max Frames Per Instance | 5000  |
|Max Objects Per Frame | 500  |
|Background Object Density | 3  |
|Num Background Passes  | 1  |
|Occluding Object Scaling Range Minimum | 0.2  |
|Occluding Object Scaling Range Size | 0.1  |
|Light Color Minimum |  0.1 |
|Light Rotation Maximum  |  90 |
|Background Hue Maximum Offset |  180 |
|Occluding Hue Maximum Offset |  180 |
|White Noise Maximum Strength |  0.02 |
|Blur Kernel Maximum Size | 0.01  |
|Blur Standard Deviation Maximum  |  0.5 |

