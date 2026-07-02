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
using SmashTools;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

internal static class UnitTestDetector
{
  [UsedImplicitly] internal static bool IsTestingContext { get; set; }
}

[StaticConstructorOnStartup]
internal static class ModCompat
{
  public static readonly bool AllowTool = IsModActive("UnlimitedHugs.AllowTool");

  public static readonly bool BillDoorsFramework = IsModActive("3HSTltd.Framework");

  public static readonly bool CombatExtended = IsModActive("CETeam.CombatExtended") || IsModActive("CETeam.CombatExtended_steam");

  public static readonly bool ColonyGroups = IsModActive("DerekBickley.LTOColonyGroupsFinal");

  public static readonly bool DeepStorage = IsModActive("LWM.DeepStorage");

  public static readonly bool Fortified = IsModActive("AOBA.Framework");

  public static readonly bool DrakkenLaserDrill = IsModActive("MYDE.DrakkenLaserDrill") || IsModActive("Mlie.DrakkenLaserDrill");

  public static readonly bool DrillTurret = IsModActive("Mlie.MiningCoDrillTurret");

  public static readonly bool ExosuitFramework = IsModActive("Aoba.Exosuit.Framework");

  public static readonly bool GiantImperialTurret = IsModActive("XMB.Giantimperialcannonturret.MO");

  public static readonly bool Gunplay = IsModActive("automatic.gunplay");

  public static readonly bool HospitalityCasino = IsModActive("Adamas.HospitalityCasino");

  public static readonly bool IRBM = IsModActive("kazepsi.irbm");

  public static readonly bool MuzzleFlash = IsModActive("IssacZhuang.MuzzleFlash");

  public static readonly bool ProjectRimFactory = IsModActive("spdskatr.projectrimfactory");

  public static readonly bool SmarterConstruction = IsModActive("dhultgren.smarterconstruction");

  public static readonly bool TabulaRasa = IsModActive("neronix17.toolbox");

  public static readonly bool VFEArchitect = IsModActive("VanillaExpanded.VFEArchitect");

  public static readonly bool VPsyE = IsModActive("VanillaExpanded.VPsycastsE");

  public static readonly bool VGE = IsModActive("vanillaexpanded.gravship");

  public static readonly bool VQEGenerator = IsModActive("vanillaquestsexpanded.generator");

  public static readonly bool Vivi = IsModActive("gguake.race.vivi");

  public static readonly bool WASDedPawn = IsModActive("addvans.WASDedPawn");

  public static readonly bool WhileYoureUp = IsModActive("CodeOptimist.JobsOfOpportunity") || IsModActive("zsbk.patch16.whileyoureup");

  public static readonly bool WVCWorkModes = IsModActive("wvc.sergkart.biotech.MoreMechanoidsWorkModes");

  public static readonly bool YayosCombat3 = IsModActive("Mlie.YayosCombat3");

  public static readonly bool PickUpAndHaul = IsModActive("Mehni.PickUpAndHaul");

  public static readonly bool TraderShips = IsModActive("automatic.traderships");

  public static readonly bool UFHeavyIndustries = IsModActive("KindSeal.LOL");

  public static readonly bool NightmareCore = IsModActive("Nightmare.Core");

  public static readonly bool SmartPistol = IsModActive("rabiosus.smartpistol");

  public static readonly bool SRALib = IsModActive("DiZhuan.SRALib");

  public static readonly bool RealFogOfWar = IsModActive("Mlie.NWNRealFogOfWar");

  public static readonly bool RimWorldOfMagic = IsModActive("Torann.ARimworldOfMagic");

  public static readonly bool CeleTech = IsModActive("TOT.CeleTech.MKIII");

  public static readonly bool PerspectiveShift = IsModActive("ferny.PerspectiveShift");

  public static readonly bool PauseOtherSettlements = IsModActive("esvn.PauseOtherSettlementsSimulation");

  public static readonly bool CutPlantsBeforeBuilding = IsModActive("Mlie.CutPlantsBeforeBuilding");

