using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundGeneratorParameters : MonoBehaviour
{
    public bool SystemEnabled = false;
    // Number of cells to create per foreground object for placing background objects
    [Range(1, 9)]
    public int ObjectDensity = 4;
    // The number of times the background generator will place an object in each "cell"
    [Range(0, 5)]
    public int NumFillPasses = 1;

    // For debugging only - the amount of frames to wait in between each new background generation
    [Range(0, 30)] 
    public int PauseBetweenFrames = 0;
}
