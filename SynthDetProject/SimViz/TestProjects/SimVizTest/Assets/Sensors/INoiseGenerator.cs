using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Syncity.Sensors
{
	public interface INoiseGenerator<T>
	{
		T Generate(T cleanValue);
	}
}