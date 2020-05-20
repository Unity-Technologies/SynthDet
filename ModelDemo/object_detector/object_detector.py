import logging
from collections import namedtuple
import time

import cv2
import torch
import torchvision

# Simple container to hold our detections in an organized way
Detection = namedtuple("detection", ["label", "box", "score"])


class ObjectDetector:
    def __init__(self, model_path: str, labels: list, detection_threshold: float, num_frames_to_detect: int):
        """
        :param model_path: file path to the model to load with torchvision
        :param labels: ordered list of class names whose indices correspond to the labels the model will output
        :param detection_threshold: confidence threshold to filter which detections should be drawn
        :param num_frames_to_detect: number of frames to process before quitting, set to -1 to run indefinitely
        """
        self.log = logging.getLogger()
        logging.basicConfig()
        self.log.setLevel(logging.INFO)
        self.camera = cv2.VideoCapture(0)
        num_classes = len(labels)
        self.log.info(f"Loading model from {model_path} with {num_classes} labels...")
        model_save = torch.load(model_path)
        self.log.debug(f"System config: {model_save['config']['system']}")
        if model_save['config']['num_classes'] != num_classes:
            raise ValueError(f"Number of labels ({len(labels)}) does not match number in {model_path} "
                             f"({model_save['config']['num_classes']}).")
        self.model = torchvision.models.detection.fasterrcnn_resnet50_fpn(pretrained=False, num_classes=num_classes)
        self.model.load_state_dict(model_save['model'])
        self.model.eval()
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        self.model.to(self.device)
        self.transform = torchvision.transforms.Compose([
            torchvision.transforms.ToTensor()
        ])
        self.labels = labels
        # NOTE: Use this instead for smaller, but less descriptive labels:
        # self.labels = [label.split("_")[0] for label in labels]
        self.threshold = detection_threshold
        self.num_frames_to_do = num_frames_to_detect
        self.num_frames_done = 0
        self.log.info(f"Model loaded. Ready to start.")

    def _detect_objects(self, img):
        tensor = self._img_to_tensor(img)
        inferences = self.model([tensor])
        if len(inferences) == 0:
            self.log.warning(f"No inferences generated for frame {self.num_frames_done}")
            return []
        # If running on cuda, we need to call cpu() to copy objects back into cpu-readable memory
        if self.device == torch.device("cuda"):
            labels = [self.labels[i] for i in list(inferences[0]['labels'].cpu().numpy()) if i < len(self.labels)]
            boxes = [[(b[0], b[1]), (b[2], b[3])] for b in (inferences[0]['boxes'].detach().cpu().numpy())]
            scores = inferences[0]['scores'].detach().cpu().numpy()
        else:
            labels = [self.labels[i] for i in list(inferences[0]['labels'].numpy()) if i < len(self.labels)]
            boxes = [[(b[0], b[1]), (b[2], b[3])] for b in (inferences[0]['boxes'].detach().numpy())]
            scores = inferences[0]['scores'].detach().numpy()
        # This problem occurs most often when the labels specified don't match those used in the model
        if len(labels) != len(boxes) or len(labels) != len(scores):
            self.log.error("Length of labels, boxes, and scores should all be the same but were " +
                           f"{len(labels)}, {len(boxes)}, and {len(scores)}.")
            return []

        detections = [Detection(labels[i], boxes[i], scores[i])
                      for i in range(len(scores)) if scores[i] > self.threshold]
        return detections

    def _draw_boxes_and_labels(self, img, detections):
        if len(detections) == 0:
            self.log.debug(f"No objects detected within threshold {self.threshold:0.2f} this frame")
        for label, box, score in detections:
            thickness_rectangle = 1
            cv2.rectangle(img, box[0], box[1], color=(0, 255, 0), thickness=thickness_rectangle)
            text = f"{label}({score:0.2f})"
            font = cv2.FONT_HERSHEY_SIMPLEX
            width_text, height_text = cv2.getTextSize(text, font, 0.5, 1)[0]
            height_img, width_img = img.shape[:2]
            # The class text may print off the end of the image, so shift it back into the frame
            x_adjusted = int(min(box[0][0], width_img - width_text))
            # Bump text to one pixel above the rectangle to make it more readable
            y_adjusted = int(max(height_text, box[0][1] - thickness_rectangle - 1))
            # NOTE: For a sufficiently small image and a sufficiently large string, this can still go off the left side
            origin_text = (x_adjusted, y_adjusted)
            cv2.putText(img, text, origin_text, font, 0.5, color=(0, 255, 0), thickness=1)
        return img

    def _img_to_tensor(self, img):
        img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        tensor = self.transform(img)
        return tensor.to(self.device)

    def _should_keep_running(self):
        run_forever = self.num_frames_to_do == -1
        escape_pressed = cv2.waitKey(1) == 27
        more_frames_to_run = self.num_frames_done < self.num_frames_to_do
        return (run_forever and not escape_pressed) or more_frames_to_run

    def run(self):
        while self._should_keep_running():
            self.num_frames_done += 1
            time_start = time.time()
            _, img = self.camera.read()
            time_camera = time.time()
            self.log.debug(f"Took {(time_camera - time_start):0.3f} seconds to read camera")
            labelled_boxes = self._detect_objects(img)
            time_detection = time.time()
            self.log.debug(f"Took {(time_detection - time_camera):0.3f} seconds to detect objects in image.")
            img_labeled = self._draw_boxes_and_labels(img, labelled_boxes)
            cv2.imshow(f'Demo', img_labeled)
            time_draw = time.time()
            self.log.debug(f"Took {(time_draw - time_detection):0.3f} seconds to draw labels and boxes")
        cv2.destroyAllWindows()
