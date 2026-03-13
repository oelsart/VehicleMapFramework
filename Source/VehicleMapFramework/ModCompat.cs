using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using VehicleMapFramework.VMF_HarmonyPatches.AM;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

internal static class UnitTestDetector
{
    [UsedImplicitly]
    internal static bool IsTestingContext { get; set; }
}

[StaticConstructorOnStartup]
internal static class ModCompat
{
    [UsedImplicitly]
    internal static ThreadLocal<Exception> CctorException { get; private set; }
    
    internal static bool AnyNull(params object[] args)
    {
        return args.Any(arg => arg == null);
    }
    
    internal static void LogIncompat(string modName)
    {
        LogError(new Exception($"{modName} compatibility is broken."));
    }

    internal static bool IsModActive(string id)
    {
        return UnitTestDetector.IsTestingContext || ModsConfig.IsActive(id);
    }

    private static void LogError(Exception ex)
    {
        if (UnitTestDetector.IsTestingContext)
        {
            CctorException.Value = ex;
            return;
        }
        VMF_Log.Error(ex.Message);
    }

    static ModCompat()
    {
        if (UnitTestDetector.IsTestingContext)
        {
            CctorException = new ThreadLocal<Exception>();
            return;
        }
        foreach (var type in AccessTools.InnerTypes(typeof(ModCompat)))
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
    }

    public static class VehicleFramework
    {
        public const string HarmonyId = "SmashPhil.VehicleFramework";

        public static readonly Dictionary<(VehicleDef, Rot4), Texture2D> CachedVehicleTextures;

        static VehicleFramework()
        {
            if (!UnitTestDetector.IsTestingContext)
            {
                CachedVehicleTextures = AccessTools.StaticFieldRefAccess<Dictionary<(VehicleDef, Rot4), Texture2D>>(typeof(VehicleTex), "CachedVehicleTextures");
                if (AnyNull(CachedVehicleTextures))
                {
                    LogIncompat("Vehicle Framework");
                }
            }
        }
    }

    public static class AdaptiveStorage
    {
        public static readonly bool Active = IsModActive("adaptive.storage.framework");

        public static readonly Type TransformData;

        public static readonly Type RotationAngle;

        public static readonly Type ThingClass;

        public static readonly FastInvokeHandler Renderer;

        public static readonly FastInvokeHandler SetAllPrintDatasDirty;

        private static readonly Dictionary<Type, bool> SameOrSubClassDic;

        static AdaptiveStorage()
        {
            
            if (Active)
            {
                try
                {
                    TransformData = AccessTools.TypeByName("ITransformable.TransformData");
                    RotationAngle = AccessTools.TypeByName("ITransformable.RotationAngle");
                    ThingClass = AccessTools.TypeByName("AdaptiveStorage.ThingClass");
                    Renderer = MethodInvoker.GetHandler(AccessTools.PropertyGetter(ThingClass, "Renderer"));
                    SetAllPrintDatasDirty = MethodInvoker.GetHandler(AccessTools.Method("AdaptiveStorage.StorageRenderer:SetAllPrintDatasDirty"));
                    SameOrSubClassDic = [];
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(TransformData, RotationAngle, ThingClass, Renderer, SetAllPrintDatasDirty, SameOrSubClassDic))
                    {
                        LogIncompat("Adaptive Storage");
                        Active = false;
                    }
                }
            }
        }

