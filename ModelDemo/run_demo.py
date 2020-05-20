import object_detector

# Set your model path and label list here...
MODEL = "./models/FasterRCNN.estimator"
# List of labels associated with the model you want to use
LABELS = object_detector.class_labels.GROCERY_LIST_V0
# Detections over this confidence threshold will be drawn to screen
THRESHOLD = 0.90
# Number of frames to run program for, set to -1 to run until ESC is pressed
FRAMES_TO_RUN = -1


if __name__ == '__main__':
    detector = object_detector.ObjectDetector(model_path=MODEL, labels=LABELS,
                                              detection_threshold=THRESHOLD, num_frames_to_detect=FRAMES_TO_RUN)
    detector.run()
