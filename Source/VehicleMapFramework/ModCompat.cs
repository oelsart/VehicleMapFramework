using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
internal static class ModCompat
{
    internal static bool AnyNull(params object[] args)
    {
        return args.Any(arg => arg == null);
    }

    internal static void LogIncompat(string modName)
    {
        VMF_Log.Error($"{modName} compatibility is broken.");
    }

    static ModCompat()
    {
        foreach (var type in AccessTools.InnerTypes(typeof(ModCompat)))
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
    }

    public static class VehicleFramework
    {
        public const string HarmonyId = "SmashPhil.VehicleFramework";

        public static readonly FastInvokeHandler VehicleTurret_IsManned = MethodInvoker.GetHandler(AccessTools.PropertySetter(typeof(VehicleTurret), nameof(VehicleTurret.IsManned)));

        public static readonly Dictionary<(VehicleDef, Rot4), Texture2D> CachedVehicleTextures = AccessTools.StaticFieldRefAccess<Dictionary<(VehicleDef, Rot4), Texture2D>>(typeof(VehicleTex), "CachedVehicleTextures");

        static VehicleFramework()
        {
            if (AnyNull(VehicleTurret_IsManned, CachedVehicleTextures))
            {
                LogIncompat("Vehicle Framework");
            }
        }
    }

    public static readonly bool AdaptiveStorage = ModsConfig.IsActive("adaptive.storage.framework");

    public static readonly bool AllowTool = ModsConfig.IsActive("UnlimitedHugs.AllowTool");

    public static readonly bool BillDoorsFramework = ModsConfig.IsActive("3HSTltd.Framework");

    public static readonly bool BiomesCaverns = ModsConfig.IsActive("BiomesTeam.BiomesCaverns");

    public static readonly bool CallTradeShips = ModsConfig.IsActive("calltradeships.kv.rw");

    public static readonly bool CombatExtended = ModsConfig.IsActive("CETeam.CombatExtended") || ModsConfig.IsActive("CETeam.CombatExtended_steam");

    public static readonly bool ColonyGroups = ModsConfig.IsActive("DerekBickley.LTOColonyGroupsFinal");

    public static readonly bool DeepStorage = ModsConfig.IsActive("LWM.DeepStorage");

    public static readonly bool Fortified = ModsConfig.IsActive("AOBA.Framework");

    public static readonly bool DrakkenLaserDrill = ModsConfig.IsActive("MYDE.DrakkenLaserDrill") || ModsConfig.IsActive("Mlie.DrakkenLaserDrill");

    public static readonly bool DrillTurret = ModsConfig.IsActive("Mlie.MiningCoDrillTurret");

    public static class DubsBadHygiene
    {
        public static readonly bool Active = ModsConfig.IsActive("Dubwise.DubsBadHygiene") || ModsConfig.IsActive("Dubwise.DubsBadHygiene.Lite");

        public static readonly bool LiteMode;

        public static readonly Type SectionLayer_ThingsSewagePipe;

        public static readonly Type SectionLayer_SewagePipeOverlay;

        public static readonly Type SectionLayer_AirDuctOverlay;

        public static readonly Type SectionLayer_Irrigation;

        public static readonly Type SectionLayer_FertilizerGrid;

        public static readonly Type Building_Pipe;

        public static readonly FastInvokeHandler PrintForGrid;

        public static readonly Type CompProperties_Pipe;

        public static readonly AccessTools.FieldRef<object, int> CompProperties_Pipe_mode;

        public static readonly AccessTools.FieldRef<object, int> SectionLayer_PipeOverlay_mode;

