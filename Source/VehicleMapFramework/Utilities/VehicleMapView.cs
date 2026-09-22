using System;
using HarmonyLib;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public static class VehicleMapView
{
  private static readonly AccessTools.FieldRef<float> transitionPct
    = AccessTools.StaticFieldRefAccess<float>(AccessTools.Field(typeof(ExpandableWorldObjectsUtility), "transitionPct"));
  private static readonly AccessTools.FieldRef<float> expandMoreTransitionPct
    = AccessTools.StaticFieldRefAccess<float>(AccessTools.Field(typeof(ExpandableWorldObjectsUtility), "expandMoreTransitionPct"));

  public const float MeshSizeX = 200f;
  public static readonly Vector2 MeshSize = new(MeshSizeX, MeshSizeX);
  public static readonly Vector3 Center = new(MeshSizeX / 2f, 0f, MeshSizeX / 2f);
  private static Mesh mesh200;
  private static Material skyMat;

  public static bool Available { get; internal set; } = true;
  
  static VehicleMapView()
  {
    if (UnitTestDetector.IsTestingContext) return;
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      mesh200 = MeshPool.GridPlane(MeshSize);
      skyMat = SolidColorMaterials.NewSolidColorMaterial(Color.black, ShaderDatabase.SolidColor);
      skyMat.renderQueue = 3100;
    });
  }
  
  public static void Draw(VehiclePawnWithMap vehicle)
  {
    if (Find.World.renderer.RegenerateLayersIfDirtyInLongEvent())
      return;
    
    var forceRotation = VehicleMapFramework.settings.forceRotated;
    var forceRotated = forceRotation != VehicleMapSettings.ForceRotated.None;
    var angle = forceRotated
      ? new Rot8((byte)forceRotation).AsAngle
      : vehicle.Transform.rotation + vehicle.Rotation.AsAngle;
    var vehicleCaravanOrStashedVehicle = vehicle.VehicleCaravanOrStashedVehicle;
    
    var worldObject = vehicleCaravanOrStashedVehicle ?? GetWorldObject(vehicle);
    if (worldObject is null) return;
    
    Find.World.renderer.wantedMode = WorldRenderMode.Planet;
    WorldRendererUtility.UpdateGlobalShadersParams();
    var curLayer = worldObject.Tile.Layer;
    using (new WorldCameraScope(Find.Camera, worldObject))
    {
      var id = curLayer.LayerID;
      foreach (var (_, planetLayer) in Find.WorldGrid.PlanetLayers)
      {
        if (planetLayer.LayerID <= id && planetLayer.Visible)
        {
          foreach (var drawLayer in planetLayer.WorldDrawLayers)
          {
            if (drawLayer is
                WorldDrawLayer_SingleTile or WorldDrawLayer_Satellites or WorldDrawLayer_Clouds or
                WorldDrawLayer_Glow or WorldDrawLayer_UngeneratedPlanetParts)
              continue;

            drawLayer.Render();
          }
        }
      }

      Find.World.dynamicDrawManager.DrawDynamicWorldObjects();
      if (worldObject is VehicleCaravan vehicleCaravan)
      {
        vehicleCaravan.gotoMote.RenderMote();
        vehicleCaravan.vehiclePather?.curPath?.DrawPath(vehicleCaravan);
      }
      
      Find.Camera.Render();
    }
    Find.World.renderer.wantedMode = WorldRenderMode.None;

    if (!vehicle.Spawned)
    {
      if (forceRotated)
      {
        vehicle.FullRotation = new Rot8((byte)forceRotation);
      }
      else
      {
        angle =
          worldObject switch
          {
            VehicleCaravan vehicleCaravan2 => AngleOnPlanetSurface(
              Find.WorldGrid.GetTileCenter(vehicleCaravan2.vehiclePather.NextTile.Valid
                ? vehicleCaravan2.vehiclePather.NextTile
                : vehicleCaravan2.Tile), Find.WorldGrid.GetTileCenter(vehicleCaravan2.Tile)),
            Caravan caravan => AngleOnPlanetSurface(
              Find.WorldGrid.GetTileCenter(caravan.pather.nextTile.Valid ? caravan.pather.nextTile : caravan.Tile),
              Find.WorldGrid.GetTileCenter(caravan.Tile)),
            AerialVehicleInFlight aerial => AngleOnPlanetSurface(aerial.DrawPos, aerial.position),
            _ => 0f
          };
        var rot = Rot4.FromAngleFlat(angle);
        if (vehicleCaravanOrStashedVehicle is not null)
        {
          foreach (var vehicle2 in vehicleCaravanOrStashedVehicle.Vehicles)
          {
            vehicle2.FullRotation = rot;
          }
        }
        else vehicle.FullRotation = rot;
      }
    }
    
    // 空の暗さ
    skyMat.color = Color.black.WithAlpha((1f - vehicle.VehicleMap.skyManager.CurSkyGlow) * 0.2f);
    Graphics.DrawMesh(mesh200, Center.WithY(AltitudeLayer.LightingOverlay.AltitudeFor()), Quaternion.identity, skyMat, 0);

    //　車両本体
    if (vehicleCaravanOrStashedVehicle?.GetComponent<VehicleFormationComp>() is { } comp)
    {
      var drawPositions = comp.DrawPositions;

      foreach (var vehicle2 in vehicleCaravanOrStashedVehicle.Vehicles)
      {
        if (!drawPositions.ContainsKey(vehicle2))
        {
          comp.FindVehiclePosition(vehicle2);
          comp.CenteredDrawPositions();
        }
        var drawPos2 = Center + (drawPositions[vehicle2].position).RotatedBy(angle);
        vehicle2.DrawAt(in drawPos2, vehicle2.FullRotation, angle - vehicle2.FullRotation.AsAngle);
      }
    }
    else
    {
      vehicle.DrawAt(in Center, vehicle.FullRotation, angle - vehicle.FullRotation.AsAngle);
    }
    return;

    float AngleOnPlanetSurface(Vector3 root, Vector3 to)
    {
      if ((to - root).magnitude <= Mathf.Epsilon)
      {
        return 0f;
      }

      var normal = root - curLayer.Origin;
      var planeFrom = Vector3.ProjectOnPlane(curLayer.NorthPolePos, normal);
      var planeTo = Vector3.ProjectOnPlane(to, normal);
      var signedAngle = Vector3.SignedAngle(planeFrom, planeTo, normal);
      return Mathf.Repeat(signedAngle + 180f, 360f);
    }

    static WorldObject GetWorldObject(IThingHolder holder)
    {
      while (holder is not null)
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

  public readonly struct WorldCameraScope : IDisposable
  {
    private readonly Camera camera;
    private readonly Vector3 position;
    private readonly Quaternion rotation;
    private readonly bool orthographic;
    private readonly float orthographicSize;
    private readonly int cullingMask;
    private readonly float farClipPlane;
    private readonly float transition;
    private readonly float expandMoreTransition;

    public WorldCameraScope(Camera targetCamera, WorldObject lookTarget)
    {
      camera = targetCamera;
      var transform = camera.transform;

      position = transform.position;
      rotation = transform.rotation;
      orthographic = camera.orthographic;
      orthographicSize = camera.orthographicSize;
      cullingMask = camera.cullingMask;
      farClipPlane = camera.farClipPlane;
      transition = transitionPct();
      expandMoreTransition = expandMoreTransitionPct();

      var mapCameraPosition = Find.Camera.transform.position;
      var scale = lookTarget.Tile.Layer.AverageTileSize / VehicleMapFramework.settings.mapSizePerTile;
      var drawPos = lookTarget.DrawPos;
      transform.rotation = Quaternion.LookRotation(-drawPos);
      transform.position = drawPos + drawPos.normalized * 15f;
      camera.transform.Translate((mapCameraPosition - Center).ToVector2() * scale);
      camera.orthographic = true;
      camera.orthographicSize = Find.CameraDriver.RootSize * scale;
      camera.cullingMask = WorldCameraManager.WorldLayerMask;
      camera.farClipPlane = WorldCameraManager.FarClipPlane;
      transitionPct() = 0f;
      expandMoreTransitionPct() = 1f;
    }

    void IDisposable.Dispose()
    {
      var transform = camera.transform;
      transform.rotation = rotation;
      transform.position = position;
      camera.orthographic = orthographic;
      camera.orthographicSize = orthographicSize;
      camera.cullingMask = cullingMask;
      camera.farClipPlane = farClipPlane;
      transitionPct() = transition;
      expandMoreTransitionPct() = expandMoreTransition;
    }
  }
}