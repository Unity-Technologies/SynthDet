# SimViz

The SimViz package contains tools for authoring and executing autonomous vehicle simulations. They are organized into two categories: Scenarios and Sensors.

## Scenarios

TODO

## Sensors

Reference implementations of the following sensors are included:

* RGB(D) panoramic camera
* Lidar
* Radar

Note that these implementations were developed by a third party and are not allowed to be broadly distributed yet.

# Open Street Map conversion to OpenDRIVE files

## Setup:
* Install the [SUMO](https://sumo.dlr.de/releases/1.3.1/sumo-win64-1.3.1.msi) to use NETCONVERT
* Download an osm file from [Open Street Maps](https://www.openstreetmap.org/export#map=13/47.5980/-122.1551)
 * Or grab a map from [simViz](https://drive.google.com/drive/u/0/folders/1-nbFgyn-lFqzLqMz6UpzINIsowZUx10t) team share

## Using the Tool
* Example cmd for converting osm to xodr
* netconvert --osm-files "Map.osm" --opendrive-output "Map.xodr"

# Setup for local development
* Clone the simviz repository into an arbirary directory on disk
* Initialize the Path-Creator using `git submodule update --init --recursive`
* In the root of your clone, run `git lfs install` followed by `git lfs pull`
* Install and use Unity 2019.3.0b7 (SimViz will currently not work on 2019.3.0b9 or later)
## Option 1: SimVizTest
The repository includes a project for local development in `TestProjects/SimVizTest`. 

Note: This project is the only way to run some of the tests.

## Option 2: Set up a project from scratch
*The following instructions reference the Unity doc's page on [installing a local package](https://docs.unity3d.com/Manual/upm-ui-local.html)*
* Create a new HDRP project or open an existing one
* Open your project's `<project root>/Packages/manifest.json` in a text editor
* At the end of the file, add `"registry": "https://artifactory.prd.cds.internal.unity3d.com/artifactory/api/npm/upm-candidates"`
    * _Note: This step will be removed once the dependency `com.unity.entities-0.2.0-preview.*` is published publically._
* Back in Unity, open the Package Manager window
* Add the High Definition RP package, version 7.1.2 or later
* Click the ***+*** button in the upper lefthand corner of the window
* Click the ***add package from disk*** option
* Select to the package.json file under the com.unity.simviz folder in your cloned simviz repository
* To allow the compilation and running of tests, add `"testables": [ "com.unity.simviz" ]`
    * For an example `manifest.json`, see `TestProjects/SimVizTest/Packages/manifest.json`
    * For more on the `manifest.json` schema, see the [Package Manager documentation](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@1.7/manual/index.html#advanced-package-topics)

## Suggested IDE Setup
For closest standards conformity and best experience overall, JetBrains Rider or Visual Studio w/ JetBrains Resharper are suggested. For optimal experience, perform the following additional steps:
* To allow navigating to code in all packages included in your project, in your Unity Editor, navigate to `Edit -> Preferences... -> External Tools` and check `Generate all .csproj files.` 
* To get automatic feedback and fixups on formatting and naming convention violations, set up Rider/JetBrains with our Unity standard .dotsettings file by following [these instructions](https://github.cds.internal.unity3d.com/unity/com.unity.coding/tree/master/UnityCoding/Packages/com.unity.coding/Coding~/Configs/JetBrains).
* If you use VS Code, install the Editorconfig extension to get automatic code formatting according to our conventions.

# Artifact repository for unity package servers 

The Yamato build step `publish` will publish the com.unity.simviz package to the `upm-candidates` registry. To see which versions of the package have been published, see 
  * https://artifactory.prd.cds.internal.unity3d.com/artifactory/webapp/#/artifacts/browse/tree/General/upm-candidates/com.unity.simviz
  * https://bintray.com/unity
