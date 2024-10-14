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

        public static JobDef VIF_GotoAcrossMaps;

        public static JobDef VIF_GotoThingAcrossMaps;

        public static JobDef VIF_AttackMeleeAcrossMaps;

        public static JobDef VIF_HaulToCellAcrossMaps;

        public static JobDef VIF_HaulToContainerAcrossMaps;

        public static JobDef VIF_CarryDownedPawnDraftedAcrossMaps;

        public static JobDef VIF_TakeDownedPawnToBedDraftedAcrossMaps;

        public static JobDef VIF_CarryToEntityHolderAlreadyHoldingAcrossMaps;
    }
}
