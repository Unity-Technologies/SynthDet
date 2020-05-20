# Object Detection Demo
This is a simple python script that will load the model you've trained using this project, fetch images from a 
connected webcam, and attempt to detect objects within the image.

## Environment Setup

1. Install a Python 3.7 environment using your preferred method. We are partial to 
[miniconda](https://docs.conda.io/en/latest/miniconda.html).
2. Install the requirements file with pip:\
`pip install opencv-python yacs torch==1.4.0 torchvision==0.5.0 -f https://download.pytorch.org/whl/torch_stable.html`

## Using the Script
1. Add the list of your class labels to `object_detector/class_labels.py`
2. Edit `run_demo.py` and modify the `MODEL` and `LABELS` variables to use your model and labels.
3. Do `python run_demo.py` inside your python environment. After a short delay, you should see a window open up with
your webcam stream and boxes draw around any detected objects in the stream.
