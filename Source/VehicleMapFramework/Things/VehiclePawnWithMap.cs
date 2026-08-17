using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.World;
using Verse;
using Verse.AI;
using Verse.AI.Group;
#if DEV
using Vehicles.Rendering;
#endif

namespace VehicleMapFramework;

public class MapVehicleEventDef : Def;

[StaticConstructorOnStartup]
public class VehiclePawnWithMap : VehiclePawn, IEventManager<MapVehicleEventDef>, IAttackTarget
{
  private bool generatingVehicleMap;
  private Map interiorMap;
  private VehicleMapFollower mapFollower;

  public Vector3 cachedDrawPos;
  public Vector3 cachedExactPos;

  private bool allowEnter = true;
  private bool allowExit = true;

  public bool impassableCellsDirty = true;
  public bool mapEdgeCellsDirty = true;
  public bool walkableCellsDirty = true;
  public bool enterPositionsDirty = true;
  private int cellDesignationsDirtyTick;
  private int vehicleCaravanOrStashedVehicleCachedTick;

  internal bool resizeRequest;

  private readonly List<CompVehicleEnterSpot> tmpEnterComps = [];

  private static readonly Material ClipMat =
    SolidColorMaterials.NewSolidColorMaterial(new Color(0.3f, 0.1f, 0.1f, 0.5f), ShaderDatabase.MetaOverlay);

  private static readonly CachedTexture iconIncreasePriority = new("VehicleMapFramework/UI/IncreasePriority");

  private static readonly CachedTexture iconDecreasePriority = new("VehicleMapFramework/UI/DecreasePriority");

  private static readonly CachedTexture iconAllowEnter = new("VehicleMapFramework/UI/AllowEnter");
  private static readonly CachedTexture iconAllowExit = new("VehicleMapFramework/UI/AllowExit");
  private static readonly CachedTexture iconEye = new("VehicleMapFramework/UI/Eye");

  private static readonly Type t_SectionLayer_Zones =
    GenTypes.GetTypeInAnyAssembly("Verse.SectionLayer_Zones", "Verse");

  private static readonly FastInvokeHandler DirtyCellDesignationsCache =
    MethodInvoker.GetHandler(AccessTools.Method(typeof(DesignationManager), "DirtyCellDesignationsCache"));

  private static readonly List<DesignationDef> cellDesignations =
    [.. DefDatabase<DesignationDef>.AllDefs.Where(d => d.targetType == TargetType.Cell)];

  internal static readonly AccessTools.FieldRef<MapDrawer, Section[,]> sections =
    AccessTools.FieldRefAccess<MapDrawer, Section[,]>("sections");

  EventManager<MapVehicleEventDef> IEventManager<MapVehicleEventDef>.EventRegistry { get; set; }

  public EventManager<MapVehicleEventDef> MapVehicleEventManager =>
    ((IEventManager<MapVehicleEventDef>)this).EventRegistry;
  
  public VehicleMapProps VehicleMapProps => field ??= def.GetModExtension<VehicleMapProps>();

  public Map VehicleMap
  {
    get
    {
      if (interiorMap == null)
      {
        GenerateVehicleMap(Map);
      }

      return interiorMap;
    }
  }
  
  // AsAboveSoBelowで生成されるBandを無視したマップサイズ
  public IntVec3 MapSize{ get; private set; }

  public CellRect MapRect => [with(0, 0, MapSize.x, MapSize.z)];

  public Map CurrentLevel
  {
    get => field ??= interiorMap;
    set;
  }

  public VehicleMapBlitter VehicleMapBlitter => field ??= new VehicleMapBlitter(this);

  public Command_SelectVehicleMap VehicleMapGizmo => field ??= GetVehicleMapGizmo();

  [UsedImplicitly] public bool AllowEnter => allowEnter;

  [UsedImplicitly] public bool AllowExit => allowExit;

  [CanBeNull]
  public WorldObject VehicleCaravanOrStashedVehicle
  {
    get
    {
      var ticks = GenTicks.TicksGame;
      if (GenTicks.TicksGame == vehicleCaravanOrStashedVehicleCachedTick)
        return field;

      vehicleCaravanOrStashedVehicleCachedTick = ticks;
      field = ParentHolder as VehicleCaravan as WorldObject ??
              Find.World.GetComponent<VehicleWorldObjectsHolder>().StashedVehicleObject(this);
      return field;
    }
  }

  public BoolGrid ImpassableCellGrid
  {
    get
    {
      if (impassableCellsDirty)
      {
        impassableCellsDirty = false;
        field ??= new BoolGrid(interiorMap);
        field.Clear();
        var sizeX = MapSize.x;
        var sizeZ = MapSize.z;
        var terrainGrid = interiorMap.terrainGrid;
        for (var x = 0; x < sizeX; x++)
        {
          for (var z = 0; z < sizeZ; z++)
          {
            if (terrainGrid.TerrainAt(z * sizeX + x) is { passability: Traversability.Impassable })
              field[new IntVec3(x, 0, z)] = true;
          }
        }

        var list = interiorMap.listerThings.ThingsOfDef(VMF_DefOf.VMF_VehicleStructureFilled);
        for (var i = 0; i < list.Count; i++)
        {
          field[list[i].Position] = true;
        }
        AsAboveSoBelow.CopyBoolGrid(this, field);
      }

      return field;
    }
  }

  public BoolGrid EmptyStructureGrid
  {
    get
    {
      if (field is not null) return field;
      
      field = new BoolGrid(interiorMap);
      var props = VehicleMapProps;
      if (props is not null)
      {
        foreach (var c in props.EmptyStructureCells)
        {
          field[c.ToIntVec3] = true;
        }
        AsAboveSoBelow.CopyBoolGrid(this, field);
      }

      return field;
    }
  }

  public BoolGrid ExpandableGrid
  {
    get
    {
      if (field is not null) return field;
      
      field = new BoolGrid(interiorMap);
      var props = VehicleMapProps;
      if (props is not null)
      {
        foreach (var c in props.ExpandableCells)
        {
          field[c.ToIntVec3] = true;
        }
        AsAboveSoBelow.CopyBoolGrid(this, field);
      }

      return field;
    }
  }

