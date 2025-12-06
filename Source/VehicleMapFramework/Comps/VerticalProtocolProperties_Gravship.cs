using SmashTools;
using Vehicles;

namespace VehicleMapFramework
{
    public class VerticalProtocolProperties_Gravship : VerticalProtocolProperties
    {
        [GraphEditable]
        public LinearCurve thrusterFlameCurve;

        [GraphEditable]
        public LinearCurve thrusterFlameVerticalCurve;

        [GraphEditable]
        public LinearCurve engineGlowCurve;

        [GraphEditable]
        public LinearCurve engineGlowVerticalCurve;
    }
}
