using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(CameraJumper), nameof(CameraJumper.GetWorldTarget))]
[PatchLevel(Level.Safe)]
public static class Patch_CameraJumper_GetWorldTarget
{
  public static void Prefix(ref GlobalTargetInfo target)
  {
    if (target.Thing.IsOnVehicleMapOf(out var vehicle))
    {
      target = vehicle;
    }
  }
}

[HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.MapParentAt))]
[PatchLevel(Level.Sensitive)]
public static class Patch_WorldObjectsHolder_MapParentAt
{
  public static void Postfix(ref MapParent __result, List<MapParent> ___mapParents, PlanetTile tile)
  {
    if (__result is MapParent_Vehicle)
    {
      __result = ___mapParents.FirstOrDefault(p => p.Tile == tile && p is not MapParent_Vehicle);
    }
  }
}

[HarmonyPatch(typeof(Game), nameof(Game.FindMap), typeof(PlanetTile))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Game_FindMap
{
  public static void Postfix(ref Map __result, List<Map> ___maps, PlanetTile tile)
  {
    if (__result.IsVehicleMap)
    {
      __result = ___maps.FirstOrDefault(m => m.Tile == tile && !m.IsVehicleMap);
    }
  }
}

// ワールドマップ上でスポーンしている車両マップがクリックされるのを防止する
[HarmonyPatch(typeof(WorldSelector), nameof(WorldSelector.SelectableObjectsUnderMouse),
  [typeof(bool), typeof(bool)], [ArgumentType.Out, ArgumentType.Out])]
[PatchLevel(Level.Safe)]
public static class Patch_WorldSelector_SelectableObjectsUnderMouse
{
  public static void Postfix(IEnumerable<WorldObject> __result)
  {
    if (__result is List<WorldObject> list)
    {
      list.RemoveAll(w => w is MapParent_Vehicle { vehicle.Spawned: true });
    }
  }
}

[HarmonyPatch(typeof(Selector), nameof(Selector.SelectorOnGUI_BeforeMainTabs))]
[PatchLevel(Level.Safe)]
public static class Patch_Selector_SelectorOnGUI_BeforeMainTabs
{
    private const float SphereRadius = 100f;
    private const float Magnification = Patch_Map_MapUpdate.MeshSizeX / 2f / Patch_Map_MapUpdate.TextureSize;
    
    public static void Postfix(Selector __instance)
    {
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
            Event.current.shift &&
            Find.CurrentMap.IsVehicleMapOf(out var vehicle) &&
            vehicle.ParentHolder is VehicleCaravan { IsPlayerControlled: true } caravan &&
            __instance.SelectedPawns.Empty() &&
            new Rect(Vector2.zero, Patch_Map_MapUpdate.MeshSize).Contains(UI.MouseMapPosition().ToVector2()))
        {
            var altitude = RootSizeToAltitude();
            Patch_Map_MapUpdate.JumpTo(caravan.DrawPos, altitude);
            Find.WorldCamera.transform.Translate(ScreenOffset());
            Find.WorldSelector.ClearSelection();
            Find.WorldSelector.Select(caravan, false);
            Find.WorldSelector.WorldSelectorOnGUI();
            Event.current.Use();
        }
    }

    private static Vector2 ScreenOffset()
    {
        var offset = Find.Camera.transform.position.ToVector2() - Patch_Map_MapUpdate.MeshSize / 2f;
        return offset * Magnification;
    }

    private static float RootSizeToAltitude()
    {
        var halfFovRad = Find.WorldCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        var distanceToSurface = Find.CameraDriver.RootSize * Magnification / Mathf.Tan(halfFovRad);
        return distanceToSurface + SphereRadius;
    }
}

[HarmonyPatch(typeof(GenWorld), nameof(GenWorld.TileAt))]
[PatchLevel(Level.Safe)]
public static class Patch_GenWorld_TileAt
{
    public static void Prefix()
    {
        if (WorldRendererUtility.DrawingMap && Find.CurrentMap is { IsVehicleMap: true })
        {
            Find.WorldCamera?.gameObject.SetActive(true);
        }
    }
}