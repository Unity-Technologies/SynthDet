using UnityEngine;

public class SunAngleController : MonoBehaviour
{
    public SunAngle sunAngle;

    private Quaternion earthSpin, earthTilt, earthLat;
    
    [Tooltip("Uses serialized parameters to initialize sun angle when this script is enabled")]
    public bool initializeOnEnable = true;
    
    public float Hour
    {
        get => sunAngle.hour;
        set
        {
            sunAngle.hour = Mathf.Repeat(value, 24);
            earthSpin = Quaternion.AngleAxis(((sunAngle.hour + 12f) / 24f) * 360f, Vector3.down);
        }
    }

    public float TimeOfYear
    {
        get => sunAngle.timeOfYear;
        set
        {
            sunAngle.timeOfYear = value;
            float timeOfYearRads = sunAngle.timeOfYear * Mathf.PI * 2f;
            earthTilt = Quaternion.Euler(Mathf.Cos(timeOfYearRads) * 23.5f, 0, Mathf.Sin(timeOfYearRads) * 23.5f);
        }
    }
    
    public float Latitude
    {
        get => sunAngle.latitude;
        set
        {
            sunAngle.latitude = LoopValue(-90, 90, value);
            earthLat = Quaternion.AngleAxis(sunAngle.latitude, Vector3.right);
        }
    }

    /*
     * Loops the value t, so that it is never larger
     * than max and never smaller than min.
     */
    private float LoopValue(float min, float max, float t)
    {
        return Mathf.Repeat(t - min, max - min) + min;
    }

    public void SetSunAngle()
    {
        earthSpin = Quaternion.AngleAxis(((sunAngle.hour + 12f) / 24f) * 360f, Vector3.down);
        float timeOfYearRads = sunAngle.timeOfYear * Mathf.PI * 2f;
        earthTilt = Quaternion.Euler(Mathf.Cos(timeOfYearRads) * 23.5f, 0, Mathf.Sin(timeOfYearRads) * 23.5f);
        earthLat = Quaternion.AngleAxis(sunAngle.latitude, Vector3.right);
        Quaternion lightRotation = earthTilt * earthSpin * earthLat;
        transform.rotation = Quaternion.Euler(90,0,0) * Quaternion.Inverse(lightRotation);
    }

    private void OnValidate()
    {
        if (sunAngle != null)
        {
            SetSunAngle();
        }
    }
    
    private void OnEnable()
    {
        if (sunAngle != null)
        {
            Hour = sunAngle.hour;
            TimeOfYear = sunAngle.timeOfYear;
            Latitude = sunAngle.latitude;
            if (initializeOnEnable) SetSunAngle();
        }
    }
}
