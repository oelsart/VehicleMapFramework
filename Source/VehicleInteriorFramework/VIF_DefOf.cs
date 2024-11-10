using RimWorld;
using Vehicles;
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

        public static TerrainDef VIF_VehicleFloor;

        public static RGBOpacityShaderTypeDef VIF_CutoutComplexRGBOpacity;

        public static RGBOpacityShaderTypeDef VIF_CutoutComplexPatternOpacity;

        public static RGBOpacityShaderTypeDef VIF_CutoutComplexSkinOpacity;

        public static JobDef VIF_GotoAcrossMaps;

        public static JobDef VIF_AttackMeleeAcrossMaps;

        public static JobDef VIF_HaulToCellAcrossMaps;

        public static JobDef VIF_HaulToContainerAcrossMaps;

        public static JobDef VIF_CarryDownedPawnDraftedAcrossMaps;

        public static JobDef VIF_TakeDownedPawnToBedDraftedAcrossMaps;

        public static JobDef VIF_CarryToPrisonerBedDraftedAcrossMaps;

        public static JobDef VIF_CarryToEntityHolderAcrossMaps;

        public static JobDef VIF_CarryToEntityHolderAlreadyHoldingAcrossMaps;

        public static JobDef VIF_TransferBetweenEntityHoldersAcrossMaps;

        public static JobDef VIF_ArrestAcrossMaps;

        public static JobDef VIF_RescueAcrossMaps;

        public static JobDef VIF_BringBabyToSafetyAcrossMaps;

        public static JobDef VIF_CaptureAcrossMaps;

        public static JobDef VIF_CarryToCryptosleepCasketAcrossMaps;

        public static JobDef VIF_CarryToCryptosleepCasketDraftedAcrossMaps;

        public static JobDef VIF_HaulToTransporterAcrossMaps;

        public static JobDef VIF_ExtractRelicAcrossMaps;

        public static JobDef VIF_InstallRelicAcrossMaps;

        public static JobDef VIF_HaulMechToChargerAcrossMaps;

        public static JobDef VIF_DeliverToBedAcrossMaps;

        public static JobDef VIF_GiveToPackAnimalAcrossMaps;

        public static JobDef VIF_CarryDownedPawnToPortalAcrossMaps;

        public static JobDef VIF_CarryDownedPawnToExitAcrossMaps;

        public static JobDef VIF_TendPatientAcrossMaps;

        public static JobDef VIF_TendEntityAcrossmaps;

        public static JobDef VIF_BoardAcrossMaps;
    }
}