  public BoolGrid OutOfBoundsGrid
  {
    get
    {
      if (field is not null) return field;
      
      field = new BoolGrid(interiorMap);
      var props = VehicleMapProps;
      if (props is not null)
      {
        foreach (var c in props.OutOfBoundsCells)
        {
          field[c.ToIntVec3] = true;
        }
        AsAboveSoBelow.CopyBoolGrid(this, field);
      }

      return field;
    }
  }

  public CellRect ValidMapRect
  {
    get
    {
      _ = CachedMapEdgeCells;
      return field;
    }
    private set;
  }

  public List<IntVec3> CachedMapEdgeCells
  {
    get
    {
      if (mapEdgeCellsDirty)
      {
        mapEdgeCellsDirty = false;
        walkableCellsDirty = true;
        enterPositionsDirty = true;
        field.Clear();
        var mapSize = MapSize;
        var cellRect = new CellRect(1, 1, mapSize.x - 1, mapSize.z - 1);
        var impassableGrid = ImpassableCellGrid;
        for (var i = 0; i < 4; i++)
        {
          var rot = new Rot4(i);
          var facingInside = rot.Opposite.FacingCell;
          // GetEdgeCellsはminからの列挙なので東と南は反転させ時計回りにしておく。
          foreach (var c in cellRect.EdgeRectClockwise(rot))
          {
            var c2 = c;
            while (cellRect.Contains(c2) && impassableGrid[c2])
            {
              c2 += facingInside;
            }

            if (cellRect.Contains(c2) && !impassableGrid[c2])
            {
              field.AddUnique(c2);
            }
          }
        }

        ValidMapRect = CellRect.FromCellList(field);
      }

      return field;
    }
  } = [];

  public Dictionary<IntVec3, District> CachedWalkableMapEdgeCells
  {
    get
    {
      if (walkableCellsDirty)
      {
        var mapEdgeCells = CachedMapEdgeCells;
        walkableCellsDirty = false;
        CrossMapReachabilityCache.ClearCacheFor(interiorMap);
        field.Clear();
        CachedEdgeDistricts.Clear();
        for (var i = 0; i < mapEdgeCells.Count; i++)
        {
          var cell = mapEdgeCells[i];
          if (cell.Walkable(interiorMap))
          {
            var district = RegionAndRoomQuery.DistirctAtFast(cell, interiorMap);
            field[cell] = district;
            CachedEdgeDistricts.Add(district);
          }
        }
      }

      return field;
    }
  } = [];

  public HashSet<District> CachedEdgeDistricts
  {
    get
    {
      _ = CachedWalkableMapEdgeCells;
      return field;
    }
  } = [];

  private List<IntVec3?> CachedEnterPositions
  {
    get
    {
      if (enterPositionsDirty)
      {
        var mapEdgeCells = CachedMapEdgeCells;
        enterPositionsDirty = false;
        CrossMapReachabilityCache.ClearCacheFor(interiorMap);
        field.Clear();
        for (var i = 0; i < mapEdgeCells.Count; i++)
        {
          field.Add(null);
        }
      }

      return field;
    }
  } = [];

  public IntVec3 GetCachedEnterPosition(int index)
  {
    var enterPositions = CachedEnterPositions;
    var pos = enterPositions[index];
    if (pos.HasValue) return pos.Value;

    var cell = CachedMapEdgeCells[index];
    pos = enterPositions[index] = CachedWalkableMapEdgeCells.ContainsKey(cell) &&
                                  CrossMapReachabilityUtility.EnterVehiclePosition(
                                      new TargetInfo(cell, interiorMap)) is
                                    { IsValid: true } c
      ? c
      : IntVec3.Invalid;
    return pos.Value;
  }

  private void WalkableCellsDirtyIfNeeded(Building building)
  {
    if (!building.def.AffectsReachability) return;
    foreach (var c in building.OccupiedRect())
    {
      if (CachedMapEdgeCells.Contains(c))
      {
        walkableCellsDirty = true;
        enterPositionsDirty = true;
        CrossMapReachabilityCache.ClearCacheFor(interiorMap);
        break;
      }
    }
  }

  private void WalkableCellsDirtyIfNeeded(IntVec3 c)
  {
    if (CachedMapEdgeCells.Contains(c))
    {
      walkableCellsDirty = true;
      enterPositionsDirty = true;
      CrossMapReachabilityCache.ClearCacheFor(interiorMap);
    }
  }

  private FetchedComp<CompNpcVehicleMap> _compNpcVehicleMap;
  public CompNpcVehicleMap CompNpcVehicleMap => (_compNpcVehicleMap ??= new FetchedComp<CompNpcVehicleMap>(this)).Value;

  private FetchedComp<CompDelayedKill> _compDelayedKill;
  public CompDelayedKill CompDelayedKill => (_compDelayedKill ??= new FetchedComp<CompDelayedKill>(this)).Value;

  private FetchedComp<CompVehicleDrawOffset> _compVehicleDrawOffset;

  public CompVehicleDrawOffset CompVehicleDrawOffset =>
    (_compVehicleDrawOffset ??= new FetchedComp<CompVehicleDrawOffset>(this)).Value;

  private FetchedComp<VehicleComp> _compVehicleHover;

  protected VehicleComp CompVehicleHover =>
    (_compVehicleHover ??= new FetchedComp<VehicleComp>(this, VehicleRaidFramework.CompVehicleHover)).Value;

  protected bool IsAirborne => VehicleRaidFramework.Active && CompVehicleHover is not null &&
                               VehicleRaidFramework.State(CompVehicleHover) > 0;

  public MapComponent InterceptorMapComponent =>
    field ??= interiorMap.GetComponent(DefenseGrid.InterceptorMapComponent);

  public List<CompVehicleEnterSpot> EnterComps { get; } = [];

  public List<CompFuelTank> FuelTankComps { get; } = [];

  public List<CompMapExpander> MapExpanderComps { get; } = [];

  public List<CompBuildableContainer> ContainerComps { get; } = [];

  public override Vector3 DrawPos => Spawned && Find.CurrentMap != CurrentLevel
    ? base.DrawPos
    : cachedDrawPos - (CompVehicleDrawOffset?.DrawOffsetFull(FullRotation) ?? Vector3.zero);

  public override Vector2 DrawSize => VehicleDef.IsUniqueVehicle ? VehicleDef.Size.ToVector2() : base.DrawSize;

  float IAttackTarget.TargetPriorityFactor => 0.15f;

