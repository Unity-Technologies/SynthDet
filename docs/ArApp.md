# AR App Tool

## Hardware needed:
1. Webcam connected to your machine

## Modifying the AR app example: 
If you would like to change the clases or models that the app detects you can do so by modifying object_detector/class_labels.py script

## Step 1: AR Example application
1. In the SynthDet repo there is a folder nameed Model Demo that contains th python scripts needed for the next steps
2. Inside of a python environment run the cmd python run_demo.py
3. After a short delay, you should see a window open up with your webcam stream 
4. Hold up a cereal box or any other grocery item to your webcam and you should see boxes draw around any detected objects in the stream.