        static DubsBadHygiene()
        {
            if (Active)
            {
                try
                {
                    Patch_JobGiver_Work_PawnCanUseWorkGiver.NoNeedVirtualMapTransferList.Add(
                        GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.WorkGiver_PlaceFertilizer", "DubsBadHygiene"));
                    
                    LiteMode = (bool)AccessTools.PropertyGetter("DubsBadHygiene.Settings:LiteMode").Invoke(null, null);
                    if (LiteMode) return;

                    SectionLayer_ThingsSewagePipe = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_SewagePipeOverlay", "DubsBadHygiene");
                    if (SectionLayer_ThingsSewagePipe != null)
                    {
                        VehicleSectionLayerManager.OrientedSectionLayerTypes.Add(SectionLayer_ThingsSewagePipe);
                    }
                    SectionLayer_SewagePipeOverlay = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_SewagePipeOverlay", "DubsBadHygiene");
                    SectionLayer_AirDuctOverlay = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_AirDuctOverlay", "DubsBadHygiene");
                    SectionLayer_Irrigation = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_Irrigation", "DubsBadHygiene");
                    SectionLayer_FertilizerGrid = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_FertilizerGrid", "DubsBadHygiene");
                    Building_Pipe = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.Building_Pipe", "DubsBadHygiene");
                    PrintForGrid = MethodInvoker.GetHandler(AccessTools.Method(Building_Pipe, "PrintForGrid"));
                    CompProperties_Pipe = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.CompProperties_Pipe", "DubsBadHygiene");
                    CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
                    SectionLayer_PipeOverlay_mode = AccessTools.FieldRefAccess<int>("DubsBadHygiene.SectionLayer_PipeOverlay:mode");
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (!LiteMode && AnyNull(
                        SectionLayer_SewagePipeOverlay,
                        SectionLayer_AirDuctOverlay,
                        SectionLayer_Irrigation,
                        SectionLayer_FertilizerGrid,
                        Building_Pipe,
                        PrintForGrid,
                        CompProperties_Pipe,
                        CompProperties_Pipe_mode,
                        SectionLayer_PipeOverlay_mode))
                    {
                        LogIncompat("Dubs Bad Hygiene");
                        Active = false;
                    }
                }
            }
        }
    }

    public static class Rimefeller
    {
        public static readonly bool Active = ModsConfig.IsActive("Dubwise.Rimefeller");

        public static readonly Type SectionLayer_SewagePipe;

        public static readonly Type SectionLayer_ThingsPipe;

        public static readonly Type XSectionLayer_Napalm;

        public static readonly Type XSectionLayer_OilSpill;

        public static readonly Type Building_Pipe;

        public static readonly FastInvokeHandler PrintForGrid;

        public static readonly Type CompProperties_Pipe;

        public static readonly AccessTools.FieldRef<object, int> CompProperties_Pipe_mode;

        public static readonly AccessTools.FieldRef<object, int> SectionLayer_PipeOverlay_mode;

        static Rimefeller()
        {
            if (Active)
            {
                try
                {
                    SectionLayer_SewagePipe = GenTypes.GetTypeInAnyAssembly("Rimefeller.SectionLayer_SewagePipe", "Rimefeller");
                    SectionLayer_ThingsPipe = GenTypes.GetTypeInAnyAssembly("Rimefeller.SectionLayer_ThingsPipe", "Rimefeller");
                    if (SectionLayer_ThingsPipe != null)
                    {
                        VehicleSectionLayerManager.OrientedSectionLayerTypes.Add(SectionLayer_ThingsPipe);
                    }
                    XSectionLayer_Napalm = GenTypes.GetTypeInAnyAssembly("Rimefeller.XSectionLayer_Napalm", "Rimefeller");
                    XSectionLayer_OilSpill = GenTypes.GetTypeInAnyAssembly("Rimefeller.XSectionLayer_OilSpill", "Rimefeller");
                    Building_Pipe = GenTypes.GetTypeInAnyAssembly("Rimefeller.Building_Pipe", "Rimefeller");
                    PrintForGrid = MethodInvoker.GetHandler(AccessTools.Method(Building_Pipe, "PrintForGrid"));
                    CompProperties_Pipe = GenTypes.GetTypeInAnyAssembly("Rimefeller.CompProperties_Pipe", "Rimefeller");
                    CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
                    SectionLayer_PipeOverlay_mode = AccessTools.FieldRefAccess<int>("Rimefeller.SectionLayer_PipeOverlay:mode");
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(
                        SectionLayer_SewagePipe,
                        SectionLayer_ThingsPipe,
                        XSectionLayer_Napalm,
                        XSectionLayer_OilSpill,
                        Building_Pipe,
                        PrintForGrid,
                        CompProperties_Pipe,
                        CompProperties_Pipe_mode,
                        SectionLayer_PipeOverlay_mode))
                    {
                        LogIncompat("Rimefeller");
                        Active = false;
                    }
                }
            }
        }
    }

    public static class DefenseGrid
    {
        public static readonly bool Active = ModsConfig.IsActive("Aelanna.EccentricTech.DefenseGrid");

