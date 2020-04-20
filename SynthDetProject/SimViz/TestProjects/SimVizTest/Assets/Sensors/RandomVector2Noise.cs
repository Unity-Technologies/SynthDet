using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Syncity.Sensors
{
	public class RandomVector2Noise : ScriptableObject, INoiseGenerator<Vector2>
	{
		public float radius = 1;
		public Vector2 Generate(Vector2 cleanValue)
		{			
			return cleanValue + Random.insideUnitCircle * radius;
		}
	}
}