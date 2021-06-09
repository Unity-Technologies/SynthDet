using System;
using System.Collections.Generic;
using System.Linq;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;

namespace SynthDet.Randomizers
{
    [Serializable]
    [AddRandomizerMenu("SynthDet/Active Object Switcher")]
    public class ActiveObjectSwitcher : Randomizer
    {
        int m_FrameInIteration;
        List<ObjectSwitcherTag> m_SwitcherTagsInIteration;
        protected override void OnIterationStart()
        {
            m_FrameInIteration = 0;
            m_SwitcherTagsInIteration = tagManager.Query<ObjectSwitcherTag>().ToList();
        }

        protected override void OnUpdate()
        {
            foreach (var tag in m_SwitcherTagsInIteration)
            {
                tag.gameObject.SetActive(false);
            }
            
            if (m_FrameInIteration < m_SwitcherTagsInIteration.Count)
            {
                m_SwitcherTagsInIteration[m_FrameInIteration].gameObject.SetActive(true);
            }

            m_FrameInIteration++;
        }
    }
}