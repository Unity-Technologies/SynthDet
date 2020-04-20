using System;
using System.Collections;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

using Unity.Simulation;

using UnityEngine.TestTools;
using NUnit.Framework;

public class ImageFormatTests
{
#if !UNITY_EDITOR && UNITY_STANDALONE
    [Test]
    public void ImageFormat_EncodeJPG_DecodeJPG_ProducesSimilarData()
    {
        const int kDimension = 64;
        const int kLength = kDimension * kDimension;
        const int kDeviation = 3;

        var color = new Color32((byte)UnityEngine.Random.Range(0, 255), (byte)UnityEngine.Random.Range(0, 255), (byte)UnityEngine.Random.Range(0, 255), 255);

        var data = ArrayUtilities.Allocate<Color32>(kLength);
        for (var i = 0; i < kLength; ++i)
            data[i] = color;

        var encoded = JpegEncoder.Encode(ArrayUtilities.Cast<byte>(data), kDimension, kDimension, GraphicsUtilities.GetBlockSize(GraphicsFormat.R8G8B8A8_UNorm), GraphicsFormat.R8G8B8A8_UNorm);

        int width = 0, height = 0;
        var decoded = ArrayUtilities.Cast<Color32>(JpegEncoder.Decode(encoded, ref width, ref height));

        Debug.Assert(width == kDimension && height == kDimension);
        Debug.Assert(ArrayUtilities.Count<Color32>(data) == ArrayUtilities.Count<Color32>(decoded));

        int count = 0;
        for (var i = 0; i < kLength; ++i)
        {
            int rd = Math.Abs((int)data[i].r - (int)decoded[i].r);
            int gd = Math.Abs((int)data[i].g - (int)decoded[i].g);
            int bd = Math.Abs((int)data[i].b - (int)decoded[i].b);
            int ad = Math.Abs((int)data[i].a - (int)decoded[i].a);
            if (rd > kDeviation || gd > kDeviation || bd > kDeviation || ad > kDeviation)
                ++count;
        }

        Debug.AssertFormat(count == 0, "{0} pixels had deviation of {1} or more from original data.", count, kDeviation);
    }
#endif
}
