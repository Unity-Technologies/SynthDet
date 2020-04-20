#if HDRP_PRESENT

using UnityEditor;
using UnityEditor.Rendering.HighDefinition;

namespace UnityEngine.SimViz.Sensors.Editor
{
    [CustomPassDrawer(typeof(SegmentationPass))]
    public class SegmentationPassEditor : BaseCustomPassDrawer
    {
        protected override void Initialize(SerializedProperty customPass)
        {
            var targetCameraProperty = customPass.FindPropertyRelative(nameof(GroundTruthPass.targetCamera));
            AddProperty(targetCameraProperty);
            AddProperty(customPass.FindPropertyRelative(nameof(SegmentationPass.targetTexture)));
            AddProperty(customPass.FindPropertyRelative(nameof(SegmentationPass.reassignIds)));
            AddProperty(customPass.FindPropertyRelative(nameof(SegmentationPass.idStart)));
            AddProperty(customPass.FindPropertyRelative(nameof(SegmentationPass.idStep)));
            base.Initialize(customPass);
        }
    }
}
#endif