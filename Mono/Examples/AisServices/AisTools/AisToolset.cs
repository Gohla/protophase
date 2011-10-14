using System;
using System.Text;

namespace AisTools
{
    public static class AisToolset
    {
        public static AisData? DecodeAis(string fullmessage)
        {
            string[] arr = fullmessage.Split(',');
            if (arr[1] == "2") //not supporting multi part messages right now.
                return null;
            if (getChecksum(fullmessage) != fullmessage.Substring(fullmessage.IndexOf('*') + 1))
                return null;
            return DecodeAisDataMessage(arr[5]);
        }

        public static string getChecksum(string sentence)
        {
            int checksum = Convert.ToByte(sentence[sentence.IndexOf('!') + 1]);
            for (int i = sentence.IndexOf('!') + 2; i < sentence.IndexOf('*'); i++)
                checksum ^= Convert.ToByte(sentence[i]);
            return checksum.ToString("X2");
        }

        private static AisData? DecodeAisDataMessage(string dataMessage)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in dataMessage)
            {
                int i = c;
                i -= 48;
                if (i > 40)
                    i -= 8;
                for (int j = 5; j >= 0; j--)
                {
                    if (((i >> j) & 1) == 1)
                        sb.Append("1");
                    else
                        sb.Append("0");
                }
            }
            if (sb.Length < 89 + 27)
                return null;
            string bitString = sb.ToString();
            long lat = TreatAsSigned(bitString.Substring(89, 27));
            long lon = TreatAsSigned(bitString.Substring(61, 28));
            AisData aisObj = new AisData();
            aisObj.Latitude = lat / 600000.0;
            aisObj.Longitude = lon / 600000.0;
            aisObj.TransponderID = Convert.ToInt32(bitString.Substring(8, 30), 2);
            aisObj.RateOfTurn = Convert.ToInt32(bitString.Substring(42, 8), 2);
            aisObj.SpeedOverGround = Convert.ToInt32(bitString.Substring(50, 10), 2) / 10;
            aisObj.CourseOverGround = Convert.ToInt32(bitString.Substring(116, 12), 2);
            aisObj.PositionAccuracy = Convert.ToInt32(bitString.Substring(60, 1), 2);
            aisObj.TrueHeading = Convert.ToInt32(bitString.Substring(128, 9), 2);
            return aisObj;
        }

        private static long TreatAsSigned(String s)
        {
            long l = Convert.ToInt32(s, 2);
            if (s.StartsWith("1"))
            {
                return ((-1L) << (s.Length - 1)) | l;
            }
            return l;
        }

        public static void DumpAis(AisData aisObj)
        {
            Console.WriteLine("Transponder MMSI: " + aisObj.TransponderID);
            Console.WriteLine("Latitude: " + aisObj.Latitude);
            Console.WriteLine("Longitude: " + aisObj.Longitude);
            Console.WriteLine("SpeedOverGround: " + aisObj.SpeedOverGround);
            Console.WriteLine("CourseOverGround: " + aisObj.CourseOverGround);
            Console.WriteLine("PositionAccuracy: " + aisObj.PositionAccuracy);
            Console.WriteLine("TrueHeading: " + aisObj.TrueHeading);
            Console.WriteLine("RateOfTurn: " + aisObj.RateOfTurn);
        }
    }
}