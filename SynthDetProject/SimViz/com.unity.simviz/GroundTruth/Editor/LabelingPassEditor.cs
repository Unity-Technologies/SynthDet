#if HDRP_PRESENT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;

namespace UnityEngine.SimViz.Sensors.Editor
{
    [CustomPassDrawer(typeof(LabelingPass))]
    public class LabelingPassEditor : BaseCustomPassDrawer
    {
        protected override void Initialize(SerializedProperty customPass)
        {
            AddProperty(customPass.FindPropertyRelative(nameof(GroundTruthPass.targetCamera)));
            AddProperty(customPass.FindPropertyRelative(nameof(LabelingPass.targetTexture)));
            AddProperty(customPass.FindPropertyRelative(nameof(LabelingPass.labelingConfiguration)));
            base.Initialize(customPass);
        }
    }
}
#endif