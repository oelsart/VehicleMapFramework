using HarmonyLib;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using UnityStubs.Rendering;
using Vehicles.World;
using Verse;
using Waylith.Rendering;
using Graphics = UnityEngine.Graphics;
using Material = UnityEngine.Material;
using RenderTexture = UnityEngine.RenderTexture;

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
  private static Material bgMat;
  private static RenderTexture renderTexture;

  private static readonly List<MeshDrawCommand> commandList = [];

  private static int lastWorldRenderedTick;
  private static Vector3 lastWorldObjectDrawPos;

  public static bool Available { get; internal set; } = true;
  
  static VehicleMapView()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      mesh200 = MeshPool.GridPlane(MeshSize);
      skyMat = SolidColorMaterials.NewSolidColorMaterial(Color.black, ShaderDatabase.SolidColor);
      skyMat.renderQueue = 3100;
      renderTexture = new RenderTexture(2048, 2048, 0, RenderTextureFormat.ARGB32);
      bgMat = MaterialPool.MatFrom(ShaderDatabase.Cutout);
      bgMat.mainTexture = renderTexture;
    });
  }
  
  public static void Draw(VehiclePawnWithMap vehicle)
  {
    if (Find.World.renderer.RegenerateLayersIfDirtyInLongEvent())
      return;
    
    var forceRotation = VehicleMapFramework.Settings.forceRotated;
    var forceRotated = forceRotation != VehicleMapSettings.ForceRotated.None;
    var angle = forceRotated
      ? new Rot8((byte)forceRotation).AsAngle
      : vehicle.Transform.rotation + vehicle.Rotation.AsAngle;
    var vehicleWorldObject = vehicle.ParentVehicleWorldObject;
    
    var worldObject = vehicleWorldObject as WorldObject ?? GetWorldObject(vehicle);
    if (worldObject is null) return;
    
    var curLayer = worldObject.Tile.Layer;

    if (Mathf.Abs(lastWorldRenderedTick - GenTicks.TicksGame) > 9 ||
        (worldObject.DrawPos - lastWorldObjectDrawPos).sqrMagnitude > 0.1f)
    {
      lastWorldRenderedTick = GenTicks.TicksGame;
      lastWorldObjectDrawPos = worldObject.DrawPos;
      Find.World.renderer.wantedMode = WorldRenderMode.Planet;
      WorldRendererUtility.UpdateGlobalShadersParams();
      using (new WorldCameraScope(Find.Camera, worldObject))
      {
        using var capture = MeshDrawSubmission.BeginCapture(commandList);
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
      
        var cameraView = CameraViewFactory.GetView(Find.Camera);
        CameraTextureRenderer.Current?.Render(
          renderTexture, commandList, cameraView, new Rect(0f, 0f, 1f, 1f), Color.clear);
        commandList.Clear();
      }
      Find.World.renderer.wantedMode = WorldRenderMode.None;
    }
    
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
        if (vehicleWorldObject is not null)
        {
          foreach (var vehicle2 in vehicleWorldObject.Vehicles)
          {
            vehicle2.FullRotation = rot;
          }
        }
        else vehicle.FullRotation = rot;
      }
    }
    
    // 背景
    Graphics.DrawMesh(mesh200, Center, Quaternion.identity, bgMat, 0);
    
    // 空の暗さ
    skyMat.color = Color.black.WithAlpha((1f - vehicle.VehicleMap.skyManager.CurSkyGlow) * 0.2f);
    Graphics.DrawMesh(mesh200, Center.WithY(AltitudeLayer.LightingOverlay.AltitudeFor()), Quaternion.identity, skyMat, 0);

    //　車両本体
    var drawPos = Center.WithY(AltitudeLayer.LayingPawn.AltitudeFor());
    if (worldObject.GetComponent<VehicleFormationComp>() is { } comp)
    {
      var drawPositions = comp.DrawPositions;

      foreach (var vehicle2 in vehicleWorldObject?.Vehicles ?? [])
      {
        if (!drawPositions.ContainsKey(vehicle2))
        {
          comp.FindVehiclePosition(vehicle2);
          comp.CenteredDrawPositions();
        }
        var drawPos2 = drawPos + (drawPositions[vehicle2].position).RotatedBy(angle);
        vehicle2.DrawAt(in drawPos2, vehicle2.FullRotation, angle - vehicle2.FullRotation.AsAngle);
      }
    }
    else
    {
      vehicle.DrawAt(in drawPos, vehicle.FullRotation, angle - vehicle.FullRotation.AsAngle);
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
    private readonly float aspect;
    private readonly int cullingMask;
    private readonly float farClipPlane;
    private readonly float transition;
    private readonly float expandMoreTransition;

    public WorldCameraScope(Camera targetCamera, WorldObject lookTarget, bool mapCameraTranslate = false)
    {
      camera = targetCamera;
      var transform = camera.transform;

      position = transform.position;
      rotation = transform.rotation;
      aspect = camera.aspect;
      orthographic = camera.orthographic;
      orthographicSize = camera.orthographicSize;
      cullingMask = camera.cullingMask;
      farClipPlane = camera.farClipPlane;
      transition = transitionPct();
      expandMoreTransition = expandMoreTransitionPct();

      var mapCameraPosition = Find.Camera.transform.position;
      var scale = lookTarget.Tile.Layer.AverageTileSize / VehicleMapFramework.Settings.mapSizePerTile;
      var drawPos = lookTarget.DrawPos;
      transform.rotation = Quaternion.LookRotation(-drawPos);
      transform.position = drawPos + drawPos.normalized * 15f;
      if (mapCameraTranslate)
        camera.transform.Translate((mapCameraPosition - Center).ToVector2() * scale);
      camera.orthographic = true;
      camera.orthographicSize = (mapCameraTranslate ? Find.CameraDriver.RootSize : MeshSizeX) * scale;
      camera.aspect = 1f;
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
      camera.aspect = aspect;
      camera.cullingMask = cullingMask;
      camera.farClipPlane = farClipPlane;
      transitionPct() = transition;
      expandMoreTransitionPct() = expandMoreTransition;
    }
  }
}