  public override IEnumerable<Gizmo> GetGizmos()
  {
    foreach (var gizmo in base.GetGizmos()) yield return gizmo;
    if (Faction != Faction.OfPlayer && !DebugSettings.ShowDevGizmos)
      yield break;

    yield return new Command_Action
    {
      action = () =>
      {
        //リンクされたストレージの優先度が変わりすぎてしまうのを防ぎかつ全てのストレージにMoteを出したいので、一度優先度をキャッシュしておく
        var allGroups = interiorMap.haulDestinationManager.AllGroupsListForReading;
        var priorityList = allGroups.Select(g => g.Settings.Priority).ToList();
        for (var i = 0; i < allGroups.Count; i++)
        {
          allGroups[i].Settings.Priority =
            (StoragePriority)Math.Min((sbyte)(priorityList[i] + 1), (sbyte)StoragePriority.Critical);
          MoteMaker.ThrowText(allGroups[i].CellsList[0].ToVector3Shifted().ToBaseMapCoord(this), Map,
            allGroups[i].Settings.Priority.ToString(), Color.white);
        }
      },
      defaultLabel = "VMF_IncreasePriority".Translate(),
      defaultDesc = "VMF_IncreasePriorityDesc".Translate(),
      icon = iconIncreasePriority.Texture
    };

    yield return new Command_Action
    {
      action = () =>
      {
        var allGroups = interiorMap.haulDestinationManager.AllGroupsListForReading;
        var priorityList = allGroups.Select(g => g.Settings.Priority).ToList();
        for (var i = 0; i < allGroups.Count; i++)
        {
          allGroups[i].Settings.Priority =
            (StoragePriority)Math.Max((sbyte)(priorityList[i] - 1), (sbyte)StoragePriority.Low);
          MoteMaker.ThrowText(allGroups[i].CellsList[0].ToVector3Shifted().ToBaseMapCoord(this), Map,
            allGroups[i].Settings.Priority.ToString(), Color.white);
        }
      },
      defaultLabel = "VMF_DecreasePriority".Translate(),
      defaultDesc = "VMF_DecreasePriorityDesc".Translate(),
      icon = iconDecreasePriority.Texture
    };

    yield return new Command_Toggle
    {
      isActive = () => allowEnter,
      toggleAction = () => allowEnter = !allowEnter,
      defaultLabel = "VMF_AllowEnter".Translate(),
      defaultDesc = "VMF_AllowEnterDesc".Translate(),
      icon = iconAllowEnter.Texture
    };

    yield return new Command_Toggle
    {
      isActive = () => allowExit,
      toggleAction = () => allowExit = !allowExit,
      defaultLabel = "VMF_AllowsGetOff".Translate(),
      defaultDesc = "VMF_AllowsGetOffDesc".Translate(),
      icon = iconAllowExit.Texture
    };
    yield return VehicleMapGizmo;

    if (DebugSettings.ShowDevGizmos)
    {
      yield return new Command_FocusVehicleMap();
      yield return new Command_Action
      {
        defaultLabel = "Flash CachedMapEdgeCells",
        Order = 5001,
        action = () =>
        {
          foreach (var c in CachedMapEdgeCells) interiorMap.debugDrawer.FlashCell(c);
        }
      };
      yield return new Command_Action
      {
        defaultLabel = "Flash CachedWalkableMapEdgeCells",
        Order = 5002,
        action = () =>
        {
          foreach (var c in CachedWalkableMapEdgeCells.Keys) interiorMap.debugDrawer.FlashCell(c);
        }
      };
      yield return new Command_Action
      {
        defaultLabel = "Flash CachedEnterPositions",
        Order = 5003,
        action = () =>
        {
          for (var i = 0; i < CachedMapEdgeCells.Count; i++)
          {
            var pos = GetCachedEnterPosition(i);
            if (pos.IsValid) Map.debugDrawer.FlashCell(pos);
          }
        }
      };
      yield return new Command_Action
      {
        defaultLabel = "Flash ValidMapRect",
        Order = 5004,
        action = () =>
        {
          foreach (var c in ValidMapRect) interiorMap.debugDrawer.FlashCell(c);
        }
      };
      if (this.VehicleDef.IsUniqueVehicle)
      {
        yield return new Command_Toggle
        {
          defaultLabel = "Debug draw: bridge cells",
          Order = 5005,
          isActive = () => CompMapExpander.debugDraw,
          toggleAction = () => CompMapExpander.debugDraw = !CompMapExpander.debugDraw
        };
      }
    }
  }

  private Command_SelectVehicleMap GetVehicleMapGizmo()
  {
    return new Command_SelectVehicleMap(this)
    {
      portrait = new VehiclePortrait(new VehiclePortrait.Config
      {
        expiryTime = 5f
      }),
      toggleAction = () =>
      {
        if (Find.CurrentMap == interiorMap && Spawned)
        {
          Current.Game.CurrentMap = Map;
          return;
        }

        Patch_Game_CurrentMap.ForceSet = true;
        Current.Game.CurrentMap = interiorMap;
      },
      isActive = () => Find.CurrentMap != interiorMap || !Spawned,
      defaultLabel = interiorMap.Parent.LabelCap,
      miniIcon = iconEye.Texture,
      miniIconSize = 28f
    };
  }

  public List<CompVehicleEnterSpot> GetSortedEnterComps(IntVec3 cell,
    CompVehicleEnterSpot.Kind kind = CompVehicleEnterSpot.Kind.All)
  {
    tmpEnterComps.Clear();
    if (EnterComps.Empty()) return tmpEnterComps;
    for (var i = 0; i < EnterComps.Count; i++)
    {
      var comp = EnterComps[i];
      switch (kind)
      {
        case CompVehicleEnterSpot.Kind.RampOnly when !comp.Props.allowPassingVehicle:
        case CompVehicleEnterSpot.Kind.GroundAccessOnly when !comp.Props.canAccessToGround:
        case CompVehicleEnterSpot.Kind.DirectAccessOnly when !comp.Props.canAccessVehicleToVehicle:
          continue;
        case CompVehicleEnterSpot.Kind.All:
        default:
          break;
      }

      if (comp.parent.Position.Walkable(interiorMap)) tmpEnterComps.Add(comp);
    }

    tmpEnterComps.SortBy(c => c.parent.Position.DistanceToSquared(cell));
    return tmpEnterComps;
  }

