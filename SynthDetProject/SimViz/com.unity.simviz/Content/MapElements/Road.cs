using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Serialization;

namespace UnityEngine.SimViz.Content.MapElements
{

    [Serializable]
    public struct RoadLink
    {
        public string nodeId;
        public LinkContactPoint contactPoint;
        public RoadLinkType linkType;

        public RoadLink(string id, LinkContactPoint contact, RoadLinkType linkType)
        {
            nodeId = id;
            contactPoint = contact;
            this.linkType = linkType;
        }
    }

    [Serializable]
    [InternalBufferCapacity(8)]
    public struct RoadElevationProfile : IBufferElementData
    {
        public float s;
        public float a;
        public float b;
        public float c;
        public float d;

        public RoadElevationProfile(float s, float a, float b, float c, float d)
        {
            this.s = s;
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
        }
    }

    // TODO: Refactor to replace any NativeString64/string instances of road IDs with this wrapper
    public struct RoadId : IBufferElementData
    {
        public static implicit operator NativeString64(RoadId e) { return e.Value; }
        public static implicit operator RoadId(NativeString64 e) { return new RoadId { Value = e }; }

        // Actual value each buffer element will store.
        public NativeString64 Value;
    }

    [Serializable]
    public struct Road
    {
        public string roadId;
        // NOTE: IDE is suggesting CapitalCase names here because these aren't public members?
        public float length;// { get; }
        public string junction; // { get; }
        public string name; // { get; }
        // List of elements specified by the planView block of each road element
        public List<Geometry> geometry;
        public List<LaneOffset> laneOffsets;
        [FormerlySerializedAs("lanes")] public List<LaneSection> laneSections;
        public List<RoadElevationProfile> elevationProfiles;
        public RoadLink predecessor;
        public RoadLink successor;

        // TODO: rule, type, elevationProfile, lateralProfile, objects, signals, surface, railroad
        public Road(
            string roadId,
            float length,
            string junction,
            List<Geometry> geometry,
            List<LaneSection> laneSections,
            List<RoadElevationProfile> elevationProfiles,
            List<LaneOffset> laneOffsets,
            RoadLink predecessor,
            RoadLink successor,
            string name="")
        {
            this.length = length;
            this.junction = junction;
            this.geometry = geometry;
            this.laneSections = laneSections;
            this.elevationProfiles = elevationProfiles;
            this.laneOffsets = laneOffsets;
            this.predecessor = predecessor;
            this.successor = successor;
            this.name = name;
            this.roadId = roadId;
        }
    }
}
