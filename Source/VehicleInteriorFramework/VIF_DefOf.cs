using RimWorld;
using Verse;

namespace VehicleInteriors
{
    [DefOf]
    public static class VIF_DefOf
    {
        static VIF_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VIF_DefOf));
        }

        public static WorldObjectDef VIF_VehicleMap;

        public static MapGeneratorDef VIF_InteriorGenerator;

        public static TerrainDef Sand_;
    }
}