  private void GenerateVehicleMap(Map sourceMap)
  {
    if (Destroyed)
    {
      VMF_Log.Error("Tried to generate vehicle map for destroyed vehicle.");
      return;
    }

    generatingVehicleMap = true;
    if (MapGenerator.mapBeingGenerated is not null)
    {
      if (!generatingVehicleMap)
        LongEventHandler.ExecuteWhenFinished(() => GenerateVehicleMap(sourceMap));
      return;
    }

    try
    {
      if (VehicleMapProps is { } props)
      {
        var mapParent = (MapParent_Vehicle)WorldObjectMaker.MakeWorldObject(VMF_DefOf.VMF_VehicleMap);
        mapParent.mapGenerator = VMF_DefOf.VMF_VehicleMapGenerator;
        mapParent.vehicle = this;
        mapParent.Tile = 0;
        mapParent.SetFaction(Faction);
        var mapSize = new IntVec3(props.size.x, 1, props.size.z);
        mapSize.x += 2;
        mapSize.z += 2;
        MapSize = mapSize;
        mapParent.sourceMap = sourceMap;
        interiorMap = MapGenerator.GenerateMap(mapSize, mapParent, mapParent.MapGeneratorDef,
          mapParent.ExtraGenStepDefs, isPocketMap: true);
        if (VehicleMapFramework.settings.drawPlanet)
        {
          var size = Patch_Map_MapUpdate.MeshSize;
          interiorMap.rememberedCameraPos?.rootPos = new Vector3(size.x / 2f, 0f, size.y / 2f);
        }

        Find.World.pocketMaps.Add(mapParent);
        SpawnStructures(IntVec3.Zero);
      }

      interiorMap.events.BuildingSpawned += WalkableCellsDirtyIfNeeded;
      interiorMap.events.PathCostRecalculate += WalkableCellsDirtyIfNeeded;
      CurrentLevel ??= interiorMap;
      if (!Find.World.worldObjects.Contains(interiorMap.Parent))
      {
        Find.World.worldObjects.Add(interiorMap.Parent);
      }

      if (sourceMap is not null)
      {
        interiorMap.skyManager = sourceMap.skyManager;
        interiorMap.weatherDecider = sourceMap.weatherDecider;
        interiorMap.weatherManager = sourceMap.weatherManager;
      }
    }
    catch (Exception ex)
    {
      VMF_Log.Error($"Error while generating vehicle map.\n{ex}");
    }
    finally
    {
      generatingVehicleMap = false;
    }
  }

  internal void SpawnStructures(IntVec3 origin)
  {
    if (VehicleMapProps is not { } props) return;
    
    foreach (var c in props.FilledStructureCells)
    {
      GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureFilled, origin + c.ToIntVec3, interiorMap).SetFaction(Faction);
    }

    foreach (var c in props.EmptyStructureCells)
    {
      interiorMap.terrainGrid.SetTerrain(origin + c.ToIntVec3, VMF_DefOf.VMF_ImpassableFloor);
    }

    foreach (var c in props.ExpandableCells)
    {
      interiorMap.terrainGrid.SetTerrain(origin + c.ToIntVec3, VMF_DefOf.VMF_ImpassableFloor);
    }

