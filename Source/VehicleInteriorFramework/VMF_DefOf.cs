using RimWorld;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    [DefOf]
    public static class VMF_DefOf
    {
        static VMF_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VMF_DefOf));
        }

        public static WorldObjectDef VMF_VehicleMap;

        public static ThingDef VMF_VehicleStructureFilled;

        public static ThingDef VMF_VehicleStructureEmpty;

        public static ThingDef VMF_ZiplineEnd;

        public static ThingDef VMF_Bullet_ZiplineTurretReturn;

        public static TerrainDef VMF_VehicleFloor;

        public static ShaderTypeDef VMF_TerrainHardWithZ;

        public static ShaderTypeDef VMF_CutoutComplexRGBOpacity;

        public static ShaderTypeDef VMF_CutoutComplexPatternOpacity;

        public static ShaderTypeDef VMF_CutoutComplexSkinOpacity;

        public static JobDef VMF_GotoDestMap;

        public static JobDef VMF_GotoAcrossMaps;

        public static JobDef VMF_AttackMeleeAcrossMaps;

        public static JobDef VMF_HaulToCellAcrossMaps;

        public static JobDef VMF_HaulToContainerAcrossMaps;

        public static JobDef VMF_CarryDownedPawnDraftedAcrossMaps;

        public static JobDef VMF_TakeDownedPawnToBedDraftedAcrossMaps;

        public static JobDef VMF_CarryToPrisonerBedDraftedAcrossMaps;

        public static JobDef VMF_CarryToEntityHolderAcrossMaps;

        public static JobDef VMF_CarryToEntityHolderAlreadyHoldingAcrossMaps;

        public static JobDef VMF_TransferBetweenEntityHoldersAcrossMaps;

        public static JobDef VMF_ArrestAcrossMaps;

        public static JobDef VMF_RescueAcrossMaps;

        public static JobDef VMF_BringBabyToSafetyAcrossMaps;

        public static JobDef VMF_CaptureAcrossMaps;

        public static JobDef VMF_CarryToCryptosleepCasketAcrossMaps;

        public static JobDef VMF_CarryToCryptosleepCasketDraftedAcrossMaps;

        public static JobDef VMF_HaulToTransporterAcrossMaps;

        public static JobDef VMF_ExtractRelicAcrossMaps;

        public static JobDef VMF_InstallRelicAcrossMaps;

        public static JobDef VMF_HaulMechToChargerAcrossMaps;

        public static JobDef VMF_DeliverToBedAcrossMaps;

        public static JobDef VMF_GiveToPackAnimalAcrossMaps;

        public static JobDef VMF_CarryDownedPawnToPortalAcrossMaps;

        public static JobDef VMF_CarryDownedPawnToExitAcrossMaps;

        public static JobDef VMF_TendPatientAcrossMaps;

        public static JobDef VMF_TendEntityAcrossmaps;

        public static JobDef VMF_BoardAcrossMaps;

        public static JobDef VMF_RefuelAcrossMaps;

        public static JobDef VMF_RefuelAtomicAcrossMaps;

        public static JobDef VMF_RearmTurretAcrossMaps;

        public static JobDef VMF_RearmTurretAtomicAcrossMaps;

        public static JobDef VMF_RefuelVehicleTank;

        //public static JobDef VMF_RefuelVehicleTankAtomic;

        public static WorkGiverDef VMF_LoadBuildableContainer;

        public static VehicleStatDef MaximumPayload;
    }
}
