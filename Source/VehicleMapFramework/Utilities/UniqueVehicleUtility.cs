using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HarmonyLib;
using RimWorld;
using SmashTools;
using Vehicles;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public static class UniqueVehicleUtility
{
    private static readonly Action<Def, Type, HashSet<ushort>> GiveShortHash = (Action<Def, Type, HashSet<ushort>>)AccessTools.Method(typeof(ShortHashGiver), "GiveShortHash").CreateDelegate(typeof(Action<Def, Type, HashSet<ushort>>));
    private static readonly Dictionary<Type, HashSet<ushort>> takenHashesPerDeftype = AccessTools.StaticFieldRefAccess<Dictionary<Type, HashSet<ushort>>>(typeof(ShortHashGiver), "takenHashesPerDeftype");
    private static readonly AccessTools.FieldRef<int> nextIndex;
    
    private static readonly AccessTools.FieldRef<VehiclePathingSystem, VehiclePathingSystem.VehiclePathData[]> vehicleData;
    private static readonly AccessTools.FieldRef<WorldVehiclePathGrid, WorldVehiclePathGrid.PathGrid[]> pathGrids;
    private static readonly AccessTools.FieldRef<WorldVehicleReachability, WorldVehicleReachability.WorldRegionGrid[]> regionGrids;

    private static readonly AccessTools.FieldRef<MapGridOwners, int[]> piggyToOwnerMap;
    internal static readonly AccessTools.FieldRef<MapGridOwners, MapGridOwners.PathConfig[]> configsMap;
    internal static readonly Func<VehicleDef, MapGridOwners.PathConfig> PathConfigMap;
    
    private static readonly AccessTools.FieldRef<WorldGridOwners, int[]> piggyToOwnerWorld;
    private static readonly AccessTools.FieldRef<WorldGridOwners, WorldGridOwners.PathConfig[]> configsWorld;
    private static readonly Func<VehicleDef, WorldGridOwners.PathConfig> PathConfigWorld;
    internal static readonly FastInvokeHandler GeneratePathData;
    internal static readonly FastInvokeHandler PathData;

    static UniqueVehicleUtility()
    {
        var type = AccessTools
            .FirstInner(GenTypes.GetTypeInAnyAssembly("SmashTools.DefIndexManager"), t => t.Name.Contains("Indexer"))
            .MakeGenericType(typeof(VehicleDef));
        nextIndex = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(type, "nextIndex"));
        
        vehicleData
            = AccessTools.FieldRefAccess<VehiclePathingSystem, VehiclePathingSystem.VehiclePathData[]>("vehicleData");
        pathGrids
            = AccessTools.FieldRefAccess<WorldVehiclePathGrid, WorldVehiclePathGrid.PathGrid[]>("pathGrids");
        regionGrids
            = AccessTools.FieldRefAccess<WorldVehicleReachability, WorldVehicleReachability.WorldRegionGrid[]>("regionGrids");
        
        piggyToOwnerMap = AccessTools.FieldRefAccess<MapGridOwners, int[]>("piggyToOwner");
        configsMap = AccessTools.FieldRefAccess<MapGridOwners, MapGridOwners.PathConfig[]>("configs");
        var param = Expression.Parameter(typeof(VehicleDef), "vehicleDef");
        PathConfigMap = Expression.Lambda<Func<VehicleDef, MapGridOwners.PathConfig>>(
            Expression.New(AccessTools.Constructor(typeof(MapGridOwners.PathConfig), [typeof(VehicleDef)]), param), 
            param
        ).Compile();
        
        piggyToOwnerWorld = AccessTools.FieldRefAccess<WorldGridOwners, int[]>("piggyToOwner");
        configsWorld = AccessTools.FieldRefAccess<WorldGridOwners, WorldGridOwners.PathConfig[]>("configs");
        param = Expression.Parameter(typeof(VehicleDef), "vehicleDef");
        PathConfigWorld = Expression.Lambda<Func<VehicleDef, WorldGridOwners.PathConfig>>(
            Expression.New(AccessTools.Constructor(typeof(WorldGridOwners.PathConfig), [typeof(VehicleDef)]), param), 
            param
        ).Compile();
        GeneratePathData
            = MethodInvoker.GetHandler(AccessTools.Method(typeof(VehiclePathingSystem), "GeneratePathData"));
        var s_PathData = AccessTools.PropertySetter(typeof(VehiclePathFollower), "PathData");
        if (s_PathData is not null)
            PathData = MethodInvoker.GetHandler(s_PathData);

        if (AnyNull(nextIndex, vehicleData, pathGrids, regionGrids, piggyToOwnerMap, configsMap, piggyToOwnerWorld, configsWorld))
        {
            VMF_Log.Error("UniqueVehicleUtility: Failed to initialize");
        }
        return;

        static bool AnyNull(params object[] objects) => objects.Any(o => o is null);
    }
        
    public static VehicleDef GenerateUniqueVehicleDef(VehicleMapProps_Unique props)
    {
        VMF_Log.DebugMessage($"Generate VehicleDef: {props.defName}");
        var vehicleDef = GenerateInner(props);
        VehicleMod.GenerateImpliedDefs(vehicleDef, false);
        DefGenerator.AddImpliedDef(vehicleDef);
        DefDatabase<ThingDef>.Add(vehicleDef);
        vehicleDef.DefIndex = nextIndex();
        nextIndex()++;
        foreach (var map in Find.Maps)
        {
            var component = map.GetCachedMapComponent<VehiclePathingSystem>();
            if (component is null) continue;
            ResizeArray(ref vehicleData(component));
            ResizeArray(ref piggyToOwnerMap(component.GridOwners))[^1] = vehicleDef.DefIndex;
            ResizeArray(ref configsMap(component.GridOwners))[^1] = PathConfigMap(vehicleDef);
            
            GeneratePathData(component, vehicleDef);
        }
        
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            var worldComponent = Find.World.GetComponent<WorldVehiclePathGrid>();
            if (worldComponent is not null)
            {
                ResizeArray(ref pathGrids(worldComponent));
                ResizeArray(ref regionGrids(worldComponent.reachability));
                ResizeArray(ref piggyToOwnerWorld(GridOwners.World));
                ResizeArray(ref configsWorld(GridOwners.World))[^1] = PathConfigWorld(vehicleDef);
            }
        });
        return vehicleDef;

        static T[] ResizeArray<T>(ref T[] source)
        {
            var newArray = new T[source.Length + 1];
            source.CopyTo(newArray, 0);
            source = newArray;
            return newArray;
        }
    }

    public static VehicleDef GenerateUniqueVehicleDef(VehiclePawn vehicle)
    {
        var baseProps = vehicle.def.GetModExtension<VehicleMapProps_Unique>();
        if (baseProps is null)
        {
            return null;
        }
        var defName = $"{Find.World.info.name}_{vehicle.ThingID}_Unique";
        var def = DefDatabase<VehicleDef>.GetNamedSilentFail(defName);
        if (def is not null) return def;
        
        var props = new VehicleMapProps_Unique();
        foreach (var field in typeof(VehicleMapProps_Unique).GetFields())
        {
            if (!field.IsLiteral) field.SetValue(props, field.GetValue(baseProps));
        }
        props.defName = defName;
        props.baseDef = vehicle.VehicleDef;
        var newDef = GenerateUniqueVehicleDef(props);
        newDef.components?.ForEach(component =>
        {
            component.hitbox.Hitbox.Clear();
            component.hitbox.Initialize(newDef);
        });
        return newDef;
    }

    private static VehicleDef GenerateInner(VehicleMapProps_Unique props)
    {
        var def = Gen.MemberwiseClone(props.baseDef);
        def.defName = props.defName;
        def.graphicData = new GraphicDataRGB();
        def.graphicData.CopyFrom(props.baseDef.graphicData);
        if (props.baseDef.components is not null)
        {
            def.components = [];
            foreach (var component in props.baseDef.components)
            {
                var clone = Gen.MemberwiseClone(component);
                clone.hitbox = Gen.MemberwiseClone(component.hitbox);
                def.components.Add(clone);
            }
            def.components = props.baseDef.components.ToList();
        }
        def.modExtensions = [.. props.baseDef.modExtensions.Where(e => e is not VehicleMapProps_Unique).AddItem(props)];
        def.shortHash = 0;
        GiveShortHash(def, typeof(ThingDef), takenHashesPerDeftype[typeof(ThingDef)]);
        return def;
    }
}
