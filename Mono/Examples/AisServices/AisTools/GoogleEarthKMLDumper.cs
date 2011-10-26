using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Protophase.Shared;

namespace AisTools
{
    [ServiceType("KMLDumper"), ServiceVersion("0.1")]
    public class GoogleEarthKMLDumper
    {
        private const string KMLHEADER = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document><name>Data+BalloonStyle</name><Style id=\"golf-balloon-style\">    <BalloonStyle>      <text> <![CDATA[ Transponder id $[transid]\nLat: $[lat]\nLon: $[lon]\nSpeed over Ground: $[sground]\nCourse over ground: $[cground]\nTrue heading: $[heading]\nRate of turn: $[rot]\n]]> </text> </BalloonStyle>  </Style>";
        private const string KMLFOOTER = "</Document></kml>";
        private readonly Dictionary<int, AisData> _knownTransponders = new Dictionary<int, AisData>();
        //Adds or updates a transponder in _knownTransponders. This method should be subscribed to in a Ais reciever service.
        [RPC]
        public String AddOrUpdateAisTransponder(AisData aisData)
        {
            Console.WriteLine("Received transponder data: " + aisData.TransponderID);
            lock (_knownTransponders)
            {
                if (_knownTransponders.ContainsKey(aisData.TransponderID))
                    _knownTransponders[aisData.TransponderID] = aisData;
                else
                    _knownTransponders.Add(aisData.TransponderID, aisData);
            }
            return "OK";
        }
        //dumps an up to date kml file
        public void DumpKMLFile()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(KMLHEADER);
            lock (_knownTransponders)
            {
                foreach (AisData boat in _knownTransponders.Values)
                {
                    sb.AppendLine("  <Placemark>)");
                    sb.AppendLine("    <name>" + "" + "</name>");
                    sb.AppendLine("    <styleUrl>#golf-balloon-style</styleUrl>");
                    sb.AppendLine("    <ExtendedData>");
                    sb.AppendLine("      <Data name=\"transid\">");
                    sb.AppendLine("        <value>" + boat.TransponderID + "</value>");
                    sb.AppendLine("      </Data>");
                    sb.AppendLine("      <Data name=\"lat\">");
                    sb.AppendLine("        <value>" + boat.Latitude + "</value>");
                    sb.AppendLine("      </Data>");
                    sb.AppendLine("      <Data name=\"lon\">");
                    sb.AppendLine("        <value>" + boat.Longitude+ "</value>");
                    sb.AppendLine("      </Data>");
                    sb.AppendLine("      <Data name=\"sground\">");
                    sb.AppendLine("        <value>" + boat.SpeedOverGround+ "</value>");
                    sb.AppendLine("      </Data>");
                    sb.AppendLine("      <Data name=\"cground\">");
                    sb.AppendLine("        <value>" + boat.CourseOverGround + "</value>");
                    sb.AppendLine("      </Data>");
                    sb.AppendLine("      <Data name=\"heading\">");
                    sb.AppendLine("        <value>" + boat.TrueHeading + "</value>");
                    sb.AppendLine("      </Data>");
                    sb.AppendLine("      <Data name=\"rot\">");
                    sb.AppendLine("        <value>" + boat.RateOfTurn + "</value>");
                    sb.AppendLine("      </Data>");
                    sb.AppendLine("    </ExtendedData>");
                    sb.AppendLine("    <Point>");
                    sb.AppendLine("      <coordinates>" + boat.Longitude.ToString(CultureInfo.InvariantCulture) + "," + boat.Latitude.ToString(CultureInfo.InvariantCulture) + "</coordinates>");
                    sb.AppendLine("    </Point>");
                    sb.AppendLine("</Placemark>");
                }
            }
            sb.AppendLine(KMLFOOTER);
            File.WriteAllText("c:\\OUTPUT.kml", sb.ToString());
        }
    }
}