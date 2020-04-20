using UnityEngine;

using Unity.Simulation;

using NUnit.Framework;
using NUnit.Framework.Internal;

public class FormatTests
{
    [Test]
    public void FormatFloatsProducesExpectedOutput()
    {
        var floats = new float[]
        {
            0.5168283117f,
            0.1059779524f,
            0.6119241998f,
            0.3220131802f,
            0.5126545982f,
            0.1220944873f,
            0.7932604766f,
            0.8110761667f,
            0.0694901928f,
            0.3618201420f,
        };

        var precision2 = Format.Floats(floats, 2, 2);

        var expected = "0.52, 0.11, \n0.61, 0.32, \n0.51, 0.12, \n0.79, 0.81, \n0.07, 0.36\n";
        Debug.Assert(expected.Equals(precision2), $"FormatFloatsProducesExpectedOutput expected\n{expected}\nbut got\n{precision2}\n");
    }
}
