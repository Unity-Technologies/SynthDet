# Overview of the SynthDet Unity Project

This project utilizes the Unity [Perception](https://github.com/Unity-Technologies/com.unity.perception) package for randomizing the environment and generating ground-truth on each frame. 

## Ground truth generation

To generate ground truth, the [Perception Camera](https://github.com/Unity-Technologies/com.unity.perception/blob/master/com.unity.perception/Documentation~/PerceptionCamera.md) is used, which is a pre-built component available in the Perception package. This special type of camera accepts a list of Labelers, each of which is capable of generating a specific type of ground truth. To speed up your workflow, the Perception package comes with seven common labelers for object-detection and human keypoint labeling tasks; however, if you are comfortable with code, you can also add your own custom Labelers. The Labelers that come with the Perception package cover **2D bounding boxes, object counts, object information (pixel counts and ids), keypoint labeling, 3D bounding boxes, instance segmentation, and semantic segmentation**. SynthDet uses the first three of these. The 2D bounding boxes are used for training object detection models, and the object count and object information Labelers are used for analyzing the statistics of the generated datasets. 

To see the list of Labelers attached to your Perception Camera, select the `Perception Camera` GameObject in the Scene ***Hierarchy*** window.

## Domain randomization

The Perception package comes with a [randomization](https://github.com/Unity-Technologies/com.unity.perception/blob/master/com.unity.perception/Documentation~/Randomization/Index.md) toolset that makes it easy to coordinate simulations in which environments are continually randomized. Simulations are coordinated by Scenarios, which run a set of Randomizers in a deterministic order. Each Randomizer is then tasked with randomizing a certain aspect of the environment. Randomizers are flexible and extensible, making it easy for users to create their own custom randomization strategies.

To check out the list of Randomizers used in the SynthDet project, select the `Scenario` GameObject inside the `SynthDet` Scene. The **Inspector** window will then look like this:

<p align="center">
<img src="images/randomizers_collapsed.png" width="400"/>
</p>

Besides the list of Randomizers, you also see several properties for the Scenario. The `Random Seed` set here is the seed used for all randomizations that happen throughout the Scenario, in all Randomizers. If you only use the provided randomization toolset for generating random values throughout your project's C# code, you are guaranteed to have identical outputs between simulations that use the same exact Randomizers and `Random Seed` value. Each Scenario comprises a number of Iterations and each Iteration can be run for a number of frames. Randomizers can be scripted to perform operations at different times, including when the simulation first starts, the start or end of each Iteration, or on each frame of each Iteration. `Total Iterations` and `Frames Per Iteration` control how many Iterations the Scenario performs and how many frames each Iteration runs for. 

In the above screenshot, the UI for all Randomizers is collapsed. Most Randomizers come with properties and parameters that you can modify. To modify a Randomizer, you just need to expand its UI by clicking the small triangle icon to the left of its name, like below:

<p align="center">
<img src="images/foreground_randomizer.png" width="400"/>
</p>

Here is what each Randomizer in SynthDet does:
* **BackgroundObjectPlacementRandomizer**
  * The background consists of a large number of primitive shapes with randomized positions, rotations, textures, and hue offsets. The purpose of this background is to act as distraction for the machine learning model. This Randomizer has the task of instantiating and positioning these primitive shapes.
* **ForegroundObjectPlacementRandomizer**
  * This Randomizer instantiates and positions the foreground objects. These are the grocery objects, for which we generate bounding boxes.
* **ForegroundOccluderPlacementRandomizer**
  * Another distraction for the model comes in the form of shapes that are placed between the camera and the objects that are to be detected (the grocery objects). These occluders are randomized in position, scale, texture, and hue offset. This Randomizer is tasked with instantiating and positioning them.
* **ForegroundOccluderScaleRandomizer**
  * Randomizes the scale of the foreground occluder objects.
* **ForegroundScaleRandomizer**
  * Assigns random scales to each of the foreground (grocery) objects.
* **TextureRandomizer**
  * Randomizes the textures of the background shapes and foreground occluder objects.
* **HueOffsetRandomizer**
  * Randomizes the hue offset of the background shapes and foreground occluder objects.
* **RotationRandomizer**
  * The background shapes, foreground occluder objects, and one of the four directional lights present in the Scene are rotated randomly using this Randomizer.
* **UnifiedRotationRandomizer**
  * This Randomizer assigns a random rotation to all foreground (grocery) objects. The difference between this and `RotationRandomizer` is that here, the same rotation is applied to all target objects.
* **LightRandomizer**
  * The Scene contains four directional lights, three of which light all objects, and one lights the background only. The background light has a very high intensity range and only turns on with a small probability. This Randomizer is tasked with randomizing the intensity and color of all lights, as well as deciding whether the intense background light should be on.
* **CameraRandomizer**
  * Randomizes post-processing effects on the camera. These include saturation, contrast, and blur.
* **ForegroundObjectMetricReporter**
  * This Randomizer does not actually randomize anything. Instead, it reports the rotation, position, and scale of foreground (grocery) objects. The reason this reporting is done in the form a Randomizer is to make sure it happens after all other randomizations are complete. Putting it at the bottom of the list in the Scenario guarantees that and simplifies our timing considerations.
* **LightingInfoMetricReporter**
  * Similar to the above, this reporter Randomizer is used for keeping records of the enabled state, intensity, colour, and rotation of all lights present in the scene.
* **CameraPostProcessingMetricReporter**
  * Reports metrics for the camera's post-processing factors. This includes saturation, contrast, and blur.