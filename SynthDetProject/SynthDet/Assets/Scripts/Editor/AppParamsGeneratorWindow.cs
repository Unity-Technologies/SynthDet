using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Simulation.Client;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class AppParamsGeneratorWindow : UnityEditor.EditorWindow
{
    [UnityEditor.MenuItem("Window/Run in USim...")]
    private static void ShowWindow()
    {
        var window = GetWindow<AppParamsGeneratorWindow>();
        window.titleContent = new UnityEngine.GUIContent("USim run");
        window.Show();
    }
    TextField m_NameField;
    TextField m_BuildZipPathField;
    
    PopupField<SysParamOption> m_SysParamPopup;
    IntegerField m_StepsField;
    IntegerField m_StepsPerJobField;
    CurveField m_ScaleFactorCurve;
    IntegerField m_MaxFramesField;
    TextElement m_EstimateElement;

    class SysParamOption
    {
        public string id;
        public string description;

        public override string ToString() => description;
    };
    
    public void OnEnable()
    {
        Project.Activate();
        
        m_NameField = new TextField("Run name")
        {
            viewDataKey = "Run name",
        };
        rootVisualElement.Add(m_NameField);
        m_BuildZipPathField = new TextField("Path to player build .zip")
        {
            viewDataKey = "Path to .zip"
        };
        rootVisualElement.Add(m_BuildZipPathField);
        m_ScaleFactorCurve = new CurveField("Scale factor range")
        {
            viewDataKey = "Scale factor range",
            ranges = new Rect(0, 0, 1, -1)
        };
        m_ScaleFactorCurve.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_ScaleFactorCurve);
        
        m_StepsField = new IntegerField("Scale factor steps")
        {
            value = 100,
            viewDataKey = "Scale factor steps",
            maxLength = 4
        };
        m_StepsField.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_StepsField);
        m_StepsPerJobField = new IntegerField("Steps per instance")
        {
            value = 2,
            viewDataKey = "Steps per instance",
            maxLength = 3
        };
        m_StepsPerJobField.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_StepsPerJobField);
        m_MaxFramesField = new IntegerField("Max frames per instance")
        {
            value = 50000,
            viewDataKey = "Max frames"
        };
        m_MaxFramesField.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_MaxFramesField);

        m_EstimateElement = new TextElement();
        UpdateEstimate();
        rootVisualElement.Add(m_EstimateElement);

        SysParamDefinition[] sysParamDefinitions;
        try
        {
            sysParamDefinitions = API.GetSysParams();
        }
        catch (Exception e)
        {
            this.Close();
            throw;
        }
        
        var sysParamOptions = sysParamDefinitions.Where(s => s.allowed)
            .Select(s => new SysParamOption()
            {
                id = s.id,
                description = s.description
            }).ToList();
        m_SysParamPopup = new PopupField<SysParamOption>("SysParam", sysParamOptions, sysParamOptions[0])
        {
            viewDataKey = "SysParam"
        };
        rootVisualElement.Add(m_SysParamPopup);
        rootVisualElement.Add(new Button(OnBeginRunButtonClicked){ text = "Execute on Unity Simulation"});
    }

    void UpdateEstimate()
    {
        m_EstimateElement.text = $"Estimated total frames: {EstimateTotalFrames()}";
    }

    //A hand-tuned value that approximately corresponds to the frames per scale factor per orientation with 64 foreground objects.
    const float estimatedFramesPerCurriculumStepAtOneScale = 4.2f;
    
    public int EstimateTotalFrames()
    {
        var inPlaneRotations = ObjectPlacementUtilities.GenerateInPlaneRotationCurriculum(Allocator.Temp);
        var outOfPlaneRotations = ObjectPlacementUtilities.GenerateOutOfPlaneRotationCurriculum(Allocator.Temp);
        
        var framesPerScaleFactorAtOneScale = estimatedFramesPerCurriculumStepAtOneScale * inPlaneRotations.Length * outOfPlaneRotations.Length;

        inPlaneRotations.Dispose();
        outOfPlaneRotations.Dispose();

        var estimate = 0;
        
        var scaleFactorRange = m_ScaleFactorCurve.value;
        var steps = m_StepsField.value;
        var stepsPerJob = m_StepsPerJobField.value;
        var maxFramesPerJob = m_MaxFramesField.value;

        if (steps == 0 || maxFramesPerJob == 0 || stepsPerJob == 0)
            return 0;
        
        float stepSize = 1f / (steps + 1);
        float time = 0;

        for (int i = 0; i < steps; i++)
        {
            var scaleFactor = scaleFactorRange.Evaluate(time);
            estimate += (int)(framesPerScaleFactorAtOneScale * (scaleFactor * scaleFactor));
            time += stepSize;
        }

        var maxFrames = (m_StepsField.value / stepsPerJob) * maxFramesPerJob;

        return math.min(maxFrames, estimate);
    }

    void OnBeginRunButtonClicked()
    {
        var scaleFactorRange = m_ScaleFactorCurve.value;
        var steps = m_StepsField.value;
        var stepsPerJob = m_StepsPerJobField.value;
        var run = USimRunner.StartUSimRun(m_NameField.value, m_SysParamPopup.value.id, m_BuildZipPathField.value, scaleFactorRange, steps, stepsPerJob, m_MaxFramesField.value);
        Debug.Log("USim run started. Execution id " + run.executionId);
    }
}
