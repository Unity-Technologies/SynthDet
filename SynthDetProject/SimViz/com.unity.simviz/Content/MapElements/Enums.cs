using System;

namespace UnityEngine.SimViz.Content.MapElements
{
    [Serializable]
    public enum GeometryKind
    {
        Unknown,
        Line,
        Spiral,
        Arc,
        Poly3,
        ParamPoly3
    }

    [Serializable]
    public enum JunctionType
    {
        Default,
        Virtual
    }

    [Serializable]
    public enum RoadLinkType
    {
        None,
        Road,
        Junction
    }

    [Serializable]
    public enum LaneType
    {
        None,
        Driving,
        Stop,
        Shoulder,
        Biking,
        Sidewalk,
        Border,
        Restricted,
        Parking,
        Bidirectional,
        Median,
        Special1,
        Special2,
        Special3,
        RoadWorks,
        Tram,
        Rail,
        Entry,
        Exit,
        OffRamp,
        OnRamp,
        ConnectingRamp,
        Bus,
        Taxi,
        Hov
    }

    [Serializable]
    public enum LinkContactPoint
    {
        // No link was assigned (empty link)
        None,
        // An assignment was attempted, but failed to parse contact point from file
        Undefined,
        Start,
        End
    }

    [Serializable]
    public enum Poly3PRange
    {
        // pRange was introduced in 1.5 - older specs will not have them
        Unknown,
        ArcLength,
        Normalized,
        // For when the parameter p is equal to the input domain (p = u)
        Domain
    }
}