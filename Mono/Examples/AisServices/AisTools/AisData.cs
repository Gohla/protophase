using System;

namespace AisTools
{
    [Serializable]
    public struct AisData
    {
        public int TransponderID;
        public double Latitude;
        public double Longitude;
        public int SpeedOverGround;
        public int CourseOverGround;
        public int PositionAccuracy;
        public int TrueHeading;
        public int RateOfTurn;
    }
}