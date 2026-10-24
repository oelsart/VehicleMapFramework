using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine.Assertions;
using Vehicles;
using Vehicles.Tests;
using Verse;

namespace VehicleMapFramework.Test;

public static class MapVehicleDefGenerator
{
  private const string MockTexPath = "Things/Building/Furniture/Bed/SleepSpot";

  private static readonly FieldInfo FleshField;

  static MapVehicleDefGenerator()
  {
    FleshField = AccessTools.Field(typeof(RaceProperties), "fleshType");
  }

  public static void ClearStatWorkerCaches(VehicleDef vehicleDef)
  {
    foreach (var statDef in DefDatabase<VehicleStatDef>.AllDefsListForReading)
    {
      statDef.Worker.ClearCachedBaseValues(vehicleDef);
    }
  }
  
  public static VehicleDef CreateVehicleDef(VehicleGroup.MockSettings settings)
  {
    var vehicleDef = CreateTransientMapVehicleDef($"VehicleDef_MOCK_{Rand.Int}", settings);

    if (!settings.statModifiers.NullOrEmpty())
    {
      vehicleDef.vehicleStats = [.. settings.statModifiers];
    }
    else
    {
      // Default values to ensure vehicle is at least moveable if required
      vehicleDef.vehicleStats =
      [
        new VehicleStatModifier
        {
          statDef = VehicleStatDefOf.MoveSpeed,
          value = (settings.permissions & VehiclePermissions.Mobile) != 0 ? 10 : 0
        },
        new VehicleStatModifier
        {
          statDef = VehicleStatDefOf.CargoCapacity,
          value = 1
        }
      ];
    }
    ClearStatWorkerCaches(vehicleDef);
    vehicleDef.RecacheMovementPermissions();

    var totalSlots = settings.passengers + settings.prisoners + settings.slaves + settings.extraSlots;
    if (totalSlots > 0)
    {
      vehicleDef.properties.roles =
      [
        new VehicleRole
        {
          key = "Passenger",
          slots = totalSlots
        }
      ];
    }

    if ((settings.permissions & VehiclePermissions.Autonomous) == 0)
    {
      vehicleDef.properties.roles.Add(new VehicleRole
      {
        key = "Driver",
        slots = settings.drivers,
        slotsToOperate = settings.drivers,

        handlingTypes = HandlingType.Movement
      });
    }
    return vehicleDef;
  }

  public static VehicleDef CreateTransientMapVehicleDef(string defName,
    [CanBeNull] VehicleGroup.MockSettings settings)
  {
    Assert.IsNotNull(FleshField);

    VehicleBuildDef buildDef = new()
    {
      defName = $"{defName}_Blueprint",
      label = $"{settings?.debugLabel ?? defName} Blueprint",
      modContentPack = VehicleMod.content,
      thingClass = typeof(VehicleBuilding),
      terrainAffordanceNeeded = TerrainAffordanceDefOf.Heavy,
      clearBuildingArea = true,
      category = ThingCategory.Building,
      rotatable = true,
      blockWind = true,
      useHitPoints = true,
      size = settings?.size ?? IntVec2.One,
      graphicData = new GraphicDataRGB
      {
        graphicClass = typeof(Graphic_Single),
        texPath = MockTexPath,
      },
      building = new BuildingProperties
      {
        canPlaceOverImpassablePlant = false,
        paintable = false
      }
    };
    VehicleDef def = new()
    {
      defName = defName,
      label = settings?.debugLabel ?? $"{defName}_LABEL",
      modContentPack = VehicleMod.content,
      thingClass = typeof(VehiclePawnWithMap),
      category = ThingCategory.Pawn,
      tickerType = TickerType.Normal,
      selectable = true,
      useHitPoints = false,
      properties = settings?.properties ?? new VehicleProperties(),
      drawProperties = settings?.drawProperties ?? new VehicleDrawProperties(),
      size = buildDef.size,
      graphicData = new GraphicDataRGB
      {
        graphicClass = typeof(Graphic_Vehicle),
        texPath = MockTexPath,
      },

      race = new RaceProperties
      {
        body = DefDatabase<BodyDef>.GetNamed("emptyBody"),
        trainability = DefDatabase<TrainabilityDef>.GetNamed("None"),
        thinkTreeMain = DefDatabase<ThinkTreeDef>.GetNamed("Vehicle"),
        thinkTreeConstant = DefDatabase<ThinkTreeDef>.GetNamed("Vehicle_Constant"),
        intelligence = Intelligence.ToolUser,
        needsRest = false,
        hasGenders = false,
        foodType = FoodTypeFlags.None,
        alwaysAwake = true,
        doesntMove = true,

        baseBodySize = 1,

        lifeStageAges =
        [
          new LifeStageAge
          {
            def = DefDatabase<LifeStageDef>.GetNamed("MechanoidFullyFormed"),
            minAge = 0,
          }
        ]
      },
      modExtensions =
        [
          new VehicleMapProps
          {
            size = buildDef.size
          }
        ]
    };
    FleshField.SetValue(def.race, DefDatabase<FleshTypeDef>.GetNamed("MetalVehicle"));
    def.buildDef = buildDef;
    buildDef.thingToSpawn = def;

    // Transient defs should not be accessing any caches used for loaded defs. Doing so will yield
    // unexpected behavior so we should let it throw.
    //def.DefIndex = -1;

    if (settings != null)
    {
      def.type = settings.type;
      def.components = settings.components;
    }

    if (settings != null && !settings.comps.NullOrEmpty())
    {
      foreach (var compProps in settings.comps)
      {
        compProps.ResolveReferences(def);
        def.comps.Add(compProps);
      }
    }

    Assert.IsTrue(VehicleMod.GenerateImpliedDefs(def, false));
    def.PostLoad();
    def.ResolveReferences();
    def.PostDefDatabase();
    return def;
  }
}