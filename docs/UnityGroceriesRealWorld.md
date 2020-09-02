# UnityGroceries-RealWorld Dataset

As part of this project, we collected a real world dataset containing 1267 images of the 63 target grocery items. This dataset is primarily used to assess the real-world performance of models trained on synthetic data with a model trained on real data. This dataset can be downloaded [here](https://storage.googleapis.com/datasetinsights/data/groceries/v3.zip).

## dataset collection

To take the photos for the UnityGroceries-RealWorld dataset, we devised a strategy to vary object selection, placement, lighting, and background. After a few test shoots, we realized that major barriers to randomness of placement and object selection were the actual humans doing the selecting and placing. Hence, planning on how to randomize while taking photos became highly important, and something we did before taking photos.

For our photography sessions, we gathered all sixty three grocery objects, and staked out five locations to take the photos. These were:

- A south facing large space with tall windows. This gave us strong daylight across the objects, and deep shadows.
- A west facing space with smaller windows, and an arrangement of furniture. This gave us bounced light and varied background colors and contours.
- A windowless space with variable lighting. This allowed us to create challenging one-sided and colored lighting.
- A second south facing space with partitions blocking the direct light. This gave us a blending of artificial and natural light, plus additional background objects.
- A north facing alcove, with white laminate cabinets and countertops. This gave us strong artificial lighting and featureless areas to place objects.

To make sure we were randomizing placement and combinations of objects for the photos, we gathered several willing volunteers to assist, with one person as the photographer. In each of the locations described above, we used the following strategy:

- Each volunteer started with eight objects, and placed them in different locales.
- Some objects were placed nicely, some were dropped or thrown lightly.
- The photographer would step around the space and take a picture of each volunteer’s arrangement, varying the angle each time.
- After the photo was taken, each volunteer would mix up the objects, hand placing some and dropping or lightly throwing others to create new arrangements.
- After the cycle was repeated eight times, each volunteer would hand an object to another volunteer, and receive one from a different volunteer.
- Once eight full cycles of object swapping and randomizing were completed, we gathered the groceries and moved to the next location to repeat the process.

To take the pictures for the UnityGroceries-RealWorld dataset, we used a Canon T5i camera with a standard 35mm lens and no filters. The images were shot at the native resolution of 5184x3456 pixels, and saved as JPEG files at maximum quality. For all of the shots, White Balance, ISO, aperture, and shutter speed were set to Automatic, and no flash was used. All of the shots were handheld, with the photographer bracing on furniture or floor for stability wherever possible.

## bounding box annotations

To create bounding box annotations for this dataset, we used [VGG Image Annotator (VIA)](http://www.robots.ox.ac.uk/~vgg/software/via/) tool, version `via-2.x.y`. Image annotators are required to draw tight bounding boxes for the objects that can be recognized among the 63 groceries categories. These annotation are reviewed by at-least one independent annotator to ensure quality of annotations.

We followed similar guidelines from [VOC](http://host.robots.ox.ac.uk/pascal/VOC/voc2011/guidelines.html) dataset. In addition to VOC guidelines, annotators are also required to follow these guidelines:

- If parts of the object are occluded, the bounding box should not extend to the occluded parts. We only draw bounding boxes that enclose visible parts of the object.
- If the object is split into 2 segments due occlusion, one should draw a bounding box that include both segments.
- If the approximate visibility of a given object is less than 10% due to edge cropping or occlusion from other objects, this object is excluded from annotations.
- If more than 30% of the objects in the image are blurred due to motion blur or camera focus, we regard this image as “unidentifiable”. These images are removed from the dataset.

## dataset splits

We randomly shuffle all annotated images and split the dataset into a training dataset of 760 (60%) images, a validation dataset of 253 (20%) images and a testing dataset of 254 (20%) images. We also created various randomly selected training subsets from the training dataset to demonstrate model fine-tuning using limited amounts of the training data. To compare model performance under different conditions, we split the testing dataset into subsets of different conditions for different evaluation purposes:

- We split the testing dataset into high/low contrast to test model performance in different lighting conditions. Images in high contrast split tend to have more complicated shadow patterns.
- We split the testing dataset into high/low foreground to background ratio, Objects in low ratio subset tend to have larger sizes than in high ratio subset.
