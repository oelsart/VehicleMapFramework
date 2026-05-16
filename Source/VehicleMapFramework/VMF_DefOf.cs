using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

[DefOf]
public static class VMF_DefOf
{
    static VMF_DefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(VMF_DefOf));
    }

    public static WorldObjectDef VMF_VehicleMap;

    public static MapGeneratorDef VMF_VehicleMapGenerator;

    public static ThingDef VMF_VehicleStructureFilled;

    public static ThingDef VMF_VehicleStructureEmpty;

    public static ThingDef VMF_ZiplineEnd;

    public static ThingDef VMF_Bullet_ZiplineTurretReturn;

    public static ThingDef VMF_GrapplingHookFlyer;

    public static ThingDef VMF_MoteSink;

    public static TerrainDef VMF_VehicleFloor;

    public static TerrainDef VMF_ImpassableFloor;

    public static ShaderTypeDef VMF_TerrainHardWithZ;

    public static ShaderTypeDef VMF_CutoutComplexRGBOpacity;

    public static ShaderTypeDef VMF_CutoutComplexPatternOpacity;

    public static ShaderTypeDef VMF_CutoutComplexSkinOpacity;

    public static JobDef VMF_GotoDestMap;

    public static JobDef VMF_GotoAcrossMaps;

    public static JobDef VMF_BoardAcrossMaps;

    public static JobDef VMF_RefuelVehicleTank;

    public static JobDef VMF_RepairMapVehicle;

    public static JobDef VMF_DeconstructVehicleSegment;
    
    public static WorkGiverDef VMF_RemoveVehicleSegment;

    public static DesignationDef VMF_RemoveSegment;

    public static ThinkTreeDef VMF_GotoDestMapThinkTree;

    //public static JobDef VMF_RefuelVehicleTankAtomic;

    public static VehicleStatDef MaximumPayload;

    public static MapVehicleEventDef EnterNextCell;

    [MayRequireOdyssey]
    public static VehicleDef VMF_GravshipVehicleBase;

    [MayRequireOdyssey]
    public static VehicleDef VMF_GravshipVehicleBaseSpace;
}
