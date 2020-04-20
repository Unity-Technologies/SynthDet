using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Syncity.Sensors
{
	public class RandomFloatNoise : ScriptableObject, INoiseGenerator<float>
	{
		public float min = 0, max = 1;
		public float Generate(float cleanValue)
		{			
			return cleanValue + Random.Range(min, max);
		}
	}
}