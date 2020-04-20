# About the SimvViz for AI & Autonomous Vehicles
This package includes a toolchain for the generating synthetic data for autonomous driving perception systems in Unity

# Installing SimViz Package :
To install the _SimViz_ package, follow the instructions in the [Package Manager](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest/index.html). 

In addition, you need to install the following resources:
 * Render Pipelines Core : To install, open *Window > Render Pipelines Core*

# Using SimViz

## Scene creation from procedural placement
Below illustrates a basic use case of preparing a scene to be used for creating scenarios using procedural placement.

### Prerequisites:
* A set of assets to be used for placement
* A road map in OpenDrive (.xodr) format

### Importing Assets for placement
Ensure that assets adhere to the requirements (AssetImportRequirements.pdf in Documentation~ directory) for importing with SimViz.
Asset categorization is configured via a .txn file, which is a json file that maps a category to a folder of assets.  To set up your configuration file:
1.  In your assets folder, structure your assets such that each asset type is in a specific folder, for instance, trees should go in a "Trees" folder.  While not required, this will make it easier to create your configuration file.
2.  Create a .txn file in your assets folder using a json or plain text editor.  See an example of the txn file format (SimpleAssets.txn in Documentation~ directory).  It is a fairly simple mapping of an asset category to a list of paths which contain prefabs of that asset category.
Your txn file will be imported and a number of PlacementCategory assets will be created under the .txn file in your project window. 

### Importing road map
To import your OpenDrive road map, simply place the .xodr in your assets folder.  The map will then be imported into the project and ready for use.

### Visualizing the road map in Unity
1.  Select the .xodr file in your project window
2.  Select Tri->Lanes -> Lines from the menu  <!--(TODO:  update with new Ux) -->
This will create a number of game objects in your active scene that will show you where the lanes of your OpenDrive network are.  These lanes are the reference for the placement algorithms.

### Creating placement parameters
1.  Select Assets->Create->Content->PolygonSoupPlacementParameters.  This will create a new asset for defining placement parameters.
2.  Select the newly-created placement parameters asset in your project window.
3.  In the placement parameters, select .xodr file as the Road Network Description, select and the placement category object (created by Importing Assets above) and parameters for each category.  A good starting point is to have spacing and offset at least at 2 for all objects you are placing.

![Placement example window](Placement.PNG)

4.  Select the "Run Pipeline" button
This will populate your active scene with the imported objects along your road network.


<!--The contents of this section depends on the type of package.

For packages that augment the Unity Editor with additional features, this section should include workflow and/or reference documentation:

* At a minimum, this section should include reference documentation that describes the windows, editors, and properties that the package adds to Unity. This reference documentation should include screen grabs (see how to add screens below), a list of settings, an explanation of what each setting does, and the default values of each setting.
* Ideally, this section should also include a workflow: a list of steps that the user can easily follow that demonstrates how to use the feature. This list of steps should include screen grabs (see how to add screens below) to better describe how to use the feature.

For packages that include sample files, this section may include detailed information on how the user can use these sample files in their projects and scenes. However, workflow diagrams or illustrations could be included if deemed appropriate.

## How to add images

*(This section is for reference. Do not include in the final documentation file)* 

If the [Using &lt;package name&gt;](#UsingPackageName) section includes screen grabs or diagrams, a link to the image must be added to this MD file, before or after the paragraph with the instruction or description that references the image. In addition, a caption should be added to the image link that includes the name of the screen or diagram. All images must be PNG files with underscores for spaces. No animated GIFs.

An example is included below:

![A cinematic in the Timeline Editor window.](images/example.png)

Notice that the example screen shot is included in the images folder. All screen grabs and/or diagrams must be added and referenced from the images folder.

For more on the Unity documentation standards for creating and adding screen grabs, see this confluence page: https://confluence.hq.unity3d.com/pages/viewpage.action?pageId=13500715
-->



# Technical details
## Requirements

This version of _SimViz_; is compatible with the following versions of the Unity Editor:

* 2019.3 and later (recommended)

<!--To use this package, you must have the following 3rd party products:

* &lt;product name and version with trademark or registered trademark.&gt;-->

## Known limitations
SimViz version 0.1 Alpha includes the following known limitations:
* XODR maps larger than 50MB are extremely slow and difficult to use in the editor

## Package contents
|Content|Contains data samples, tests, and tools for procedural scene creation|
|---|---|
|`Clipper`|OpenSource library for polygon clipping
|`MapElements`|Contains elements used for creating roads i.e. lanes and networks|
|`Poly2Tri`|Contains more utilities for Triangulation Algorithm's and other utilities|
|`Sampling`|Implementation of road network lane/line sampling|

|Ground Truth|Captures RGB channels and segmentation data from a camera|
|---|---|
|Labeling|Script object that labels an asset for target taxonomy|
|Labeling Configuration|Captures all the labeling and adds the data to the corresponding pixels|

|Scenario Manager|Contains different sample scenarios of driving conditions for a car to operate in|
|---|---|
|`QuickGraph`|graphing calculator for 2D, 3D spatial coordinate systems|

|Procedural Placement|Allows you to procedural create cities based on OpenDRIVE files|
|---|---|
|Polygon Soup|Helps create a city using a road mesh network and assets using the data pipeline|

<!--
|Sensors|Contains the simulated sensors for vehicles to use|
|---|---|
|`Editor`| Scenario Manager for the unity editor|
|`GuidReference`| Guid references for components and managers|
-->

## Document revision history
|2019-12-3|Updated SimViz documentation for Alpha release v2|
|---|---|
|2019-10-30|Updated SimViz documentation for Alpha release|
|---|---|
|2019-09-13|Document edited from template for SimViz|
|---|---|
|2019-06-10|Unedited. Published to package.|