    foreach (var c in props.OutOfBoundsCells)
    {
      interiorMap.terrainGrid.SetTerrain(origin + c.ToIntVec3, VMF_DefOf.VMF_ImpassableFloor);
    }
  }

  internal void RemoveVehicleMap()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      if (Find.Maps.Contains(interiorMap))
      {
        var pocketMapParent = interiorMap.PocketMapParent;
        if (pocketMapParent != null)
        {
          pocketMapParent.sourceMap = null;
          Find.World.pocketMaps.Remove(pocketMapParent);
          Find.World.renderer.wantedMode = WorldRenderMode.None;
        }

        Current.Game.DeinitAndRemoveMap(interiorMap, false);
      }

      interiorMap = null;

      if (!VehicleMapFramework.settings.dynamicUnpatchEnabled) return;
      if (Find.Maps.Any(m => m.IsVehicleMap)) return;
      VMF_Harmony.DynamicPatchAll(VehicleMapFramework.settings.dynamicPatchLevel);
    });
  }

  public override void SpawnSetup(Map map, bool respawningAfterLoad)
  {
    if (interiorMap is null)
    {
      GenerateVehicleMap(map);
    }
    else if (respawningAfterLoad)
    {
      interiorMap.events.BuildingSpawned += WalkableCellsDirtyIfNeeded;
      interiorMap.events.PathCostRecalculate += WalkableCellsDirtyIfNeeded;
      _ = CachedMapEdgeCells;

      if (VehicleMapProps is VehicleMapProps_Unique { baseDef: not null })
      {
        FrameDelay.DelayOne<object>(_ => LongEventHandler.ExecuteWhenFinished(() => this.ResizeNow(false)), null);
      }
    }

    if (interiorMap is not null)
    {
      interiorMap.PocketMapParent?.sourceMap = map;
      if (!Find.World.worldObjects.Contains(interiorMap.Parent))
      {
        Find.World.worldObjects.Add(interiorMap.Parent);
      }

      if (VehicleMapProps is VehicleMapProps_Gravship)
      {
        if (GravshipUtility.GetPlayerGravEngine_NewTemp(interiorMap) is { launchInfo.doNegativeOutcome: true } engine)
        {
          var list = handlers.OfType<VehicleRoleHandlerBuildable>()
            .SelectMany<VehicleRoleHandlerBuildable, Pawn>(h => h.thingOwner).ToList();
          foreach (var t in list)
          {
            DisembarkPawn(t);
          }

          var gravship = GravshipUtility.GenerateGravship(engine);
          GravshipVehicleUtility.PlaceGravship(null, gravship, gravship.originalPosition, interiorMap);
          DefDatabase<LandingOutcomeDef>.AllDefsListForReading.RandomElementByWeight(d => d.weight).Worker
            .ApplyOutcome(gravship);
          engine.launchInfo = null;
        }
      }
    }

    base.SpawnSetup(map, respawningAfterLoad);
    RegisterEvents();
    RecacheDrawPos(DrawPos);
    VehiclePawnWithMapCache.RegisterVehicle(this);
    mapFollower = new VehicleMapFollower(this);

    if (interiorMap is not null)
    {
      interiorMap.skyManager = map.skyManager;
      interiorMap.weatherDecider = map.weatherDecider;
      interiorMap.weatherManager = map.weatherManager;
      if (Find.CurrentMap == interiorMap)
      {
        Current.Game.CurrentMap = map;
      }

      interiorMap.mapPawns.AllPawns.OfType<VehiclePawn>().Do(v => { v.Transform.rotation = 0f; });
    }

    SetTile();
    Transform.rotation = 0f;
    enterPositionsDirty = true;
  }

  protected override void Tick()
  {
    Resize();
    if (Spawned)
    {
      RecacheDrawPos(DrawPos);
      if (CompDelayedKill is { KillStarted: true })
      {
        CompDelayedKill.CompTick();
        return;
      }

      mapFollower?.MapFollowerTick();
    }
    else if (this.IsHashIntervalTick(30))
    {
      SetTile();
    }

    base.Tick();
  }

  protected override void TickInterval(int delta)
  {
    if (Spawned && CompDelayedKill is { KillStarted: true })
      return;
    base.TickInterval(delta);
  }

  private void SetTile()
  {
    if (Spawned)
    {
      interiorMap?.Parent.Tile = Map.Tile;
      return;
    }

    var worldObject2 = GetWorldObject(this);
    switch (worldObject2)
    {
      case AerialVehicleInFlight aerial:
        Task.Run(() => { interiorMap?.Parent.Tile = WorldHelper.GetNearestTile(aerial.DrawPos); });
        return;
      case null or MapParent_Vehicle:
        return;
      default:
        interiorMap?.Parent.Tile = worldObject2.Tile;
        return;
    }

    static WorldObject GetWorldObject(IThingHolder holder)
    {
      while (holder != null)
      {
        if (holder is WorldObject worldObject)
        {
          return worldObject;
        }

        holder = holder.ParentHolder;
      }

      return null;
    }
  }

  public override void Notify_MyMapRemoved()
  {
    base.Notify_MyMapRemoved();
    Destroy();
  }

  public override void Notify_AbandonedAtTile(PlanetTile tile)
  {
    base.Notify_AbandonedAtTile(tile);
    Destroy();
  }

  public override void Notify_LeftBehind()
  {
    base.Notify_LeftBehind();
    Destroy();
  }

  public override void Kill(DamageInfo? dinfo, DestroyMode destroyMode = DestroyMode.KillFinalize,
    bool spawnWreckage = false)
  {
    if (Spawned && CompDelayedKill is { KillOnTick: false })
    {
      if (!CompDelayedKill.KillStarted)
      {
        if (dinfo?.Instigator is Pawn instigator)
        {
          RecordsUtility.Notify_PawnKilled(this, instigator);
        }

        CompDelayedKill.StartKillTimer(destroyMode, spawnWreckage);
      }

      return;
    }

    base.Kill(dinfo, destroyMode, spawnWreckage);
  }

  public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
  {
    var map = Map;
    if (Spawned)
    {
      DisembarkAll();
    }

    if (interiorMap is null)
    {
      base.Destroy(mode);
      return;
    }

    if (map is not null)
    {
      StringBuilder stringBuilder = new();
      var flag = false;
      var allThings = interiorMap.listerThings.AllThings;
      for (var i = allThings.Count - 1; i >= 0; i--)
      {
        if (i >= allThings.Count) continue;
        var thing = allThings[i];
        if (mode != DestroyMode.Vanish && thing is { Destroyed: false })
        {
          var positionOnBaseMap = thing.PositionOnBaseMap;
          if (thing.def.category == ThingCategory.Building)
          {
            if (!thing.def.destroyable)
            {
              allowDestroyNonDestroyable = true;
              thing.Destroy();
              allowDestroyNonDestroyable = false;
            }
            else thing.Destroy();

            if (positionOnBaseMap.Walkable(map) &&
                positionOnBaseMap.GetItemCount(map) < positionOnBaseMap.GetMaxItemsAllowedInCell(map))
            {
              var pos = thing.Position;
              thing.Position = positionOnBaseMap;
              GenLeaving.DoLeavingsFor(thing, map, DestroyMode.Deconstruct);
              thing.Position = pos;
            }
          }
          else if (thing is not (Explosion or Projectile or Fire))
          {
            var cell = thing.DrawPos.ToIntVec3();
            if (!cell.InBounds(map))
            {
              cell = cell.ClampInsideMap(map);
            }

            thing.DeSpawn();
            var terrain = cell.GetTerrain(map);
            if (terrain.IsWater && thing is Filth)
            {
              thing.Destroy();
              continue;
            }

            if (thing is Pawn pawn &&
                (terrain == TerrainDefOf.WaterDeep || terrain == TerrainDefOf.WaterOceanDeep) &&
                HealthHelper.AttemptToDrown(pawn))
            {
              flag = true;
              stringBuilder.AppendLine(pawn.LabelCap);
              continue;
            }

            FrameDelay.DelayOne(static state =>
            {
              if (!GenPlace.TryPlaceThing(state.thing, state.cell, state.map, ThingPlaceMode.Near))
              {
                CellFinder.TryFindRandomCellNear(state.cell, state.map, 50,
                  c => GenPlace.TryPlaceThing(state.thing, c, state.map, ThingPlaceMode.Near), out _);
              }

              if (state.thing is Pawn { carryTracker.CarriedThing: not null } pawn)
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);

              if (state.thing is VehiclePawn vehicle &&
                  vehicle.Position.GetTerrain(state.map) is { IsWater: true } &&
                  !vehicle.DrivableRectOnCell(vehicle.Position))
              {
                vehicle.DisembarkAll();
                vehicle.Destroy();
              }
            }, (thing, cell, map));
          }
        }
      }

      if (flag)
      {
        string text = "VF_BoatSunkWithPawnsDesc".Translate(LabelShort, stringBuilder.ToString());
        Find.LetterStack.ReceiveLetter("VF_BoatSunk".Translate(), text, LetterDefOf.NegativeEvent,
          new TargetInfo(Position, map));
      }
    }

    base.Destroy(mode);
    RemoveVehicleMap();
    if (VehicleDef.HasModExtension<VehicleMapProps_Unique>())
      UniqueVehicleUtility.ReleaseUniqueVehicleDef(VehicleDef);
  }

  public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
  {
    // sourceMapをinteriorMap自身にすると無限ループの危険がある
    interiorMap.PocketMapParent.sourceMap = null;
    VehiclePawnWithMapCache.DeRegisterVehicle(this);
    mapFollower.DeRegisterVehicle();
    if (mode < DestroyMode.KillFinalize)
    {
      interiorMap.skyManager = new SkyManager(interiorMap);
      interiorMap.skyManager.ForceSetCurSkyGlow(Map.skyManager.CurSkyGlow);
      interiorMap.weatherManager = new WeatherManager(interiorMap)
      {
        curWeather = Map.weatherManager.curWeather,
        lastWeather = Map.weatherManager.lastWeather,
        prevSkyTargetLerp = Map.weatherManager.prevSkyTargetLerp,
        currSkyTargetLerp = Map.weatherManager.currSkyTargetLerp,
        curWeatherAge = Map.weatherManager.curWeatherAge
      };
      interiorMap.weatherDecider = new WeatherDecider(interiorMap);

      foreach (var pawn in interiorMap.mapPawns.AllPawns)
      {
        if (pawn.TryGetLord(out var pawnLord) && pawnLord.Map != interiorMap)
          pawnLord.Notify_PawnLost(pawn, PawnLostCondition.ExitedMap);
      }
    }

    foreach (var thing in interiorMap.listerThings.AllThings.Intersect(Find.Selector.SelectedObjects))
    {
      Find.Selector.Deselect(thing);
    }

    foreach (var zone in interiorMap.zoneManager.AllZones.Intersect(Find.Selector.SelectedObjects))
    {
      Find.Selector.Deselect(zone);
    }

    var crossMapHaulDestinationManager = Map.GetCachedMapComponent<CrossMapHaulDestinationManager>();
    foreach (var haulDestination in interiorMap.haulDestinationManager.AllHaulDestinations)
    {
      crossMapHaulDestinationManager.RemoveHaulDestination(haulDestination);
    }

    CrossMapReachabilityCache.ClearCacheFor(interiorMap);
    base.DeSpawn(mode);
  }

  public override void DrawAt(in Vector3 drawLoc, Rot8 rot, float rotation)
  {
    if (!Spawned)
    {
      interiorMap?.GetDetachedMapComponent<VehiclePositionManager>().AllClaimants.DoIf(
        v => v.def.graphicData?.drawRotated ?? false, v => { v.Transform.rotation = rotation.FlipAngle(v); });
      if (!Mathf.Approximately(Transform.rotation, rotation))
      {
        Transform.rotation = rotation;
        CellDesignationsDirty();
      }

      interiorMap?.rememberedCameraPos.rootPos = drawLoc;
    }

    var drawLoc2 = drawLoc.WithYOffset(-Altitudes.AltInc * 100f) +
                   (CompVehicleDrawOffset?.DrawOffsetFull(FullRotation) ?? Vector3.zero);
    RecacheDrawPos(drawLoc2);
    DrawTracker.DynamicDrawPhaseAt(DrawPhase.Draw, in drawLoc2, rot, Transform.rotation.FlipAngle(this));

    DrawVehicleMap();
  }

  public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
  {
    var drawLoc2 = drawLoc + (CompVehicleDrawOffset?.DrawOffsetFull(FullRotation) ?? Vector3.zero);
    base.DynamicDrawPhaseAt(phase, drawLoc2, flip);
    if (phase == DrawPhase.Draw)
    {
      RecacheDrawPos(drawLoc2);
      if (vehiclePather?.Moving ?? false)
      {
        CellDesignationsDirty();
      }

      DrawVehicleMap();
    }
  }

  public void RecacheDrawPos(Vector3 drawLoc)
  {
    if (!UnityData.IsInMainThread) return;

    var transform = new TransformData(drawLoc + Transform.position, FullRotation, Transform.rotation.FlipAngle(this));
    var result = VehicleGraphic?.ParallelGetPreRenderResults(ref transform, false, this);
    cachedDrawPos = result?.position ?? drawLoc;
    if (Spawned && Find.CurrentMap == CurrentLevel)
    {
      cachedExactPos = cachedDrawPos + base.DrawPos - drawLoc;
    }
    else
    {
      cachedExactPos = cachedDrawPos;
    }
  }

  private void CellDesignationsDirty()
  {
    if (cellDesignationsDirtyTick == GenTicks.TicksGame) return;
    cellDesignationsDirtyTick = GenTicks.TicksGame;
    foreach (var designationDef in cellDesignations)
    {
      DirtyCellDesignationsCache(CurrentLevel.designationManager, SingleParam.Get(designationDef));
    }
  }

  protected virtual void DrawVehicleMap()
  {
    var map = CurrentLevel;
    if (map is null) return;
    //PlantFallColors.SetFallShaderGlobals(map);
    //map.waterInfo.SetTextures();
    //map.avoidGrid.DebugDrawOnMap();
    //BreachingGridDebug.DebugDrawAllOnMap(map);
    FrameDelay.DelayOne(static vehicle =>
    {
      try
      {
        var map = vehicle.CurrentLevel;
        VehicleSectionLayerManager.CacheMode = true;
        map.GetCachedMapComponent<VehicleSectionLayerManager>()?.UpdateAllSection();
        map.mapDrawer.MapMeshDrawerUpdate_First();
      }
      finally
      {
        VehicleSectionLayerManager.CacheMode = false;
      }
    }, this);
    //map.powerNetGrid.DrawDebugPowerNetGrid();
    //DoorsDebugDrawer.DrawDebug();
    //map.mapDrawer.DrawMapMesh();
    var origin = AsAboveSoBelow.Active
      ? -AsAboveSoBelow.RectOfBand(map, AsAboveSoBelow.CurrentBand(map)).Min.ToVector3()
      : Vector3.zero;
    var drawPos = origin.ToBaseMapCoord(this);
    DrawVehicleMapMesh(drawPos, map);
    DynamicDrawManagerOnVehicle.DrawDynamicThings(map);
    DrawClippers();
    map.designationManager.DrawDesignations();
    map.overlayDrawer.DrawAllOverlays();
    map.temporaryThingDrawer.Draw();
    map.flecks.FleckManagerDraw();

    using (new Command_FocusVehicleMap.FocusVehicle(this))
    {
      map.roofGrid.RoofGridUpdate();
      map.mapTemperature.TemperatureUpdate();
      MapComponentUtility.MapComponentOnDraw(map);
      CompMapExpander.DebugDraw(MapExpanderComps);
    }

    DebugDrawHelper.DebugDraw(map.debugDrawer, map);
    //map.gameConditionManager.GameConditionManagerDraw(map);
    //MapEdgeClipDrawer.DrawClippers(__instance);
  }

  internal void DrawVehicleMapMesh(Vector3 drawPos, Map map)
  {
    var mapDrawer = map.mapDrawer;
    var component = map.GetCachedMapComponent<VehicleSectionLayerManager>();
    if (component is null) return;
    var dirty = false;
    var scope = AsAboveSoBelow.Active ? AsAboveSoBelow.RectOfBand(map, AsAboveSoBelow.CurrentBand(map)) : default;
    foreach (var section in sections(mapDrawer))
    {
      if (!dirty && (section.dirtyFlags & (MapMeshFlagDefOf.Things | MapMeshFlagDefOf.Terrain)) > 0UL)
      {
        VehicleMapUIRenderer.SetDirty(this);
        VehicleMapGizmo.portrait.MarkDirty();
        dirty = true;
      }

      if (AsAboveSoBelow.Active && !scope.Overlaps(section.CellRect))
        continue;
      
      DrawSection(section, drawPos, component);
    }
  }

  protected virtual void DrawSection(Section section, Vector3 drawPos, VehicleSectionLayerManager component)
  {
    var rot = FullRotation;
    ((SectionLayer_TerrainOnVehicle)component.GetLayer(section, typeof(SectionLayer_TerrainOnVehicle), default))
      .DrawLayer(drawPos);
    ((SectionLayer_SnowOnVehicle)component.GetLayer(section, typeof(SectionLayer_SnowOnVehicle), default))
      .DrawLayer(drawPos.WithYOffset(0.1f));
    var angle = this.FullAngle;
    VehicleSectionLayerManager.DrawLayer(component.GetLayer(section, typeof(SectionLayer_ThingsGeneral), rot), drawPos, angle);
    VehicleSectionLayerManager.DrawLayer(component, section, typeof(SectionLayer_BuildingsDamage), drawPos, rot, angle);
    VehicleSectionLayerManager.DrawLayer(component, section, typeof(SectionLayer_IndoorMask), drawPos.Yto0(), rot, angle);
    VehicleSectionLayerManager.DrawLayer(component, section, typeof(SectionLayer_EdgeShadows), drawPos, rot, angle);
    ((SectionLayer_SunShadowsOnVehicle)component.GetLayer(section, typeof(SectionLayer_SunShadowsOnVehicle), rot))
      .DrawLayer(drawPos, Transform.rotation - Angle);
    ((SectionLayer_LightingOnVehicle)component.GetLayer(section, typeof(SectionLayer_LightingOnVehicle), default))
      .DrawLayer(drawPos);
    
    if (OverlayDrawHandler.ShouldDrawPowerGrid)
    {
      VehicleSectionLayerManager.DrawLayer(component.GetLayer(section, typeof(SectionLayer_ThingsPowerGrid), rot), drawPos.Yto0(), angle);
    }

    if (OverlayDrawHandler.ShouldDrawZones)
    {
      VehicleSectionLayerManager.DrawLayer(component, section, t_SectionLayer_Zones, drawPos, rot, angle);
    }
    
    if (ModsConfig.OdysseyActive)
    {
      ((SectionLayer_SubstructurePropsOnVehicle)component.GetLayer(section,
        typeof(SectionLayer_SubstructurePropsOnVehicle), default))?.DrawLayer(rot, drawPos, Transform.rotation);
      ((SectionLayer_GravshipHullOnVehicle)component.GetLayer(section, typeof(SectionLayer_GravshipHullOnVehicle),
        default))?.DrawLayer(rot, drawPos, Transform.rotation);
    }

    DrawModLayers(section, drawPos, component);
  }

  protected virtual void DrawModLayers(Section section, Vector3 drawPos, VehicleSectionLayerManager component)
  {
    var angle = this.FullAngle;
    var rot = FullRotation;
    foreach (var compat in VehicleSectionLayerManager.CompatClassesForDrawLayers)
    {
      compat.DrawSectionLayers(component, section, drawPos, rot, angle);
    }
  }

  private void DrawClippers()
  {
    if (Command_FocusVehicleMap.FocusLockedVehicle == this || Command_FocusVehicleMap.FocusedVehicle == this)
    {
      var material = ClipMat;
      var quat = this.FullAngleQuat;
      var size = MapSize;
      Vector3 s = new(500f, 1f, size.z);
      Matrix4x4 matrix = default;
      matrix.SetTRS(ToBaseMapCoord(new Vector3(-250f, 0f, size.z / 2f), this), quat, s);
      Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
      matrix = default;
      matrix.SetTRS(ToBaseMapCoord(new Vector3(size.x + 250f, 0f, size.z / 2f), this), quat, s);
      Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
      s = new Vector3(1000f, 1f, 500f);
      matrix = default;
      matrix.SetTRS(ToBaseMapCoord(new Vector3(size.x / 2f, 0f, size.z + 250f), this), quat, s);
      Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
      matrix = default;
      matrix.SetTRS(ToBaseMapCoord(new Vector3(size.x / 2f, 0f, -250f), this), quat, s);
      Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);

      s = Vector3.one;
      var expander = Find.DesignatorManager.SelectedDesignator is Designator_Build { PlacingDef: ThingDef thingDef } &&
                     thingDef.HasComp<CompMapExpander>();
      var impassableGrid = ImpassableCellGrid;
      foreach (var c in MapRect)
      {
        if (impassableGrid[c] && (!expander || !ExpandableGrid[c]))
        {
          matrix.SetTRS(ToBaseMapCoord(c.ToVector3Shifted(), this), quat, s);
          Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }
      }
    }

    var currentMap = Find.CurrentMap;
    if ((currentMap == CurrentLevel || currentMap == interiorMap) && WorldRendererUtility.DrawingMap &&
        VehicleMapFramework.settings.drawPlanet)
    {
      var material = MapEdgeClipDrawer.ClipMat;
      var size = Patch_Map_MapUpdate.MeshSize;
      Vector3 s = new(500f, 1f, size.y);
      Matrix4x4 matrix = default;
      matrix.SetTRS(new Vector3(-250f, 0f, size.y / 2f), Quaternion.identity, s);
      Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
      matrix = default;
      matrix.SetTRS(new Vector3(size.x + 250f, 0f, size.y / 2f), Quaternion.identity, s);
      Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
      s = new Vector3(1000f, 1f, 500f);
      matrix = default;
      matrix.SetTRS(new Vector3(size.x / 2f, 0f, size.y + 250f), Quaternion.identity, s);
      Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
      matrix = default;
      matrix.SetTRS(new Vector3(size.x / 2f, 0f, -250f), Quaternion.identity, s);
      Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
    }
    return;
    
    Vector3 ToBaseMapCoord(Vector3 original, VehiclePawnWithMap vehicle)
    {
      var vehiclePos = vehicle.cachedDrawPos;
      var mapSize = vehicle.MapSize;
      var pivot = new Vector3(mapSize.x / 2f, 0, mapSize.z / 2f);
      var drawPos = (original.YOffset() - pivot).RotatedBy(vehicle.FullAngle) + vehiclePos;
      drawPos += VehicleMapUtility.OffsetFor(vehicle);
      return drawPos;
    }
  }

  public override void DrawGUIOverlay()
  {
    base.DrawGUIOverlay();
    var map = CurrentLevel;
    DebugDrawHelper.DebugOnGUI(map.debugDrawer, map);
  }

  public override string GetInspectString()
  {
    if (VehicleMapFramework.settings.weightFactor == 0f) return null;

    var str = base.GetInspectString();
    var stat = GetStatValue(VMF_DefOf.MaximumPayload);

    str += $"\n{VMF_DefOf.MaximumPayload.LabelCap}:" +
           $" {(VehicleMapUtility.VehicleMapMass(this) * VehicleMapFramework.settings.weightFactor).ToStringEnsureThreshold(2, 0)} /" +
           $" {stat.ToStringEnsureThreshold(2, 0)} {"kg".Translate()}";
    return str;
  }

  public virtual bool AllowEnterFor(Pawn pawn)
  {
    return (AllowEnter || (pawn?.HostileTo(Faction.OfPlayer) ?? true) || pawn.Drafted) && !IsAirborne;
  }

  public virtual bool AllowExitFor(Pawn pawn)
  {
    return (AllowExit || (pawn?.HostileTo(Faction.OfPlayer) ?? true) || pawn.Drafted) && !IsAirborne;
  }

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_References.Look(ref interiorMap, nameof(interiorMap));
    Scribe_Values.Look(ref allowEnter, nameof(allowEnter));
    Scribe_Values.Look(ref allowExit, nameof(allowExit));
    
    if (VehicleMapProps is { } props)
    {
      var size = new IntVec3(props.size.x + 2, 1, props.size.z + 2);
      MapSize = size;
    }
    else
    {
      MapSize = interiorMap.Size;
    }
  }

  protected override void PostLoad()
  {
    VMF_Harmony.DynamicPatchAll(Level.All);
    base.PostLoad();
    RegisterEvents();
    CompVehicleTurrets?.RevalidateTurrets();
    ResetRenderStatus();
    if (VehicleDef.IsUniqueVehicle)
      this.ResizeNow();
  }

  public override void PostMake()
  {
    base.PostMake();
    if (VehicleMapProps is VehicleMapProps_Unique { baseDef: null })
    {
      def = UniqueVehicleUtility.ClaimUniqueVehicleDef(VehicleDef);
    }
  }

  public override void PostGenerationSetup()
  {
    VMF_Harmony.DynamicPatchAll(Level.All);
    base.PostGenerationSetup();
    RegisterEvents();
  }

  public new void RegisterEvents()
  {
    var manager = MapVehicleEventManager;
    if (manager is not null && manager.Initialized()) return;
    this.FillEventsDef<MapVehicleEventDef>();
    this.AddEvent(VMF_DefOf.EnterNextCell, () =>
    {
      enterPositionsDirty = true;
      CrossMapReachabilityCache.ClearCacheFor(VehicleMap);
    });
    if (DefenseGrid.Active)
    {
      this.AddEvent(VMF_DefOf.EnterNextCell, () =>
      {
        if (InterceptorMapComponent is null) return;
        foreach (var grid in DefenseGrid.grids(InterceptorMapComponent))
        {
          DefenseGrid.RepaintGrid(InterceptorMapComponent, SingleParam.Get(grid));
        }
      });
      this.AddEvent(VehicleEventDefOf.Spawned, () => FrameDelay.DelayOne(static component =>
      {
        if (component is null) return;
        foreach (var grid in DefenseGrid.grids(component))
        {
          DefenseGrid.RepaintGrid(component, SingleParam.Get(grid));
        }
      }, InterceptorMapComponent));
      this.AddEvent(VehicleEventDefOf.Despawned, () =>
      {
        if (InterceptorMapComponent is null) return;
        foreach (var grid in DefenseGrid.grids(InterceptorMapComponent))
        {
          DefenseGrid.UnpaintGrid(InterceptorMapComponent, SingleParam.Get(grid));
        }
      });
    }
  }

  public void Resize()
  {
    if (resizeRequest)
    {
      resizeRequest = false;
      this.ResizeNow();
    }
  }

  [DebugAction(VehicleMapFramework.CategoryName, "Set Transform.Rotation",
    actionType = DebugActionType.ToolMapForPawns)]
  private static void SetTransformRotation(Pawn pawn)
  {
    if (pawn is not VehiclePawn vehicle)
    {
      Messages.Message("The selected pawn is not a vehicle.", MessageTypeDefOf.RejectInput, false);
      return;
    }

    DebugTools.curTool = new DebugTool($"{pawn}: to...", () =>
    {
      var angle = Ext_Math.RotateAngle((UI.MouseMapPosition() - vehicle.DrawPos).ToAngleFlat(), 90f) -
                  vehicle.FullRotation.AsAngle;
      vehicle.Transform.rotation = angle;
      Messages.Message($"Set {pawn}'s Transform.rotation to {angle:F1}", MessageTypeDefOf.NeutralEvent, false);
    });
  }

  [DebugAction(VehicleMapFramework.CategoryName, "Reset Transform.Rotation",
    actionType = DebugActionType.ToolMapForPawns)]
  private static void ResetTransformRotation(Pawn pawn)
  {
    if (pawn is not VehiclePawn vehicle)
    {
      Messages.Message("The selected pawn is not a vehicle.", MessageTypeDefOf.RejectInput, false);
      return;
    }

    vehicle.Transform.rotation = 0f;
  }
}