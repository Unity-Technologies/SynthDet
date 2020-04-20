using System;
using Malee;

namespace UnityEngine.SimViz.Scenarios
{
    [Serializable]
    public class ParameterSetList : ReorderableArray<ParameterSet> { }

    [Serializable]
    public class ParameterSelector
    {
        [Reorderable(sortable = false)]
        public ParameterSetList parameterSetList = new ParameterSetList();

        public static explicit operator ParameterSet[](ParameterSelector selector)
        {
            return selector.parameterSetList.ToArray();
        }
    }

    [Serializable]
    public class ParameterSelectorList : ReorderableArray<ParameterSelector>
    {
    }

    [Serializable]
    public class ScenarioAppParams
    {
        public ScenarioAsset scenarioAsset;
    }

    [Serializable]
    public class SceneList : ReorderableArray<SceneReference> { }

    [CreateAssetMenu]
    public class ScenarioAsset : ExpandableScriptableObject
    {
        [Reorderable(sortable = false)]
        public SceneList scenes = new SceneList();
        [Reorderable(sortable = false)]
        public ParameterSelectorList parameterSelectors = new ParameterSelectorList();

        public static explicit operator ScenarioAppParams(ScenarioAsset scenarioAsset)
        {
            return new ScenarioAppParams()
            {
                scenarioAsset = scenarioAsset,
            };
        }
    }
}