  public static readonly bool AnimalCages = IsModActive("zal.animalcages");

  public static readonly bool DoNotHitMe = IsModActive("Og.do.not.hit.me");

  public static readonly bool AutoApparelPickup = IsModActive("Scorpio.AutoApparelPickup");

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

  [UsedImplicitly] internal static ThreadLocal<Exception> CctorException { get; private set; }

  internal static bool AnyNull(params object[] args)
  {
    for (var i = 0; i < args.Length; i++)
    {
      if (args[i] is null)
      {
        LogError(new Exception($"Argument {i} is null."));
        return true;
      }
    }

    return false;
  }

  internal static void LogIncompat(string modName)
  {
    if (!UnitTestDetector.IsTestingContext)
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
  
  public abstract class CompatBase<T> where T : CompatBase<T>
  {
    // ReSharper disable once StaticMemberInGenericType
    public static bool Active { get; private set; }

    protected static void Initialize(string packageId, Action initialize, params Span<string> alternativeIds)
    {
      Active = IsModActive(packageId);
      if (!Active && !alternativeIds.IsEmpty)
      {
        foreach (var alternativeId in alternativeIds)
        {
          if (IsModActive(alternativeId))
          {
            Active = true;
            break;
          }
        }
      }
      
      if (Active)
      {
        try
        {
          initialize();
        }
        catch (Exception ex)
        {
          LogError(ex);
          Active = false;
        }
        finally
        {
          if (AnyNull(AccessTools.GetDeclaredProperties(typeof(T)).Select(f => f.GetValue(null))))
          {
            LogIncompat(typeof(T).Name);
            Active = false;
          }
        }
      }
    }
  }

  public static class VehicleFramework
  {
    public const string HarmonyId = "SmashPhil.VehicleFramework";

    public static readonly Dictionary<(VehicleDef, Rot4), Texture2D> CachedVehicleTextures;
    public static readonly AccessTools.FieldRef<CompFueledTravel, CompPower> connectedPower;
    public static readonly AccessTools.StructFieldRef<MapGridOwners.PathConfig, VehicleDef> vehicleDef;

    static VehicleFramework()
    {
      if (!UnitTestDetector.IsTestingContext)
      {
        CachedVehicleTextures = AccessTools.StaticFieldRefAccess<Dictionary<(VehicleDef, Rot4), Texture2D>>(typeof(VehicleTex), nameof(CachedVehicleTextures));
        connectedPower = AccessTools.FieldRefAccess<CompFueledTravel, CompPower>(nameof(connectedPower));
        vehicleDef = AccessTools.StructFieldRefAccess<MapGridOwners.PathConfig, VehicleDef>(nameof(vehicleDef));
        if (AnyNull(CachedVehicleTextures, connectedPower))
        {
          LogIncompat("Vehicle Framework");
        }
      }
    }
  }

  public class AdaptiveStorage : CompatBase<AdaptiveStorage>
  {
    public static Type ThingClass { get; private set; }
    public static FastInvokeHandler Renderer { get; private set; }
    public static FastInvokeHandler SetAllPrintDatasDirty { get; private set; }
    private static Dictionary<Type, bool> SameOrSubClassDic { get; set; }

    static AdaptiveStorage()
    {
      Initialize("adaptive.storage.framework", () =>
      {
        ThingClass = AccessTools.TypeByName("AdaptiveStorage.ThingClass");
        Renderer = MethodInvoker.GetHandler(AccessTools.PropertyGetter(ThingClass, nameof(Renderer)));
        SetAllPrintDatasDirty =
          MethodInvoker.GetHandler(AccessTools.Method("AdaptiveStorage.StorageRenderer:SetAllPrintDatasDirty"));
        SameOrSubClassDic = [];
      });
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

  public class CallTradeShips : CompatBase<CallTradeShips>
  {
    public static Type Job_CallTradeShip { get; private set; }
    public static AccessTools.FieldRef<Job, TraderKindDef> TraderKindDef { get; private set; }
    public static AccessTools.FieldRef<Job, int> TraderKind { get; private set; }

    static CallTradeShips()
    {
      Initialize("calltradeships.kv.rw", () =>
      {
        Job_CallTradeShip =
          GenTypes.GetTypeInAnyAssembly("CallTradeShips.Job_CallTradeShip", nameof(CallTradeShips));
        TraderKindDef = AccessTools.FieldRefAccess<TraderKindDef>(Job_CallTradeShip, nameof(TraderKindDef));
        TraderKind = AccessTools.FieldRefAccess<int>(Job_CallTradeShip, nameof(TraderKind));
      });
    }
  }

  public class DubsBadHygiene : CompatBase<DubsBadHygiene>
  {
    public static bool LiteMode { get; private set; }
    public static Type SectionLayer_ThingsSewagePipe { get; private set; }
    public static Type SectionLayer_SewagePipeOverlay { get; private set; }
    public static Type SectionLayer_AirDuctOverlay { get; private set; }
    public static Type SectionLayer_Irrigation { get; private set; }
    public static Type SectionLayer_FertilizerGrid { get; private set; }
    public static Type CompProperties_Pipe { get; private set; }
    public static AccessTools.FieldRef<object, int> CompProperties_Pipe_mode { get; private set; }
    public static AccessTools.FieldRef<object, int> SectionLayer_PipeOverlay_mode { get; private set; }
    
    static DubsBadHygiene()
    {
      Initialize("Dubwise.DubsBadHygiene", () =>
      {
        Patch_JobGiver_Work_PawnCanUseWorkGiver.NoNeedVirtualMapTransferList.Add(
          GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.WorkGiver_PlaceFertilizer", nameof(DubsBadHygiene)));

        LiteMode = (bool)AccessTools.PropertyGetter("DubsBadHygiene.Settings:LiteMode").Invoke(null, null);
        if (LiteMode) return;

        SectionLayer_ThingsSewagePipe = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_SewagePipeOverlay", nameof(DubsBadHygiene));
        if (SectionLayer_ThingsSewagePipe != null)
        {
          VehicleSectionLayerManager.OrientedSectionLayerTypes.Add(SectionLayer_ThingsSewagePipe);
        }
        SectionLayer_SewagePipeOverlay = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_SewagePipeOverlay", nameof(DubsBadHygiene));
        SectionLayer_AirDuctOverlay = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_AirDuctOverlay", nameof(DubsBadHygiene));
        SectionLayer_Irrigation = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_Irrigation", nameof(DubsBadHygiene));
        SectionLayer_FertilizerGrid = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.SectionLayer_FertilizerGrid", nameof(DubsBadHygiene));
        CompProperties_Pipe = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.CompProperties_Pipe", nameof(DubsBadHygiene));
        CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
        SectionLayer_PipeOverlay_mode = AccessTools.FieldRefAccess<int>("DubsBadHygiene.SectionLayer_PipeOverlay:mode");
      }, "Dubwise.DubsBadHygiene.Lite");
    }
  }

  public class Rimefeller : CompatBase<Rimefeller>
  {
    public static Type SectionLayer_SewagePipe { get; private set; }
    public static Type SectionLayer_ThingsPipe { get; private set; }
    public static Type XSectionLayer_Napalm { get; private set; }
    public static Type XSectionLayer_OilSpill { get; private set; }
    public static Type CompProperties_Pipe { get; private set; }
    public static AccessTools.FieldRef<object, int> CompProperties_Pipe_mode { get; private set; }
    public static AccessTools.FieldRef<object, int> SectionLayer_PipeOverlay_mode { get; private set; }

    static Rimefeller()
    {
      Initialize("Dubwise.Rimefeller", () =>
      {
        SectionLayer_SewagePipe = GenTypes.GetTypeInAnyAssembly("Rimefeller.SectionLayer_SewagePipe", nameof(Rimefeller));
        SectionLayer_ThingsPipe = GenTypes.GetTypeInAnyAssembly("Rimefeller.SectionLayer_ThingsPipe", nameof(Rimefeller));
        if (SectionLayer_ThingsPipe != null)
        {
          VehicleSectionLayerManager.OrientedSectionLayerTypes.Add(SectionLayer_ThingsPipe);
        }
        XSectionLayer_Napalm = GenTypes.GetTypeInAnyAssembly("Rimefeller.XSectionLayer_Napalm", nameof(Rimefeller));
        XSectionLayer_OilSpill = GenTypes.GetTypeInAnyAssembly("Rimefeller.XSectionLayer_OilSpill", nameof(Rimefeller));
        CompProperties_Pipe = GenTypes.GetTypeInAnyAssembly("Rimefeller.CompProperties_Pipe", nameof(Rimefeller));
        CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
        SectionLayer_PipeOverlay_mode = AccessTools.FieldRefAccess<int>("Rimefeller.SectionLayer_PipeOverlay:mode");
      });
    }
  }

  public class DefenseGrid : CompatBase<DefenseGrid>
  {
    public static Type SectionLayer_DefenseGridOverlay { get; private set;}
    public static Type CompDefenseConduit { get; private set;}
    public static Type Designator_DeconstructConduit { get; private set;}
    public static Type InterceptorMapComponent { get; private set;}
    public static AccessTools.FieldRef<MapComponent, IList> grids { get; private set;}
    public static AccessTools.FieldRef<object, MapComponent> mapComponent { get; private set;}
    public static FastInvokeHandler RepaintGrid { get; private set;}
    public static FastInvokeHandler UnpaintGrid { get; private set;}

    static DefenseGrid()
    {
      Initialize("Aelanna.EccentricTech.DefenseGrid", () =>
      {
        SectionLayer_DefenseGridOverlay = AccessTools.TypeByName("EccentricDefenseGrid.SectionLayer_DefenseGridOverlay");
        CompDefenseConduit = AccessTools.TypeByName("EccentricDefenseGrid.CompDefenseConduit");
        Designator_DeconstructConduit = AccessTools.TypeByName("EccentricDefenseGrid.Designator_DeconstructConduit");
        InterceptorMapComponent = GenTypes.GetTypeInAnyAssembly("EccentricProjectiles.InterceptorMapComponent", "EccentricProjectiles");
        grids = AccessTools.FieldRefAccess<IList>(InterceptorMapComponent, nameof(grids));
        var t_InterceptorGrid = GenTypes.GetTypeInAnyAssembly("EccentricProjectiles.InterceptorGrid", "EccentricProjectiles");
        mapComponent = AccessTools.FieldRefAccess<MapComponent>(t_InterceptorGrid, nameof(mapComponent));
        RepaintGrid = MethodInvoker.GetHandler(AccessTools.Method(InterceptorMapComponent, nameof(RepaintGrid)));
        UnpaintGrid = MethodInvoker.GetHandler(AccessTools.Method(InterceptorMapComponent, nameof(UnpaintGrid)));
      });
    }
  }

  public class MeleeAnimation : CompatBase<MeleeAnimation>
  {
    public static AccessTools.FieldRef<object, Map> AnimRenderer_Map { get; private set;}
    public static AccessTools.FieldRef<object, Matrix4x4> AnimRenderer_RootTransform { get; private set;}
    public static AccessTools.FieldRef<object, Def> AnimRenderer_Def { get; private set;}
    public static AccessTools.FieldRef<Def, IReadOnlyList<object>> AnimRenderer_cellData { get; private set;}
    public static MethodInfo m_GetWorldPosition { get; private set;}
    public static MethodInfo m_GetWorldPositionOffset { get; private set;}

    static MeleeAnimation()
    {
      Initialize("co.uk.epicguru.meleeanimation", () =>
      {
        AnimRenderer_Map = AccessTools.FieldRefAccess<Map>("AM.AnimRenderer:Map");
        AnimRenderer_RootTransform = AccessTools.FieldRefAccess<Matrix4x4>("AM.AnimRenderer:RootTransform");
        AnimRenderer_Def = AccessTools.FieldRefAccess<Def>("AM.AnimRenderer:Def");
        AnimRenderer_cellData = AccessTools.FieldRefAccess<IReadOnlyList<object>>("AM.AnimDef:cellData");
        m_GetWorldPosition = AccessTools.Method("AnimPartSnapshot:GetWorldPosition");
        m_GetWorldPositionOffset = ((Delegate)Patch_AnimRenderer_DrawPawns.GetWorldPositionOffset).Method;
      });
    }
  }

  public class MiscRobots : CompatBase<MiscRobots>
  {
    public static Type X2_AIRobot { get; private set; }
    public static AccessTools.FieldRef<Pawn, Building> rechargeStation { get; private set;}

    static MiscRobots()
    {
      Initialize("Haplo.Miscellaneous.Robots", () =>
      {
        X2_AIRobot = GenTypes.GetTypeInAnyAssembly("AIRobot.X2_AIRobot", "AIRobot");
        rechargeStation = AccessTools.FieldRefAccess<Building>("AIRobot.X2_AIRobot:rechargeStation");
      });
    }
  }

  public class VFECore : CompatBase<VFECore>
  {
    public static Type PipeNetDef { get; private set; }
    public static Type SectionLayer_Resource { get; private set;}
    public static FastInvokeHandler ShouldDraw { get; private set;}
    public static AccessTools.FieldRef<Def> pipeNetDef { get; private set;}

    static VFECore()
    {
      Initialize("OskarPotocki.VanillaFactionsExpanded.Core", () =>
      {
        PipeNetDef = GenTypes.GetTypeInAnyAssembly("PipeSystem.PipeNetDef");
        SectionLayer_Resource = GenTypes.GetTypeInAnyAssembly("PipeSystem.SectionLayer_Resource", "PipeSystem");
        ShouldDraw = MethodInvoker.GetHandler(AccessTools.PropertyGetter(SectionLayer_Resource, nameof(ShouldDraw)));
        pipeNetDef = AccessTools.StaticFieldRefAccess<Def>(AccessTools.Field(SectionLayer_Resource, "pipeNet"));
      });
    }
  }

  public class VFESecurity : CompatBase<VFESecurity>
  {
    public static AccessTools.FieldRef<object, GlobalTargetInfo> worldTarget { get; private set;}
    public static AccessTools.FieldRef<object, int> worldMapAttackRange { get; private set;}

    static VFESecurity()
    {
      Initialize("VanillaExpanded.VFESecurity", () =>
      {
        worldTarget = AccessTools.FieldRefAccess<GlobalTargetInfo>("VFESecurity.CompWorldArtillery:worldTarget");
        worldMapAttackRange = AccessTools.FieldRefAccess<int>("VFESecurity.CompProperties_WorldArtillery:worldMapAttackRange");
      });
    }
  }

  public class VFEFactory : CompatBase<VFEFactory>
  {
    static VFEFactory()
    {
      Initialize("VanillaExpanded.VFEFactory", () => { });
    }
  }

  public class VanillaVehiclesExpanded : CompatBase<VanillaVehiclesExpanded>
  {
    public static AccessTools.FieldRef<CompProperties, float> refuelAmountPerTick { get; private set;}

    static VanillaVehiclesExpanded()
    {
      Initialize("OskarPotocki.VanillaVehiclesExpanded", () =>
      {
        refuelAmountPerTick = AccessTools.FieldRefAccess<float>("VanillaVehiclesExpanded.CompProperties_RefuelingPump:refuelAmountPerTick");
      });
    }
  }

  public class VanillaTemperatureExpanded : CompatBase<VanillaTemperatureExpanded>
  {
    public static Type ProxyHeatManager { get; private set; }
    public static FastInvokeHandler RemoveComp { get; private set; }

    static VanillaTemperatureExpanded()
    {
      Initialize("VanillaExpanded.Temperature", () =>
      {
        ProxyHeatManager = GenTypes.GetTypeInAnyAssembly("ProxyHeat.ProxyHeatManager", "ProxyHeat");
        RemoveComp = MethodInvoker.GetHandler(AccessTools.Method(ProxyHeatManager, nameof(RemoveComp)));
      });
    }
  }

  public class EnergyShield : CompatBase<EnergyShield>
  {
    public static Type Building_Shield { get; private set;}
    public static bool CECompat { get; private set;}

    static EnergyShield()
    {
      Initialize("zhuzi.AdvancedEnergy.Shields", () =>
      {
        Building_Shield = AccessTools.TypeByName("zhuzi.AdvancedEnergy.Shields.Shields.Building_Shield");
        CECompat = IsModActive("cn.zhuzijun.EnergyShieldCECompat");
      });
    }
  }

  public class Aquariums : CompatBase<Aquariums>
  {
    public static FastInvokeHandler CurrentTank { get; private set;}

    static Aquariums()
    {
      Initialize("Nightmare.Aquariums", () =>
      {
        CurrentTank =
          MethodInvoker.GetHandler(AccessTools.PropertyGetter("Aquariums.AquariumFish:CurrentTank"));
      });
    }
  }

  public class Rimatomics : CompatBase<Rimatomics>
  {
    public static Type CompProperties_Pipe { get; private set;}
    public static AccessTools.FieldRef<object, int> CompProperties_Pipe_mode { get; private set;}
    public static AccessTools.FieldRef<object, int> SectionLayer_OverlayPipe_mode { get; private set;}
    public static List<Type> SectionLayer_OverlayPipes { get; private set;}
    public static Type Designator_RemovePipe { get; private set;}
    public static AccessTools.FieldRef<Designator, int> Designator_RemovePipe_RemovalMode { get; private set;}
    public static Type SectionLayer_ThingsPipe { get; private set;}
    public static Type BaseMissile { get; private set;}

    static Rimatomics()
    {
      Initialize("Dubwise.Rimatomics", () =>
      {
        var t_SectionLayer_OverlayPipe =
          GenTypes.GetTypeInAnyAssembly("Rimatomics.SectionLayer_OverlayPipe", nameof(Rimatomics));
        CompProperties_Pipe = GenTypes.GetTypeInAnyAssembly("Rimatomics.CompProperties_Pipe", nameof(Rimatomics));
        CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
        SectionLayer_OverlayPipe_mode = AccessTools.FieldRefAccess<int>(t_SectionLayer_OverlayPipe, "mode");
        SectionLayer_OverlayPipes = t_SectionLayer_OverlayPipe.AllSubclassesNonAbstract();
        Designator_RemovePipe = GenTypes.GetTypeInAnyAssembly("Rimatomics.Designator_RemovePipe", nameof(Rimatomics));
        Designator_RemovePipe_RemovalMode = AccessTools.FieldRefAccess<int>(Designator_RemovePipe, "RemovalMode");
        SectionLayer_ThingsPipe = GenTypes.GetTypeInAnyAssembly("Rimatomics.SectionLayer_ThingsPipe", nameof(Rimatomics));
        BaseMissile = GenTypes.GetTypeInAnyAssembly("Rimatomics.BaseMissile", nameof(Rimatomics));
      });
    }
  }

  public class SmartFarming : CompatBase<SmartFarming>
  {
    private const string SmartFarmingPackageId = "Owlchemist.SmartFarming";
    private const string ReGrowthPackageId = "ReGrowth.BOTR.Core";
    public static readonly bool SmartFarmingActive = IsModActive(SmartFarmingPackageId);
    public static readonly bool ReGrowthActive = IsModActive(ReGrowthPackageId);
    public static Type MapComponent_SmartFarming { get; private set;}
    public static AccessTools.FieldRef<MapComponent, IDictionary> growZoneRegistry { get; private set;}
    public static AccessTools.FieldRef<object, int> priority { get; private set;}

    static SmartFarming()
    {
      Initialize(SmartFarmingPackageId, () =>
      {
        if (SmartFarmingActive && ReGrowthActive && !UnitTestDetector.IsTestingContext)
        {
          VMF_Log.Error("When both Smart Farming and ReGrowth 2 are enabled, a patch error will occur. Since these have overlapping functionality, please enable only one of them.");
        }
        Type t_ZoneData;
        if (SmartFarmingActive)
        {
          MapComponent_SmartFarming = GenTypes.GetTypeInAnyAssembly("SmartFarming.MapComponent_SmartFarming", nameof(SmartFarming));
          t_ZoneData = GenTypes.GetTypeInAnyAssembly("SmartFarming.ZoneData", nameof(SmartFarming));
        }
        else
        {
          MapComponent_SmartFarming = GenTypes.GetTypeInAnyAssembly("ReGrowthCore.MapComponent_SmartFarming", "ReGrowthCore");
          t_ZoneData = GenTypes.GetTypeInAnyAssembly("ReGrowthCore.ZoneData", "ReGrowthCore");
        }
        growZoneRegistry = AccessTools.FieldRefAccess<IDictionary>(MapComponent_SmartFarming, nameof(growZoneRegistry));
        priority = AccessTools.FieldRefAccess<int>(t_ZoneData, nameof(priority));
      }, ReGrowthPackageId);
    }
  }

  public class MultiFloors : CompatBase<MultiFloors>
  {
    public static Func<Map, Map> GroundMap { get; private set;}
    public static Func<Map, int> GetLevel { get; private set;}
    public static Action<Map> RevalidateLaunchSiteState { get; private set;}
    public static Type SectionLayer_LowerLevel { get; private set;}
    private static Func<Map, MapComponent> GetCachedLevelMapComp { get; set;}
    private static FastInvokeHandler GetOtherMapVerticallyOutwardFromCache { get; set;}

    private static readonly object MinusOne = -1;

    static MultiFloors()
    {
      Initialize("telardo.MultiFloors", () =>
      {
        GroundMap = AccessTools.MethodDelegate<Func<Map, Map>>("MultiFloors.HarmonyPatches.HarmonyPatch_CallBossGroupOnGround:GetGroundMap");
        GetLevel = AccessTools.MethodDelegate<Func<Map, int>>("MultiFloors.HarmonyPatches.HarmonyPatch_SortMapInColonistBarByLevel:GetLevel");
        RevalidateLaunchSiteState = AccessTools.MethodDelegate<Action<Map>>("MultiFloors.HarmonyPatches.HarmonyPatch_OnGravshipLaunch:RevalidateLaunchSiteState");
        SectionLayer_LowerLevel = GenTypes.GetTypeInAnyAssembly("MultiFloors.Maps.SectionLayer_LowerLevel", "MultiFloors.Maps");
        if (SectionLayer_LowerLevel is not null)
        {
          VehicleSectionLayerManager.OrientedSectionLayerTypes.Add(SectionLayer_LowerLevel);
        }
        GetCachedLevelMapComp = AccessTools.MethodDelegate<Func<Map, MapComponent>>(
          AccessTools.Method(
            typeof(MapComponentCache<>).MakeGenericType(GenTypes.GetTypeInAnyAssembly("MultiFloors.MF_LevelMapComp", nameof(MultiFloors))),
            "GetComponent",
            [typeof(Map)]));
        GetOtherMapVerticallyOutwardFromCache = MethodInvoker.GetHandler(AccessTools.Method("MultiFloors.LevelUtility:GetOtherMapVerticallyOutwardFromCache"));
      }, "telardo.MultiFloorsDev");
    }

    public static IEnumerable<Map> GetOtherLevels(Map map)
    {
      return (IEnumerable<Map>)GetOtherMapVerticallyOutwardFromCache(null,
        Params<ValueTuple<object, object, object>>.Get((map, GetCachedLevelMapComp(map), MinusOne)));
    }
  }

  public class StackGap : CompatBase<StackGap>
  {
    public const string HarmonyId = "Andromeda.StackGap";

    static StackGap()
    {
      Initialize("Andromeda.StackGap", () => { });
    }
  }

  public static class ProgressionEducation
  {
    public const string HarmonyId = "ProgressionEducationMod";
  }

  public class ColonyManagerRedux : CompatBase<ColonyManagerRedux>
  {
    static ColonyManagerRedux()
    {
      Initialize("ilyvion.colonymanagerredux", () =>
      {
        var type = GenTypes.GetTypeInAnyAssembly("ColonyManagerRedux.WorkGiver_Manage", nameof(ColonyManagerRedux));
        if (type is not null)
          JobAcrossMapsUtility.WorkGiverClassesNeedWrap.Add(type);
        else LogIncompat(nameof(ColonyManagerRedux));
      });
    }
  }

  public static class GestaltEngine
  {
    public const string HarmonyId = "GestaltEngine.Mod";
  }

  public class PowerPoles : CompatBase<PowerPoles>
  {
    public static Type Building_LongDistancePower { get; private set; }
    public static Type Building_LongDistanceCabled { get; private set; }
    public static FastInvokeHandler IsLinkedTo { get; private set; }
    public static FastInvokeHandler TryRemoveLink { get; private set; }
    public static FastInvokeHandler GeneratePointsAsync { get; private set; }
    public static AccessTools.FieldRef<float> CableMaxDistance { get; private set; }
    public static AccessTools.FieldRef<Thing, IDictionary> connectionToPoints { get; private set; }

    static PowerPoles()
    {
      Initialize("co.uk.epicguru.rimforgepoles", () =>
      {
        Building_LongDistancePower = GenTypes.GetTypeInAnyAssembly("RimForge.Buildings.Building_LongDistancePower", "RimForge.Buildings");
        Building_LongDistanceCabled = GenTypes.GetTypeInAnyAssembly("RimForge.Buildings.Building_LongDistanceCabled", "RimForge.Buildings");
        IsLinkedTo = MethodInvoker.GetHandler(AccessTools.Method(Building_LongDistancePower, "IsLinkedTo"));
        TryRemoveLink = MethodInvoker.GetHandler(AccessTools.Method(Building_LongDistancePower, "TryRemoveLink"));
        GeneratePointsAsync = MethodInvoker.GetHandler(AccessTools.Method(Building_LongDistanceCabled, "GeneratePointsAsync"));
        CableMaxDistance = AccessTools.StaticFieldRefAccess<float>(AccessTools.Field("RimForge.PolesSettings.PolesModSettings:CableMaxDistance"));
        connectionToPoints = AccessTools.FieldRefAccess<Thing, IDictionary>(AccessTools.Field(Building_LongDistanceCabled, "connectionToPoints"));
      });
    }
  }

  public class VehicleRaidFramework : CompatBase<VehicleRaidFramework>
  {
    public const string HarmonyId = "VRF.VehicleRaidFramework";
    public static Type CompVehicleHover { get; private set; }
    public static AccessTools.FieldRef<VehicleComp, int> State { get; private set; }
    
    static VehicleRaidFramework()
    {
      Initialize("gabrieel1482.raidvehicleframework", () =>
      {
        CompVehicleHover = GenTypes.GetTypeInAnyAssembly("VehicleRaid.CompVehicleHover", "VehicleRaid");
        State = AccessTools.FieldRefAccess<int>(CompVehicleHover, "State");
      });
    }
  }

  public class DefensiveNetwork : CompatBase<DefensiveNetwork>
  {
    public static FastInvokeHandler CountWatchersTargeting;
    
    static DefensiveNetwork()
    {
      Initialize("wuhuansuiyue.defensivenetworkexpanded", () =>
      {
        CountWatchersTargeting = MethodInvoker.GetHandler(
          AccessTools.Method(
            GenTypes.GetTypeInAnyAssembly("DNX.WatcherTargetingUtility", "DNX"),
            "CountWatchersTargeting"));
      });
    }
  }
}
