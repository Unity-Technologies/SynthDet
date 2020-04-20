#if HDRP_PRESENT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;

namespace UnityEngine.SimViz.Sensors.Editor
{
    [CustomPassDrawer(typeof(LabelHistogramPass))]
    public class LabelHistogramPassEditor : BaseCustomPassDrawer
    {
        protected override void Initialize(SerializedProperty customPass)
        {
            AddProperty(customPass.FindPropertyRelative(nameof(GroundTruthPass.targetCamera)));
            AddProperty(customPass.FindPropertyRelative(nameof(LabelHistogramPass.SegmentationTexture)));
            AddProperty(customPass.FindPropertyRelative(nameof(LabelHistogramPass.LabelingConfiguration)));
            base.Initialize(customPass);
        }
    }
}
#endif