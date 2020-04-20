using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content
{
    internal class OpenDriveMapElementFactory : XmlParserBase
    {
        // Sections/Lanes of less length than this should be merged into neighbors as they represent an insignificant
        // portion of the overall road geometry
        private const float MinSectionLength = 1e-5f;
        
        internal static RigidTransform FromOpenDriveGlobalToUnity(float headingRadians, float x, float y)
        {
            var rotation = quaternion.RotateY(-headingRadians);
            // NOTE: In OpenDRIVE's global coordinate space, a heading of zero means moving in the positive x
            // direction. To map this to Unity's global coordinate space, where a heading of zero means moving along
            // positive z, we assign x to z and negate the openDRIVE y-axis and assign it to x
            var position = new float3(-y, 0, x);
            return new RigidTransform(rotation, position);
        }

        internal List<Geometry> ConstructGeometryList(IEnumerable<XElement> planView)
        {
            if (planView == null)
            {
                throw new ArgumentNullException();
            }

            if (!planView.Any())
            {
                Debug.LogWarning("Found no elements to parse in planView block.");
                return new List<Geometry>();
            }

            var allGeometry = new List<Geometry>();
            foreach (var geometry in planView.Elements())
            {
                // Assumes each geometry element is valid according to the schema (only one shape defined per element)
                var shape = geometry.Descendants().First();
                if (shape == null)
                {
                    Debug.LogWarning("No shape found in geometry element's descendants: " + geometry.Name);
                    continue;
                }

                var s = ReadFloat(geometry, "s");
                var x = ReadFloat(geometry, "x");
                var y = ReadFloat(geometry, "y");
                var hdgRadians = ReadFloat(geometry, "hdg");
                var length = ReadFloat(geometry, "length");
                var newGeometry = new Geometry()
                {
                    length = length,
                    sRoad = s,
                    startPose = FromOpenDriveGlobalToUnity(hdgRadians, x, y)
                };

                if (!Enum.TryParse(shape.Name.ToString(), true, out newGeometry.geometryKind))
                {
                    LogFailedParseError(shape, "name", shape.Name.ToString(), "GeometryKind");
                    continue;
                }

                switch (newGeometry.geometryKind)
                {
                    // NOTE: Curvatures are not inverted because they are defined in inertial space, which is right-handed
                    case GeometryKind.Arc:
                        var curvature = ReadFloat(shape, "curvature");
                        newGeometry.arcData = new ArcData(curvature);
                        break;
                    case GeometryKind.Spiral:
                        var curveStart = ReadFloat(shape, "curvStart");
                        var curveEnd = ReadFloat(shape, "curvEnd");
                        newGeometry.spiralData = new SpiralData(curveStart, curveEnd);
                        break;
                    // TODO: AISV-161 Remove Poly3Data entirely - parse in Poly3's as ParamPoly3 instead
                    case GeometryKind.Poly3:
                        var a = ReadFloat(shape, "a");
                        var b = ReadFloat(shape, "b");
                        var c = ReadFloat(shape, "c");
                        var d = ReadFloat(shape, "d");
                        newGeometry.poly3Data = new Poly3Data(new Vector4(a, b, c, d));
                        break;
                    case GeometryKind.Line:
                        // The base construction of the geometry already has all the relevant data for Line
                        break;
                    case GeometryKind.ParamPoly3:
                        var aU = ReadFloat(shape, "aU");
                        var bU = ReadFloat(shape, "bU");
                        var cU = ReadFloat(shape, "cU");
                        var dU = ReadFloat(shape, "dU");
                        var aV = ReadFloat(shape, "aV");
                        var bV = ReadFloat(shape, "bV");
                        var cV = ReadFloat(shape, "cV");
                        var dV = ReadFloat(shape, "dV");
                        var u = new Vector4(aU, bU, cU, dU);
                        var v = new Vector4(aV, bV, cV, dV);
                        // NOTE: Technically this is required as of rev 1.5 - but not in older files...
                        var pRange = ReadEnumOrDefault<Poly3PRange>(shape, "pRange", Poly3PRange.Unknown);
                        newGeometry.paramPoly3Data = new ParamPoly3Data(u, v, pRange);
                        break;
                    default:
                        // XXX: Exception here? this should be un-reachable
                        newGeometry.geometryKind = GeometryKind.Unknown;
                        Debug.LogError("Don't know how to handle geometry element named " + shape.Name);
                        break;
                }

                allGeometry.Add(newGeometry);
            }
            
            // Ensure entries are in order by distance along reference line
            return allGeometry.OrderBy(geometry => geometry.sRoad).ToList();
        }
        
        internal Junction ConstructJunction(XElement openDriveJunction)
        {
            var name = ReadStringOrDefault(openDriveJunction, "name", "");
            var id = ReadString(openDriveJunction, "id");
            var elements = openDriveJunction.Elements();
            var connections = new List<JunctionConnection>();
            foreach (var element in elements)
            {
                var elementName = element.Name.ToString();
                switch (elementName)
                {
                    case "connection":
                        connections.Add(ConstructJunctionConnection(element));
                        break;
                    default:
                        LogUnsupportedElementWarning(elementName);
                        break;
                }
            }
            
            return new Junction(name, id, connections);
        }

        internal JunctionConnection ConstructJunctionConnection(XElement connectionElement)
        {
            var id = ReadString(connectionElement, "id");
            var incomingRoad = ReadString(connectionElement, "incomingRoad");
            var connectingRoad = ReadString(connectionElement, "connectingRoad");
            var contact = ReadEnum<LinkContactPoint>(connectionElement, "contactPoint");

            var links = connectionElement.Elements("laneLink").Select(ConstructJunctionLaneLink).ToList();

            return new JunctionConnection(id, incomingRoad, connectingRoad, contact, links);
        }
        
        internal JunctionLaneLink ConstructJunctionLaneLink(XElement laneLinkElement)
        {
            var from = ReadInt(laneLinkElement, "from");
            var to = ReadInt(laneLinkElement, "to");
            return new JunctionLaneLink(from, to);
        }

        internal Lane ConstructLaneSectionInner(XElement innerLaneSection)
        {
            var id = ReadInt(innerLaneSection, "id");
            var laneType = ReadEnum<LaneType>(innerLaneSection, "type");
            var links = innerLaneSection.Element("link");
            var laneLink = links == null ? new LaneSectionLink() : ConstructLaneSectionLink(links);
            return new Lane(id, laneType, laneLink);
        }

        internal LaneSection ConstructLaneSection(XElement laneSectionElement)
        {
            var isSingleSided = ReadBoolOrDefault(laneSectionElement, "singleSide");
            if (isSingleSided)
            {
                LogUnsupportedElementWarning("singleSided LaneSection");
                return default;
            }

            var s = ReadFloat(laneSectionElement, "s");
            var leftUnsorted = laneSectionElement.Element("left")?.Elements().Select(ConstructLaneSectionOuter);
            var center = laneSectionElement.Element("center")?.Elements().Select(ConstructLaneSectionInner).First() ?? 
                         new Lane(0, default, default);
            var rightUnsorted = laneSectionElement.Element("right")?.Elements().Select(ConstructLaneSectionOuter);

            // Ensure that the elements are in middle-out order
            var leftSorted = leftUnsorted?.OrderBy(lane => lane.id).ToArray() ?? new Lane[0];
            var rightSorted = rightUnsorted?.OrderByDescending(lane => lane.id).ToArray() ?? new Lane[0];

            return new LaneSection(s, leftSorted, center, rightSorted);
        }

        internal LaneSectionLink ConstructLaneSectionLink(XElement laneLink)
        {
            var predecessors = laneLink.Elements("predecessor").Select(x => ReadInt(x, "id")).ToArray();
            var successors = laneLink.Elements("successor").Select(x => ReadInt(x, "id")).ToArray();
            
            return new LaneSectionLink(predecessors, successors);
        }

        internal Lane ConstructLaneSectionOuter(XElement outerLaneSection)
        {
            var id = ReadInt(outerLaneSection, "id");
            var laneType = ReadEnum<LaneType>(outerLaneSection, "type");
            var links = outerLaneSection.Element("link");
            var laneLink = links == null ? new LaneSectionLink() : ConstructLaneSectionLink(links);
            // Confusingly, the spec has two different names for width records, despite both holding the same values...
            var widthRecords = outerLaneSection.Elements("width").Select(ConstructLaneWidthRecord).Concat(
                outerLaneSection.Elements("border").Select(ConstructLaneWidthRecord)).ToArray();
            // TODO: Sort the width records ^
            return new Lane(id, laneType, laneLink, widthRecords);
        }

        internal LaneWidthRecord ConstructLaneWidthRecord(XElement laneWidthElement)
        {
            var isBorder = laneWidthElement.Name.ToString().ToLower() == "border";
            var s = ReadFloat(laneWidthElement, "sOffset");
            var a = ReadFloat(laneWidthElement, "a");
            var b = ReadFloat(laneWidthElement, "b");
            var c = ReadFloat(laneWidthElement, "c");
            var d = ReadFloat(laneWidthElement, "d");
            
            return new LaneWidthRecord(isBorder, s, a, b, c, d);
        }

        internal Road ConstructRoad(XElement openDriveRoad)
        {
            var name = ReadStringOrDefault(openDriveRoad, "name", "");
            var planViewElements = GetRequiredElements(openDriveRoad, "planView");
            var geometry = ConstructGeometryList(planViewElements);
            var lanesElement = GetRequiredElement(openDriveRoad, "lanes");
            var id = ReadString(openDriveRoad, "id");
            var length = ReadFloat(openDriveRoad, "length");
            var junction = ReadString(openDriveRoad, "junction");
            var lanesUnsorted = lanesElement.Elements("laneSection").Select(ConstructLaneSection);
            var lanesSorted = lanesUnsorted.OrderBy(laneSection => laneSection.sRoad).ToList();
            var laneOffsetsUnsorted = lanesElement.Elements("laneOffset").Select(ConstructLaneOffset);
            var laneOffsetsSorted = laneOffsetsUnsorted.OrderBy(laneOffset => laneOffset.sRoad).ToList();

            var link = openDriveRoad.Elements("link").FirstOrDefault();
            RoadLink predecessor;
            RoadLink successor;
            if (link == null)
            {
                predecessor = new RoadLink();
                successor = new RoadLink();
            }
            else
            {
                predecessor = ConstructRoadLink(link.Elements("predecessor"));
                successor = ConstructRoadLink(link.Elements("successor"));
            }

            var elevation = openDriveRoad.Element("elevationProfile");
            // TODO: Do these need to be sorted?
            List<RoadElevationProfile> elevationProfiles = null;
            if (elevation != null)
            {
                var profileElements = elevation.Elements("elevation").ToList();
                if (profileElements.Count == 0)
                {
                    LogWarningOnce("Elevation elements missing from elevationProfile block. " +
                                     "Ignoring for now, but this will be required for future files.");
                }

                elevationProfiles = profileElements.Select(ConstructRoadElevationProfile).ToList();
            }
            
            return new Road(id, length, junction, geometry, lanesSorted, elevationProfiles, laneOffsetsSorted, predecessor, successor, name: name);
        }

        internal RoadElevationProfile ConstructRoadElevationProfile(XElement profile)
        {
            var s = ReadPositiveFloat(profile, "s");
            var a = ReadFloat(profile, "a");
            var b = ReadFloat(profile, "b");
            var c = ReadFloat(profile, "c");
            var d = ReadFloat(profile, "d");
            
            return new RoadElevationProfile(s, a, b, c, d);
        }

        private  LaneOffset ConstructLaneOffset(XElement laneOffsetElement)
        {
            var s = ReadFloat(laneOffsetElement,"s");
            var a = ReadFloat(laneOffsetElement,"a");
            var b = ReadFloat(laneOffsetElement,"b");
            var c = ReadFloat(laneOffsetElement,"c");
            var d = ReadFloat(laneOffsetElement,"d");
            return new LaneOffset(s, new Vector4(a, b, c, d));
        }

        internal  RoadLink ConstructRoadLink(IEnumerable<XElement> potentialRoadLink)
        {
            if (potentialRoadLink == null || !potentialRoadLink.Any())
            {
                return new RoadLink();
            }

            var roadLink = potentialRoadLink.First();
            var id = ReadString(roadLink, "elementId");
            var elementType = ReadString(roadLink, "elementType");
            switch (elementType)
            {
                case "road":
                    var contactPoint = ReadEnum<LinkContactPoint>(roadLink, "contactPoint");
                    return new RoadLink(id, contactPoint, RoadLinkType.Road);
                case "junction":
                    return new RoadLink(id, LinkContactPoint.None, RoadLinkType.Junction);
                default:
                    LogUnsupportedElementWarning(elementType);
                    return new RoadLink();
            }
        }

        public bool TryCreateRoadNetworkDescription(XDocument openDrive, out RoadNetworkDescription network)
        {
            return TryCreateRoadNetworkDescription(openDrive.Descendants(), out network);
        }

        public bool TryCreateRoadNetworkDescription(IEnumerable<XElement> mapElements, out RoadNetworkDescription network)
        {
            network = ScriptableObject.CreateInstance<RoadNetworkDescription>();
            var roadElements = new List<Road>();
            var junctionElements = new List<Junction>();
            // It's unclear whether entire road elements ever fall in the "too small to matter" category so we're
            // going to keep track of them to see if they show up
            var numTinyRoads = 0;

            foreach (var element in mapElements)
            {
                switch (element.Name.ToString())
                {
                    // TODO: Schema doesn't validate that road IDs are all unique, so we need to check if the key
                    //       already exists in the dictionary and throw an exception if it does
                    case "road":
                        var roadElement = ConstructRoad(element);
                        //`linkedRoads.Add(roadElement.id, roadElement);
                        roadElements.Add(roadElement);
                        if (roadElement.length < MinSectionLength)
                        {
                            numTinyRoads++;
                        }

                        break;
                    case "junction":
                        var junctionElement = ConstructJunction(element);
                        junctionElements.Add(junctionElement);
                        break;
                    default:
                        break;
                }
            }
            
            var numTinyLaneSections = CountSmallElements(roadElements);
            network.SetRoadsAndJunctions(roadElements, junctionElements);
            
            if (numTinyRoads > 0)
            {
                Debug.LogError($"Counted {numTinyRoads} roads smaller than {MinSectionLength} meters in network {network.name}."
                + " We will not be able to sample/render this network properly.");
                NumErrorsLogged++;
            }
            if (numTinyLaneSections > 0)
            {
                Debug.LogError($"Counted {numTinyLaneSections} lane sections that are less than {MinSectionLength}"
                + $" meters long in network {network.name}. We will not be able to sample/render this network properly.");
                NumErrorsLogged++;
            }

            // Consider creation a success if no errors were logged
            return NumErrorsLogged == 0;
        }

        // Some elements from auto-generated xodr files are so small that Mathf.Approximately considers their
        // s-coordinates to be equal values. Iterate through the list of roads and count how many times this happens
        private int CountSmallElements(List<Road> allRoads)
        {
            var numTooSmall = 0;
            foreach (var road in allRoads)
            {
                var roadLength = road.length;
                for (var i = 0; i < road.laneSections.Count; i++)
                {
                    var section = road.laneSections[i];
                    var sectionStart = section.sRoad;
                    var length = i == road.laneSections.Count - 1
                        ? roadLength - sectionStart
                        : road.laneSections[i + 1].sRoad - sectionStart;
                    if (length < MinSectionLength)
                    {
                        numTooSmall++;
                    }
                }
            }

            return numTooSmall;
        }
    }
}