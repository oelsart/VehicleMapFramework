using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SmashTools;
using UnityEngine;
using UnityEngine.Rendering;
using Vehicles;
using Verse;
using Object = UnityEngine.Object;

namespace VehicleMapFramework;

public class VehicleMapUIRenderer(Game game) : GameComponent
{

  public enum DurationType
  {
    Time,
    Ticks
  }

  private const int VEHICLE_MAP_LAYER = 28;

  /// <summary>
  /// Provides the current time. Can be overridden in tests to simulate time passage.
  /// </summary>
  public static Func<float> TimeProvider = () => Time.time;

  private readonly Dictionary<CacheKey, CachedMapTexture> cachedTextures = [];
  private readonly Game game = game;

  private readonly List<RenderTexture> renderTexturesPool = [];

  private readonly List<CacheKey> toRemove = [];

  private readonly List<CacheKey> toSetDirty = [];

  private Camera camera;

  private CommandBuffer commandBuffer;

  public override void FinalizeInit()
  {
    CreateCamera();
    GameEvent.OnGameDisposing -= Clear;
    GameEvent.OnGameDisposing += Clear;
  }

  public override void GameComponentUpdate()
  {
    if (cachedTextures.Count == 0) return;

    foreach (var cache in cachedTextures)
    {
      if (!cache.Value.Expired) continue;

      toRemove.Add(cache.Key);
      if (cache.Value.RenderTexture != null)
        renderTexturesPool.Add(cache.Value.RenderTexture);
    }
    foreach (var key in toRemove)
    {
      cachedTextures.Remove(key);
    }
    toRemove.Clear();
  }

  private void CreateCamera()
  {
    var gameObject = new GameObject("VehicleMapCamera", typeof(Camera));
    gameObject.SetActive(false);
    Object.DontDestroyOnLoad(gameObject);
    camera = gameObject.GetComponent<Camera>();
    camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    camera.orthographic = true;
    camera.cullingMask = 1 << VEHICLE_MAP_LAYER;
    camera.clearFlags = CameraClearFlags.Color;
    camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
    camera.useOcclusionCulling = false;
    camera.renderingPath = RenderingPath.Forward;
    camera.transform.position = new Vector3(0f, 5f, 0f);
    camera.nearClipPlane = 0f;
    camera.farClipPlane = 5.5f;
    commandBuffer = new CommandBuffer
    {
      name = "VehicleMapDrawBuffer"
    };
    camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
  }

  public static Texture GetVehicleMapTexture(VehiclePawnWithMap vehicle, Rot4 rot, Vector2Int texSize,
    Vector2? drawSize = null, Vector3? drawOffset = null)
  {
    var component = Current.Game?.GetComponent<VehicleMapUIRenderer>();
    if (component?.camera is null || component.commandBuffer is null)
      return BaseContent.BadTex;
    var camera = component.camera;
    var key = new CacheKey(texSize, vehicle);
    var cache = component.GetOrCreateCachedMapTexture(key);
    if (!cache.Dirty)
    {
      component.cachedTextures[key] = new CachedMapTexture(cache.RenderTexture, false, TimeProvider());
      return cache.RenderTexture;
    }

    var mapSize = vehicle.VehicleMap.Size.ToVector2();
    var mapOrigin = new Vector3(-mapSize.x / 2f, 0f, -mapSize.y / 2f).RotatedBy(rot);
    var proportions = drawSize ?? mapSize;
    var offset = drawOffset ?? Vector3.zero;
    var maxSize = Mathf.Max(proportions.x, proportions.y);

    camera.enabled = true;
    camera.orthographicSize = maxSize / 2f;
    camera.aspect = (float)texSize.x / texSize.y;
    camera.targetTexture = cache.RenderTexture;
    component.commandBuffer.Clear();
    component.RenderVehicleMap(vehicle.VehicleMap, mapOrigin + offset, rot);
    camera.Render();
    camera.targetTexture = null;
    camera.enabled = false;
    component.cachedTextures[key] = new CachedMapTexture(cache.RenderTexture, false, TimeProvider());
    return cache.RenderTexture;
  }