        public static readonly Type SectionLayer_DefenseGridOverlay;

        public static readonly Type CompDefenseConduit;

        public static readonly Type Designator_DeconstructConduit;

        static DefenseGrid()
        {
            if (Active)
            {
                try
                {
                    SectionLayer_DefenseGridOverlay = AccessTools.TypeByName("EccentricDefenseGrid.SectionLayer_DefenseGridOverlay");
                    CompDefenseConduit = AccessTools.TypeByName("EccentricDefenseGrid.CompDefenseConduit");
                    Designator_DeconstructConduit = AccessTools.TypeByName("EccentricDefenseGrid.Designator_DeconstructConduit");
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(SectionLayer_DefenseGridOverlay, CompDefenseConduit, Designator_DeconstructConduit))
                    {
                        LogIncompat("Defense Grid");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool ExosuitFramework = ModsConfig.IsActive("Aoba.Exosuit.Framework");

    public static readonly bool GiantImperialTurret = ModsConfig.IsActive("XMB.Giantimperialcannonturret.MO");

    public static readonly bool Gunplay = ModsConfig.IsActive("automatic.gunplay");

    public static class MeleeAnimation
    {
        public static readonly bool Active = ModsConfig.IsActive("co.uk.epicguru.meleeanimation");

        public static readonly Func<Vector3, Pawn, IEnumerable<FloatMenuOption>> GenerateAMMenuOptions;

        static MeleeAnimation()
        {
            if (Active)
            {
                try
                {
                    var method = AccessTools.Method("AM.UI.DraftedFloatMenuOptionsUI:GenerateMenuOptions");
                    GenerateAMMenuOptions = AccessTools.MethodDelegate<Func<Vector3, Pawn, IEnumerable<FloatMenuOption>>>(method);
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(GenerateAMMenuOptions))
                    {
                        LogIncompat("Melee Animation");
                        Active = false;
                    }
                }
            }
        }
    }

    public static class MiscRobots
    {
        public static readonly bool Active = ModsConfig.IsActive("Haplo.Miscellaneous.Robots");

        public static readonly Type X2_AIRobot;

        public static readonly AccessTools.FieldRef<Pawn, Building> rechargeStation;

        static MiscRobots()
        {
            if (Active)
            {
                try
                {
                    X2_AIRobot = GenTypes.GetTypeInAnyAssembly("AIRobot.X2_AIRobot", "AIRobot");
                    rechargeStation = AccessTools.FieldRefAccess<Building>("AIRobot.X2_AIRobot:rechargeStation");
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(X2_AIRobot, rechargeStation))
                    {
                        LogIncompat("MiscRobots");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool MuzzleFlash = ModsConfig.IsActive("IssacZhuang.MuzzleFlash");

    public static readonly bool PathfindingFramework = ModsConfig.IsActive("pathfinding.framework");

    public static readonly bool ProjectRimFactory = ModsConfig.IsActive("spdskatr.projectrimfactory");

    public static readonly bool SmarterConstruction = ModsConfig.IsActive("dhultgren.smarterconstruction");

    public static readonly bool TabulaRasa = ModsConfig.IsActive("neronix17.toolbox");

    public static class VFECore
    {
        public static readonly bool Active = ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");

        public static readonly Type PipeNetDef;

        public static readonly Type SectionLayer_Resource;

        public static readonly FastInvokeHandler ShouldDraw;

        public static readonly AccessTools.FieldRef<Def> pipeNetDef;

        static VFECore()
        {
            if (Active)
            {
                try
                {
                    PipeNetDef = GenTypes.GetTypeInAnyAssembly("PipeSystem.PipeNetDef");
                    SectionLayer_Resource = GenTypes.GetTypeInAnyAssembly("PipeSystem.SectionLayer_Resource", "PipeSystem");
                    ShouldDraw = MethodInvoker.GetHandler(AccessTools.PropertyGetter(SectionLayer_Resource, "ShouldDraw"));
                    pipeNetDef = AccessTools.StaticFieldRefAccess<Def>(AccessTools.Field(SectionLayer_Resource, "pipeNet"));
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(PipeNetDef, SectionLayer_Resource, ShouldDraw, pipeNetDef))
                    {
                        LogIncompat("VFE Core");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool VFEArchitect = ModsConfig.IsActive("VanillaExpanded.VFEArchitect");

    public static class VFESecurity
    {
        public static readonly bool Active = ModsConfig.IsActive("VanillaExpanded.VFESecurity");

        public static readonly AccessTools.FieldRef<object, GlobalTargetInfo> targetedTile;

        public static readonly AccessTools.FieldRef<object, int> worldTileRange;
        
        static VFESecurity()
        {
            if (Active)
            {
                try
                {
                    targetedTile = AccessTools.FieldRefAccess<GlobalTargetInfo>("VFESecurity.CompLongRangeArtillery:targetedTile");
                    worldTileRange = AccessTools.FieldRefAccess<int>("VFESecurity.CompProperties_LongRangeArtillery:worldTileRange");
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(targetedTile, worldTileRange))
                    {
                        LogIncompat("VFE Security");
                        Active = false;
                    }
                }
            }
        }
    }

    public static class VVE
    {
        public static readonly bool Active = ModsConfig.IsActive("OskarPotocki.VanillaVehiclesExpanded");

        public static readonly AccessTools.FieldRef<CompProperties, float> refuelAmountPerTick;

        static VVE()
        {
            if (Active)
            {
                try
                {
                    refuelAmountPerTick = AccessTools.FieldRefAccess<float>("VanillaVehiclesExpanded.CompProperties_RefuelingPump:refuelAmountPerTick");
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(refuelAmountPerTick))
                    {
                        LogIncompat("VVE");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool VFEPirates = ModsConfig.IsActive("OskarPotocki.VFE.Pirates");

    public static class VFEMechanoid
    {
        public static readonly bool Active = ModsConfig.IsActive("OskarPotocki.VFE.Mechanoid");

        public static readonly FastInvokeHandler DoWorkOnCell;

        static VFEMechanoid()
        {
            if (Active)
            {
                try
                {
                    DoWorkOnCell = MethodInvoker.GetHandler(AccessTools.Method("VFE.Mechanoids.Buildings.Building_AutoPlant:DoWorkOnCell"));
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(DoWorkOnCell))
                    {
                        LogIncompat("VFEPirates");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool VGE = ModsConfig.IsActive("vanillaexpanded.gravship");

    public static readonly bool Vivi = ModsConfig.IsActive("gguake.race.vivi");

    public static readonly bool WhileYoureUp = ModsConfig.IsActive("CodeOptimist.JobsOfOpportunity") || ModsConfig.IsActive("zsbk.patch16.whileyoureup");

    public static readonly bool YayosCombat3 = ModsConfig.IsActive("Mlie.YayosCombat3");

    public static readonly bool PickUpAndHaul = ModsConfig.IsActive("Mehni.PickUpAndHaul") || ModsConfig.IsActive("Teemo.PickUpAndHaulForked");

    public static class EnergyShield
    {
        public static readonly bool Active = ModsConfig.IsActive("zhuzi.AdvancedEnergy.Shields");

        public static readonly Type Building_Shield;

        public static readonly bool CECompat;

        static EnergyShield()
        {
            if (Active)
            {
                try
                {
                    Building_Shield = AccessTools.TypeByName("zhuzi.AdvancedEnergy.Shields.Shields.Building_Shield");
                    CECompat = ModsConfig.IsActive("cn.zhuzijun.EnergyShieldCECompat");
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(Building_Shield))
                    {
                        LogIncompat("Energy Shield");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool TraderShips = ModsConfig.IsActive("automatic.traderships");

    public static readonly bool NightmareCore = ModsConfig.IsActive("Nightmare.Core");

    public static readonly bool Aquariums = ModsConfig.IsActive("Nightmare.Aquariums");

    public static readonly bool SmartPistol = ModsConfig.IsActive("rabiosus.smartpistol");

    public static readonly bool ReGrowth = ModsConfig.IsActive("ReGrowth.BOTR.Core");

    public static class SmartFarming
    {
        public static readonly bool SmartFarmingActive = ModsConfig.IsActive("Owlchemist.SmartFarming");

        public static readonly bool Active = SmartFarmingActive || ReGrowth;

        public static readonly Type MapComponent_SmartFarming;

        public static readonly AccessTools.FieldRef<MapComponent, IDictionary> growZoneRegistry;

        public static readonly AccessTools.FieldRef<object, int> priority;

        static SmartFarming()
        {
            if (Active)
            {
                try
                {
                    Type t_ZoneData;
                    if (SmartFarmingActive)
                    {
                        MapComponent_SmartFarming = GenTypes.GetTypeInAnyAssembly("SmartFarming.MapComponent_SmartFarming", "SmartFarming");
                        t_ZoneData = GenTypes.GetTypeInAnyAssembly("SmartFarming.ZoneData", "SmartFarming");
                    }
                    else
                    {
                        MapComponent_SmartFarming = GenTypes.GetTypeInAnyAssembly("ReGrowthCore.MapComponent_SmartFarming", "ReGrowthCore");
                        t_ZoneData = GenTypes.GetTypeInAnyAssembly("ReGrowthCore.ZoneData", "ReGrowthCore");
                    }
                    growZoneRegistry = AccessTools.FieldRefAccess<IDictionary>(MapComponent_SmartFarming, "growZoneRegistry");
                    priority = AccessTools.FieldRefAccess<int>(t_ZoneData, "priority");
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(MapComponent_SmartFarming, growZoneRegistry, priority))
                    {
                        LogIncompat("Smart Farming");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool RimWorldOfMagic = ModsConfig.IsActive("Torann.ARimworldOfMagic");

    public static readonly bool CeleTech = ModsConfig.IsActive("TOT.CeleTech.MKIII");

    public static readonly bool PauseOtherSettlements = ModsConfig.IsActive("esvn.PauseOtherSettlementsSimulation");

    public static class MultiFloors
    {
        public static readonly bool Active = ModsConfig.IsActive("telardo.MultiFloors") || ModsConfig.IsActive("telardo.MultiFloorsDev");

        public static readonly Func<Map, Map> GroundMap;

        public static readonly Func<Map, int> GetLevel;

        public static readonly Action<Map> RevalidateLaunchSiteState;

        public static readonly Type SectionLayer_LowerLevel;

        public static readonly Type MF_LevelMapComp;

        public static readonly FastInvokeHandler GetOtherMapVerticallyOutwardFromCache;

        static MultiFloors()
        {
            if (Active)
            {
                try
                {
                    GroundMap = AccessTools.MethodDelegate<Func<Map, Map>>("MultiFloors.HarmonyPatches.HarmonyPatch_CallBossGroupOnGround:GetGroundMap");
                    GetLevel = AccessTools.MethodDelegate<Func<Map, int>>("MultiFloors.HarmonyPatches.HarmonyPatch_SortMapInColonistBarByLevel:GetLevel");
                    RevalidateLaunchSiteState = AccessTools.MethodDelegate<Action<Map>>("MultiFloors.HarmonyPatches.HarmonyPatch_OnGravshipLaunch:RevalidateLaunchSiteState");
                    SectionLayer_LowerLevel = GenTypes.GetTypeInAnyAssembly("MultiFloors.Maps.SectionLayer_LowerLevel", "MultiFloors.Maps");
                    if (SectionLayer_LowerLevel != null)
                    {
                        VehicleSectionLayerManager.OrientedSectionLayerTypes.Add(SectionLayer_LowerLevel);
                    }
                    MF_LevelMapComp = GenTypes.GetTypeInAnyAssembly("MultiFloors.MF_LevelMapComp", "MultiFloors");
                    GetOtherMapVerticallyOutwardFromCache = MethodInvoker.GetHandler(AccessTools.Method("MultiFloors.LevelUtility:GetOtherMapVerticallyOutwardFromCache"));
                }
                catch (Exception ex)
                {
                    VMF_Log.Error(ex.Message);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(GroundMap, GetLevel, RevalidateLaunchSiteState, SectionLayer_LowerLevel, MF_LevelMapComp, GetOtherMapVerticallyOutwardFromCache))
                    {
                        LogIncompat("MultiFloors");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool CutPlantsBeforeBuilding = ModsConfig.IsActive("Mlie.CutPlantsBeforeBuilding");

    public static class StackGap
    {
        public static readonly bool Active = ModsConfig.IsActive("Andromeda.StackGap");

        public const string HarmonyId = "Andromeda.StackGap";
    }
    public static readonly bool AnimalCages = ModsConfig.IsActive("zal.animalcages");

    public static readonly bool DoNotHitMe = ModsConfig.IsActive("Og.do.not.hit.me");
}