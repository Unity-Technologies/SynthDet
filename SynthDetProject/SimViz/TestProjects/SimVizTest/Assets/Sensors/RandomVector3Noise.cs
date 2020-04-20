using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Syncity.Sensors
{
	public class RandomVector3Noise : ScriptableObject, INoiseGenerator<Vector3>
	{
		public float radius = 1;
		public Vector3 Generate(Vector3 cleanValue)
		{			
			return cleanValue + Random.insideUnitSphere * radius;
		}
	}
}