  public static Texture GetOverlayWithVehicleMapTexture(VehiclePawnWithMap vehicle, GraphicOverlay overlay, Rot4 rot,
    Vector2Int texSize, CellRect mapLimit)
  {
    var component = Current.Game?.GetComponent<VehicleMapUIRenderer>();
    if (component?.camera is null || component.commandBuffer is null)
      return BaseContent.BadTex;

    var key = new CacheKey(texSize, vehicle, overlay);
    var cache = component.GetOrCreateCachedMapTexture(key);
    if (!cache.Dirty)
    {
      component.cachedTextures[key] = new CachedMapTexture(cache.RenderTexture, false, GenTicks.TicksGame);
      return cache.RenderTexture;
    }

    var camera = component.camera;
    camera.enabled = true;

    var drawSize = overlay.Graphic.drawSize;
    var drawSizeRotated = rot.IsHorizontal ? drawSize.Rotated() : drawSize;
    camera.targetTexture = cache.RenderTexture;
    camera.orthographicSize = drawSizeRotated.y / 2f;
    camera.aspect = (float)texSize.x / texSize.y;

    component.commandBuffer.Clear();
    var graphic = overlay.Graphic;
    component.commandBuffer.DrawMesh(
      graphic.MeshAt(rot),
      Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one),
      graphic.MatAt(rot, vehicle));

    var overlayPos = VehicleMapUtility.OffsetFor(vehicle, rot) +
                     vehicle.VehicleGraphic.DrawOffset(rot) -
                     overlay.Graphic.DrawOffset(rot);
    var mapOrigin = new Vector3(-vehicle.VehicleMap.Size.x / 2f, 0f, -vehicle.VehicleMap.Size.z / 2f).RotatedBy(rot) +
                    overlayPos;

    var offset = drawSizeRotated.ToVector3() / 2f;
    var scale = texSize.y / drawSizeRotated.y;
    var bl = (mapOrigin + mapLimit.Min.ToVector3().RotatedBy(rot) + offset) * scale;
    var tr = (mapOrigin + mapLimit.MaxExpandedBy(1).Max.ToVector3().RotatedBy(rot) + offset) * scale;
    var minX = Mathf.Min(bl.x, tr.x);
    var minZ = Mathf.Min(bl.z, tr.z);
    var maxX = Mathf.Max(bl.x, tr.x);
    var maxZ = Mathf.Max(bl.z, tr.z);
    component.commandBuffer.EnableScissorRect(Rect.MinMaxRect(minX, minZ, maxX, maxZ));
    component.RenderVehicleMap(vehicle.VehicleMap, mapOrigin, rot);
    camera.Render();