        public static bool IsAdaptiveStorageClass(Type type)
        {
            if (!SameOrSubClassDic.TryGetValue(type, out var result))
            {
                SameOrSubClassDic[type] = result = type.SameOrSubclassOf(ThingClass);
            }

            return result;
        }
    }

    public static readonly bool AllowTool = IsModActive("UnlimitedHugs.AllowTool");

    public static readonly bool BillDoorsFramework = IsModActive("3HSTltd.Framework");

    public static readonly bool BiomesCaverns = IsModActive("BiomesTeam.BiomesCaverns");

    public static class CallTradeShips
    {
        public static readonly bool Active = IsModActive("calltradeships.kv.rw");

        public static readonly Type Job_CallTradeShip;
        
        public static readonly AccessTools.FieldRef<Job, TraderKindDef> TraderKindDef;

        public static readonly AccessTools.FieldRef<Job, int> TraderKind;

        static CallTradeShips()
        {
            if (Active)
            {
                try
                {
                    Job_CallTradeShip =
                        GenTypes.GetTypeInAnyAssembly("CallTradeShips.Job_CallTradeShip", "CallTradeShips");
                    TraderKindDef = AccessTools.FieldRefAccess<TraderKindDef>(Job_CallTradeShip, "TraderKindDef");
                    TraderKind = AccessTools.FieldRefAccess<int>(Job_CallTradeShip, "TraderKind");
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(Job_CallTradeShip, TraderKindDef, TraderKind))
                    {
                        LogIncompat("Call Trade Ships");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool CombatExtended = IsModActive("CETeam.CombatExtended") || IsModActive("CETeam.CombatExtended_steam");

    public static readonly bool ColonyGroups = IsModActive("DerekBickley.LTOColonyGroupsFinal");

    public static readonly bool DeepStorage = IsModActive("LWM.DeepStorage");

    public static readonly bool Fortified = IsModActive("AOBA.Framework");

    public static readonly bool DrakkenLaserDrill = IsModActive("MYDE.DrakkenLaserDrill") || IsModActive("Mlie.DrakkenLaserDrill");

    public static readonly bool DrillTurret = IsModActive("Mlie.MiningCoDrillTurret");

    public static class DubsBadHygiene
    {
        public static readonly bool Active = IsModActive("Dubwise.DubsBadHygiene") || IsModActive("Dubwise.DubsBadHygiene.Lite");

        public static readonly bool LiteMode;

        public static readonly Type SectionLayer_ThingsSewagePipe;

        public static readonly Type SectionLayer_SewagePipeOverlay;

        public static readonly Type SectionLayer_AirDuctOverlay;

        public static readonly Type SectionLayer_Irrigation;

        public static readonly Type SectionLayer_FertilizerGrid;

        public static readonly Type Building_Pipe;

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
                    CompProperties_Pipe = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.CompProperties_Pipe", "DubsBadHygiene");
                    CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
                    SectionLayer_PipeOverlay_mode = AccessTools.FieldRefAccess<int>("DubsBadHygiene.SectionLayer_PipeOverlay:mode");
                }
                catch (Exception ex)
                {
                    LogError(ex);
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
        public static readonly bool Active = IsModActive("Dubwise.Rimefeller");

        public static readonly Type SectionLayer_SewagePipe;

        public static readonly Type SectionLayer_ThingsPipe;

        public static readonly Type XSectionLayer_Napalm;

        public static readonly Type XSectionLayer_OilSpill;

        public static readonly Type Building_Pipe;

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
                    CompProperties_Pipe = GenTypes.GetTypeInAnyAssembly("Rimefeller.CompProperties_Pipe", "Rimefeller");
                    CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
                    SectionLayer_PipeOverlay_mode = AccessTools.FieldRefAccess<int>("Rimefeller.SectionLayer_PipeOverlay:mode");
                }
                catch (Exception ex)
                {
                    LogError(ex);
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
        public static readonly bool Active = IsModActive("Aelanna.EccentricTech.DefenseGrid");

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
                    LogError(ex);
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

    public static readonly bool ExosuitFramework = IsModActive("Aoba.Exosuit.Framework");

    public static readonly bool GiantImperialTurret = IsModActive("XMB.Giantimperialcannonturret.MO");

    public static readonly bool Gunplay = IsModActive("automatic.gunplay");

    public static class MeleeAnimation
    {
        public static readonly bool Active = IsModActive("co.uk.epicguru.meleeanimation");
        
        public static readonly AccessTools.FieldRef<object, Map> AnimRenderer_Map;

        public static readonly AccessTools.FieldRef<object, Matrix4x4> AnimRenderer_RootTransform;

        public static readonly AccessTools.FieldRef<object, Def> AnimRenderer_Def;

        public static readonly AccessTools.FieldRef<Def, IReadOnlyList<object>> AnimRenderer_cellData;

        public static readonly MethodInfo m_GetWorldPosition;

        public static readonly MethodInfo m_GetWorldPositionOffset;

        static MeleeAnimation()
        {
            if (Active)
            {
                try
                {
                    AnimRenderer_Map = AccessTools.FieldRefAccess<Map>("AM.AnimRenderer:Map");
                    AnimRenderer_RootTransform = AccessTools.FieldRefAccess<Matrix4x4>("AM.AnimRenderer:RootTransform");
                    AnimRenderer_Def = AccessTools.FieldRefAccess<Def>("AM.AnimRenderer:Def");
                    AnimRenderer_cellData = AccessTools.FieldRefAccess<IReadOnlyList<object>>("AM.AnimDef:cellData");
                    m_GetWorldPosition = AccessTools.Method("AnimPartSnapshot:GetWorldPosition");
                    m_GetWorldPositionOffset = AccessTools.Method(typeof(Patch_AnimRenderer_DrawPawns), nameof(Patch_AnimRenderer_DrawPawns.GetWorldPositionOffset));
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(AnimRenderer_Map, AnimRenderer_RootTransform, AnimRenderer_Def, AnimRenderer_cellData))
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
        public static readonly bool Active = IsModActive("Haplo.Miscellaneous.Robots");

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
                    LogError(ex);
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

    public static readonly bool MuzzleFlash = IsModActive("IssacZhuang.MuzzleFlash");

    public static readonly bool ProjectRimFactory = IsModActive("spdskatr.projectrimfactory");

    public static readonly bool SmarterConstruction = IsModActive("dhultgren.smarterconstruction");

    public static readonly bool TabulaRasa = IsModActive("neronix17.toolbox");

    public static class VFECore
    {
        public static readonly bool Active = IsModActive("OskarPotocki.VanillaFactionsExpanded.Core");

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
                    LogError(ex);
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

    public static readonly bool VFEArchitect = IsModActive("VanillaExpanded.VFEArchitect");

    public static class VFESecurity
    {
        public static readonly bool Active = IsModActive("VanillaExpanded.VFESecurity");
        
        public static readonly AccessTools.FieldRef<object, GlobalTargetInfo> worldTarget;
        
        public static readonly AccessTools.FieldRef<object, int> worldMapAttackRange;
    
        static VFESecurity()
        {
            if (Active)
            {
                try
                {
                    worldTarget = AccessTools.FieldRefAccess<GlobalTargetInfo>("VFESecurity.CompWorldArtillery:worldTarget");
                    worldMapAttackRange = AccessTools.FieldRefAccess<int>("VFESecurity.CompProperties_WorldArtillery:worldMapAttackRange");
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(worldTarget, worldMapAttackRange))
                    {
                        LogIncompat("VFE Security");
                        Active = false;
                    }
                }
            }
        }
    }

    public static class VFEFactory
    {
        public static readonly bool Active = IsModActive("VanillaExpanded.VFEFactory");
    }

    public static class VVE
    {
        public static readonly bool Active = IsModActive("OskarPotocki.VanillaVehiclesExpanded");

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
                    LogError(ex);
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

    public static readonly bool VGE = IsModActive("vanillaexpanded.gravship");
    
    public static readonly bool VQEGenerator = IsModActive("vanillaquestsexpanded.generator");

    public static class VTE
    {
        public static readonly bool Active = IsModActive("VanillaExpanded.Temperature");

        public static readonly Type ProxyHeatManager;

        public static readonly FastInvokeHandler RemoveComp;
        
        static VTE()
        {
            if (Active)
            {
                try
                {
                    ProxyHeatManager = GenTypes.GetTypeInAnyAssembly("ProxyHeat.ProxyHeatManager", "ProxyHeat");
                    RemoveComp = MethodInvoker.GetHandler(AccessTools.Method(ProxyHeatManager, "RemoveComp"));
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(ProxyHeatManager, RemoveComp))
                    {
                        LogIncompat("Vanilla Temperature Expanded");
                        Active = false;
                    }
                }
            }
        }
    }

    public static readonly bool Vivi = IsModActive("gguake.race.vivi");
    
    public static readonly bool WASDedPawn = IsModActive("addvans.WASDedPawn");

    public static readonly bool WhileYoureUp = IsModActive("CodeOptimist.JobsOfOpportunity") || IsModActive("zsbk.patch16.whileyoureup");
    
    public static readonly bool WVCWorkModes = IsModActive("wvc.sergkart.biotech.MoreMechanoidsWorkModes");

    public static readonly bool YayosCombat3 = IsModActive("Mlie.YayosCombat3");

    public static readonly bool PickUpAndHaul = IsModActive("Mehni.PickUpAndHaul") || IsModActive("Teemo.PickUpAndHaulForked");

    public static class EnergyShield
    {
        public static readonly bool Active = IsModActive("zhuzi.AdvancedEnergy.Shields");

        public static readonly Type Building_Shield;

        public static readonly bool CECompat;

        static EnergyShield()
        {
            if (Active)
            {
                try
                {
                    Building_Shield = AccessTools.TypeByName("zhuzi.AdvancedEnergy.Shields.Shields.Building_Shield");
                    CECompat = IsModActive("cn.zhuzijun.EnergyShieldCECompat");
                }
                catch (Exception ex)
                {
                    LogError(ex);
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

    public static readonly bool TraderShips = IsModActive("automatic.traderships");

    public static readonly bool NightmareCore = IsModActive("Nightmare.Core");
    
    public static class Aquariums
    {
        public static readonly bool Active = IsModActive("Nightmare.Aquariums");

        public static readonly FastInvokeHandler CurrentTank;

        static Aquariums()
        {
            if (Active)
            {
                try
                {
                    CurrentTank =
                        MethodInvoker.GetHandler(AccessTools.PropertyGetter("Aquariums.AquariumFish:CurrentTank"));
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(CurrentTank))
                    {
                        LogIncompat("Aquariums");
                    }
                }
            }
        }
    }

    public static readonly bool SmartPistol = IsModActive("rabiosus.smartpistol");
    
    public static readonly bool RealFogOfWar = IsModActive("Mlie.NWNRealFogOfWar");

    public static readonly bool ReGrowth = IsModActive("ReGrowth.BOTR.Core");

    public static class Rimatomics
    {
        public static readonly bool Active = IsModActive("Dubwise.Rimatomics");
        
        public static readonly Type CompProperties_Pipe;
        
        public static readonly AccessTools.FieldRef<object, int> CompProperties_Pipe_mode;
        
        public static readonly AccessTools.FieldRef<object, int> SectionLayer_OverlayPipe_mode;

        public static readonly List<Type> SectionLayer_OverlayPipes;

        public static readonly Type Designator_RemovePipe;
        
        public static readonly AccessTools.FieldRef<Designator, int> Designator_RemovePipe_RemovalMode;

        public static readonly Type SectionLayer_ThingsPipe;

        public static readonly Type BaseMissile;
        
        static Rimatomics()
        {
            if (Active)
            {
                try
                {
                    var t_SectionLayer_OverlayPipe =
                        GenTypes.GetTypeInAnyAssembly("Rimatomics.SectionLayer_OverlayPipe", "Rimatomics");
                    CompProperties_Pipe = GenTypes.GetTypeInAnyAssembly("Rimatomics.CompProperties_Pipe", "Rimatomics");
                    CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
                    SectionLayer_OverlayPipe_mode = AccessTools.FieldRefAccess<int>(t_SectionLayer_OverlayPipe, "mode");
                    SectionLayer_OverlayPipes = t_SectionLayer_OverlayPipe.AllSubclassesNonAbstract();
                    Designator_RemovePipe = GenTypes.GetTypeInAnyAssembly("Rimatomics.Designator_RemovePipe", "Rimatomics");
                    Designator_RemovePipe_RemovalMode = AccessTools.FieldRefAccess<int>(Designator_RemovePipe, "RemovalMode");
                    SectionLayer_ThingsPipe = GenTypes.GetTypeInAnyAssembly("Rimatomics.SectionLayer_ThingsPipe", "Rimatomics");
                    BaseMissile = GenTypes.GetTypeInAnyAssembly("Rimatomics.BaseMissile", "Rimatomics");
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    Active = false;
                }
                finally
                {
                    if (AnyNull(CompProperties_Pipe, CompProperties_Pipe_mode, SectionLayer_OverlayPipe_mode,
                            SectionLayer_OverlayPipes, SectionLayer_ThingsPipe, Designator_RemovePipe,
                            Designator_RemovePipe_RemovalMode))
                    {
                        LogIncompat("Rimatomics");
                        Active = false;
                    }
                }
            }
        }
    }

    public static class SmartFarming
    {
        public static readonly bool SmartFarmingActive = IsModActive("Owlchemist.SmartFarming");

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
                    if (SmartFarmingActive && ReGrowth && !UnitTestDetector.IsTestingContext)
                    {
                        VMF_Log.Error("When both Smart Farming and ReGrowth 2 are enabled, a patch error will occur. Since these have overlapping functionality, please enable only one of them.");
                    }
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
                    LogError(ex);
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

    public static readonly bool RimWorldOfMagic = IsModActive("Torann.ARimworldOfMagic");

    public static readonly bool CeleTech = IsModActive("TOT.CeleTech.MKIII");

    public static readonly bool PauseOtherSettlements = IsModActive("esvn.PauseOtherSettlementsSimulation");

    public static class MultiFloors
    {
        public static readonly bool Active = IsModActive("telardo.MultiFloors") || IsModActive("telardo.MultiFloorsDev");

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
                    LogError(ex);
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

    public static readonly bool CutPlantsBeforeBuilding = IsModActive("Mlie.CutPlantsBeforeBuilding");

    public static class StackGap
    {
        public static readonly bool Active = IsModActive("Andromeda.StackGap");

        public const string HarmonyId = "Andromeda.StackGap";
    }

    public static readonly bool AnimalCages = IsModActive("zal.animalcages");

    public static readonly bool DoNotHitMe = IsModActive("Og.do.not.hit.me");
    
    public static readonly bool AutoApparelPickup = IsModActive("Scorpio.AutoApparelPickup");

    public static class ProgressionEducation
    {
        public const string HarmonyId = "ProgressionEducationMod";
    }

    public static class ColonyManager
    {
        public static readonly bool Active = IsModActive("ilyvion.colonymanagerredux");

        static ColonyManager()
        {
            if (Active)
            {
                var type = GenTypes.GetTypeInAnyAssembly("ColonyManagerRedux.WorkGiver_Manage", "ColonyManagerRedux");
                if (type is not null)
                    JobAcrossMapsUtility.WorkGiverClassesNeedWrap.Add(type);
                else LogIncompat("ColonyManagerRedux");
            }
        }
    }

    public static class GestaltEngine
    {
        public const string HarmonyId = "GestaltEngine.Mod";
    }
}