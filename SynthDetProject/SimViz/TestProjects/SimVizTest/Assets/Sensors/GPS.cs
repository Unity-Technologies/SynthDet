using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;

namespace Syncity.Sensors
{
	[Serializable]
	public class UnityEventGPSPosition : UnityEvent<GPSPosition> {}
	[Serializable]
	public class UnityEventString : UnityEvent<string> {}
	
	[Serializable]
	public struct GPSPosition
	{
		public float latitude;
		public float longitude;
		public float height;

		public GPSPosition(float latitude, float longitude, float height)
		{
			this.latitude = latitude;
			this.longitude = longitude;
			this.height = height;
		}

		public override string ToString()
		{
			return $"lat: {latitude}, lon: {longitude}, h: {height}";
		}

		public override bool Equals(object obj)
		{
			if (obj is GPSPosition)
			{
				return Equals((GPSPosition)obj);
			}
			return base.Equals(obj);
		}

		bool Equals(GPSPosition other)
		{
			return latitude.Equals(other.latitude) && longitude.Equals(other.longitude) && height.Equals(other.height);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = latitude.GetHashCode();
				hashCode = (hashCode * 397) ^ longitude.GetHashCode();
				hashCode = (hashCode * 397) ^ height.GetHashCode();
				return hashCode;
			}
		}

		public TDM latitudeInDM => new TDM(latitude);
		public TDM longitudeInDM => new TDM(longitude);

		public struct TDM
		{
			public readonly uint degrees;
			public readonly float minutes;
			public readonly bool negative;
			public TDM (float value)
			{
				degrees = (uint)Mathf.FloorToInt(Math.Abs(value));
				minutes = (Mathf.Abs(value) - degrees) * 60;
				negative = value < 0;
			}

			public override string ToString()
			{
				return $"{degrees}.{minutes.ToString(CultureInfo.InvariantCulture)} {(negative ? "-" : "+")}";
			}
		}
	}
	
	public class GPS : MonoBehaviour
	{
		// https://en.wikipedia.org/wiki/Haversine_formula
		const float earthRadius = 6378;

		public GPSPosition gpsPosition
		{
			get
			{
				var currentPosition = transform.position;
				if (noiseGenerator != null)
				{
					currentPosition += noiseGenerator.Generate(currentPosition);
				}
				currentPosition*= GPSOrigin.instance.metersPerUnit / 1000f; // formula is in kms

				var latitude = GPSOrigin.instance.zeroPosition.latitude + (currentPosition.z / earthRadius) * (180f / Mathf.PI);
				var longitude = GPSOrigin.instance.zeroPosition.longitude + (currentPosition.x / earthRadius) * (180f / Mathf.PI) /
								   Mathf.Cos(GPSOrigin.instance.zeroPosition.latitude * Mathf.PI / 180f);
				var height = GPSOrigin.instance.zeroPosition.height  * GPSOrigin.instance.metersPerUnit + currentPosition.y;
	
				return new GPSPosition(latitude, longitude, height);
			}
		}
		public UnityEventGPSPosition onGPSPosition;
		public UnityEventString onNMEA;

		public float speedInKnots => speedInMps * 1.94384f;
		public float speedInKph => speedInMps * 3.6f;
		public float speedInMps { get; private set; }
		
		[SerializeField]
		ScriptableObject _noiseGenerator;
		public INoiseGenerator<Vector3> noiseGenerator
		{
			get { return _noiseGenerator as INoiseGenerator<Vector3>; }
			set { _noiseGenerator = value as ScriptableObject; }
		}

		Vector3 lastPosition = Vector3.zero;
		void FixedUpdate()
		{
			var currentPosition = transform.position;
			if (noiseGenerator != null)
			{
				currentPosition += noiseGenerator.Generate(currentPosition);
			}

			float d = (lastPosition - currentPosition).magnitude;

			speedInMps = d / Time.fixedDeltaTime; 
			
			lastPosition = currentPosition;
		}

		void Update()
		{
			onGPSPosition?.Invoke(gpsPosition);
			onNMEA?.Invoke(GPRMC);
			onNMEA?.Invoke(GPGGA);
		}

		public string GPRMC => GenerateGPRMC();
		string GenerateGPRMC()
		{
			var lat = gpsPosition.latitudeInDM;
			var lon = gpsPosition.longitudeInDM;

			var heading = ClampAngle(transform.eulerAngles.y + 90f);
			
			var ret = $"GPRMC," +
			          $"{DateTime.Now:HHmmss}," +
			          $"A," + // validity - A-ok, V-invalid
			          $"{lat.degrees:00}{lat.minutes.ToString("00.######", CultureInfo.InvariantCulture)},{(lat.negative ? "S" :"N")}," +
			          $"{lon.degrees:000}{lon.minutes.ToString("00.######", CultureInfo.InvariantCulture)},{(lon.negative ? "W" :"E")}," +
			          $"{speedInKnots}," +
			          $"{heading.ToString(CultureInfo.InvariantCulture)}," +
			          $"{DateTime.Now:ddMMyy}," +
			          $","; // magnetic variation

			return $"${ret}*{CalculateChecksum(ret)}";
		}
		
		public string GPGGA => GenerateGPGGA();
		string GenerateGPGGA()
		{
			var lat = gpsPosition.latitudeInDM;
			var lon = gpsPosition.longitudeInDM;

			var ret = $"GPGGA," +
			          $"{DateTime.Now:HHmmss.ff}," +
			          $"{lat.degrees:00}{lat.minutes.ToString("00.######", CultureInfo.InvariantCulture)},{(lat.negative ? "S" : "N")}," +
			          $"{lon.degrees:000}{lon.minutes.ToString("00.######", CultureInfo.InvariantCulture)},{(lon.negative ? "W" : "E")}," +
			          $"0," + // Fix Quality: 0 = Invalid, 1 = GPS fix, 2 = DGPS fix	
			          $"00," + // Number of Satellites
			          $"," + // Horizontal Dilution of Precision (HDOP)
			          $"{gpsPosition.height:0.0}," + // Antenna altitude above/below mean sea level (geoid)
			          $"M," + // Meters  (Units of geoidal separation)
			          $"," + // Height of geoid above WGS84 ellipsoid
			          $"M," + // Meters  (Units of geoidal separation)
			          $"," + // Geoidal separation (Diff. between WGS-84 earth ellipsoid and mean sea level.  -=geoid is below WGS-84 ellipsoid)
			          $"," + // Age in seconds since last update from diff. reference station
			          $""; // Diff. reference station ID#
				
			return $"${ret}*{CalculateChecksum(ret)}";
		}
		
		float ClampAngle(float angle) 
		{
			while (angle >= 360f)
			{
				angle -= 360f;
			}
			while (angle < 0f)
			{
				angle += 360f;
			}

			return angle;
		}

		string CalculateChecksum(string str)
		{
			byte ret = 0;
			foreach (var c in str)
			{
				ret ^= (byte)c;
			}

			return ret.ToString("X2");
		}
	}
}