    camera.targetTexture = null;
    camera.enabled = false;
    component.commandBuffer.DisableScissorRect();
    component.cachedTextures[key] = new CachedMapTexture(cache.RenderTexture, false, GenTicks.TicksGame);
    return cache.RenderTexture;
  }

  private void RenderVehicleMap(Map map, Vector3 drawPos, Rot4 rot)
  {
    var mapDrawer = map.mapDrawer;
    var component = map.GetCachedMapComponent<VehicleSectionLayerManager>();
    for (var i = 0; i < map.Size.x; i += 17)
    {
      for (var j = 0; j < map.Size.z; j += 17)
      {
        var section = mapDrawer.SectionAt(new IntVec3(i, 0, j));
        DrawSection(section, drawPos, rot, component);
      }
    }
  }

  private void DrawSection(Section section, Vector3 drawPos, Rot4 rot, VehicleSectionLayerManager component)
  {
    var rotation = Quaternion.AngleAxis(rot.AsAngle, Vector3.up);
    DrawLayerNow(section.GetLayer(typeof(SectionLayer_TerrainOnVehicle)));
    DrawLayerNow(component.GetLayer(section, typeof(SectionLayer_ThingsGeneral), rot));
    return;

    void DrawLayerNow(SectionLayer layer)
    {
      if (layer == null) return;

      for (var i = 0; i < layer.subMeshes.Count; i++)
      {
        var subMesh = layer.subMeshes[i];
        if (subMesh.finalized && !subMesh.disabled)
        {
          commandBuffer.DrawMesh(subMesh.mesh,
            Matrix4x4.TRS(drawPos, rotation, Vector3.one),
            subMesh.material);
        }
      }
    }
  }

  public void Clear()
  {
    foreach (var cache in cachedTextures.Values)
    {
      if (cache.RenderTexture is not null)
        Object.Destroy(cache.RenderTexture);
    }
    cachedTextures.Clear();

    foreach (var renderTexture in renderTexturesPool)
    {
      if (renderTexture is not null)
        Object.Destroy(renderTexture);
    }
    renderTexturesPool.Clear();
    toRemove.Clear();
    toSetDirty.Clear();

    if (camera != null)
    {
      camera.RemoveAllCommandBuffers();
      Object.Destroy(camera.gameObject);
    }

    commandBuffer?.Release();
    camera = null;
    commandBuffer = null;

    GameEvent.OnGameDisposing -= Clear;
  }

  public static void SetDirty(VehiclePawnWithMap vehicle, DurationType type = DurationType.Time)
  {
    var component = Current.Game?.GetComponent<VehicleMapUIRenderer>();
    if (component == null) return;

    var cachedTextures = component.cachedTextures;
    foreach (var pair in cachedTextures)
    {
      if (pair.Key.vehicle == vehicle && pair.Value.DurationType == type)
        component.toSetDirty.Add(pair.Key);
    }
    foreach (var key in component.toSetDirty)
    {
      cachedTextures[key] = type switch
      {
        DurationType.Time => new CachedMapTexture(cachedTextures[key].RenderTexture, true, TimeProvider()),
        DurationType.Ticks => new CachedMapTexture(cachedTextures[key].RenderTexture, true, GenTicks.TicksGame),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
      };
    }
    component.toSetDirty.Clear();
  }

  private CachedMapTexture GetOrCreateCachedMapTexture(CacheKey key)
  {
    if (!cachedTextures.TryGetValue(key, out var value))
    {
      cachedTextures[key] = value = new CachedMapTexture(GetRenderTexture(key.size), true, TimeProvider());
    }
    return value;
  }

  private RenderTexture GetRenderTexture(Vector2Int size)
  {
    for (var i = renderTexturesPool.Count - 1; i >= 0; i--)
    {
      var rt = renderTexturesPool[i];
      if (rt.width == size.x && rt.height == size.y)
      {
        renderTexturesPool.RemoveAt(i);
        return rt;
      }
    }
    return new RenderTexture(size.x, size.y, 24)
    {
      name = "VehicleMapTexture", useMipMap = false, filterMode = FilterMode.Bilinear
    };
  }

  private readonly record struct CacheKey(Vector2Int size, VehiclePawnWithMap vehicle, [UsedImplicitly] GraphicOverlay overlay = null);

  public readonly struct CachedMapTexture(RenderTexture renderTexture, bool dirty)
  {

    public CachedMapTexture(RenderTexture renderTexture, bool dirty, float lastUseTime) : this(renderTexture, dirty)
    {
      LastUseTime = lastUseTime;
      DurationType = DurationType.Time;
    }

    public CachedMapTexture(RenderTexture renderTexture, bool dirty, int lastUseTick) : this(renderTexture, dirty)
    {
      LastUseTick = lastUseTick;
      DurationType = DurationType.Ticks;
    }

    public const float CacheDurationTime = 1f;
    public const int CacheDurationTicks = 60;

    public RenderTexture RenderTexture { get; } = renderTexture;

    public bool Dirty { get; } = dirty;

    public DurationType DurationType { get; }

    public float LastUseTime { get; }

    public int LastUseTick { get; }

    public bool Expired => DurationType == DurationType.Ticks
      ? GenTicks.TicksGame - LastUseTick > CacheDurationTicks
      : TimeProvider() - LastUseTime > CacheDurationTime;
  }
}
