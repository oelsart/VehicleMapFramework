using System.Text;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using SmashTools;
using UnityEngine;
using UnityStubs.Rendering;
using Vehicles;
using Verse;
using Waylith.Rendering;
using FilterMode = UnityEngine.FilterMode;
using Graphics = UnityEngine.Graphics;
using Object = UnityEngine.Object;
using RenderTexture = UnityEngine.RenderTexture;
using TexSize = (int width, int height);
using Texture = UnityEngine.Texture;
using Texture2D = UnityEngine.Texture2D;

namespace VehicleMapFramework;

public class VehicleMapUIRenderer(Game game) : GameComponent
{
  public enum DurationType
  {
    Time,
    Ticks
  }

  private const int VehicleMapLayer = 28;

  /// <summary>
  /// Provides the current time. Can be overridden in tests to simulate time passage.
  /// </summary>
  public static Func<float> TimeProvider = () => RealTime.LastRealTime;

  private readonly Dictionary<CacheKey, CachedMapTexture> cachedTextures = [];
  private readonly Game game = game;

  private readonly List<RenderTexture> renderTexturesPool = [];
  private readonly List<CacheKey> toRemove = [];
  private readonly List<CacheKey> toSetDirty = [];

  private Camera camera;
  private List<MeshDrawCommand> commandList = [];

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
    camera.cullingMask = 1 << VehicleMapLayer;
    camera.clearFlags = CameraClearFlags.Color;
    camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
    camera.useOcclusionCulling = false;
    camera.renderingPath = RenderingPath.Forward;
    camera.transform.position = new Vector3(0f, 5f, 0f);
    camera.nearClipPlane = 0f;
    camera.farClipPlane = 5.5f;
  }

  public static Texture GetVehicleMapTexture(VehiclePawnWithMap vehicle, Rot4 rot, TexSize texSize,
    Vector2? drawSize = null, Vector3? drawOffset = null)
  {
    var component = Current.Game?.GetComponent<VehicleMapUIRenderer>();
    if (component?.camera is null)
      return BaseContent.BadTex;
    var camera = component.camera;
    var key = new CacheKey(texSize, vehicle, rot);
    var cache = component.GetOrCreateCachedMapTexture(key);
    if (!cache.Dirty)
    {
      component.cachedTextures[key] = new CachedMapTexture(cache.RenderTexture, false, TimeProvider());
      return cache.RenderTexture;
    }

    var mapSize = vehicle.MapSize.ToVector2();
    var mapOrigin = new Vector3(-mapSize.x / 2f, 0f, -mapSize.y / 2f).RotatedBy(rot);
    var proportions = drawSize ?? mapSize;
    if (rot.IsHorizontal) proportions = proportions.Rotated();
    if (drawOffset.HasValue) mapOrigin += drawOffset.Value;
    var texAspect = (float)texSize.width / texSize.height;
    var desiredWorldWidth = proportions.x;
    var desiredWorldHeight = proportions.y;
    var orthoSizeByHeight = desiredWorldHeight / 2f;
    var orthoSizeByWidth = desiredWorldWidth / (2f * texAspect);
    var orthoSize = Mathf.Max(orthoSizeByHeight, orthoSizeByWidth);
    camera.orthographicSize = orthoSize;
    camera.aspect = texAspect;
    
    component.RenderVehicleMap(vehicle.VehicleMap, mapOrigin, rot, cache.RenderTexture);
    component.cachedTextures[key] = new CachedMapTexture(cache.RenderTexture, false, TimeProvider());
    return cache.RenderTexture;
  }

  public static Texture GetOverlayWithVehicleMapTexture(VehiclePawnWithMap vehicle, GraphicOverlay overlay, Rot4 rot,
    TexSize texSize, CellRect mapLimit)
  {
    var component = Current.Game?.GetComponent<VehicleMapUIRenderer>();
    if (component?.camera is null)
      return BaseContent.BadTex;

    var key = new CacheKey(texSize, vehicle, rot, overlay);
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
    camera.aspect = (float)texSize.width / texSize.height;

    var overlayPos = VehicleMapUtility.OffsetFor(vehicle, rot) +
                     vehicle.VehicleGraphic.DrawOffset(rot) +
                     overlay.Graphic.DrawOffset(rot);
    var mapOrigin = new Vector3(-vehicle.MapSize.x / 2f, 0f, -vehicle.MapSize.z / 2f).RotatedBy(rot) +
                    overlayPos;

    var offset = drawSizeRotated.ToVector3() / 2f;
    var scale = texSize.height / drawSizeRotated.y;
    var bl = (mapOrigin + mapLimit.Min.ToVector3().RotatedBy(rot) + offset) * scale;
    var tr = (mapOrigin + mapLimit.MaxExpandedBy(1).Max.ToVector3().RotatedBy(rot) + offset) * scale;
    var minX = Mathf.Min(bl.x, tr.x);
    var minZ = Mathf.Min(bl.z, tr.z);
    var maxX = Mathf.Max(bl.x, tr.x);
    var maxZ = Mathf.Max(bl.z, tr.z);
    var clipRect = ClampPixelRect(Rect.MinMaxRect(minX, minZ, maxX, maxZ), texSize.width, texSize.height);

    if (clipRect is { width: > 0f, height: > 0f })
    {
      var mapRenderTexture = RenderTexture.GetTemporary(texSize.width, texSize.height, 24, cache.RenderTexture.format);
      mapRenderTexture.name = "VehicleMapTextureOverlayClipSource";
      mapRenderTexture.useMipMap = false;
      mapRenderTexture.filterMode = cache.RenderTexture.filterMode;
      try
      {
        component.RenderVehicleMap(vehicle.VehicleMap, mapOrigin, rot, mapRenderTexture);
        using (MeshDrawSubmission.BeginCapture(component.commandList))
        {
          var graphic = overlay.Graphic;
          Graphics.DrawMesh(graphic.MeshAt(rot), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one),
            graphic.MatAt(rot, vehicle), VehicleMapLayer);
          Graphics.DrawTexture(clipRect, mapRenderTexture,
            ToNormalizedSourceRect(clipRect, texSize.width, texSize.height), 0, 0, 0, 0);
        }
        var cameraView = CameraViewFactory.GetView(camera);
        CameraTextureRenderer.Current?.Render(
          cache.RenderTexture, component.commandList, cameraView, new Rect(0f, 0f, 1f, 1f), Color.clear);
        component.commandList.Clear();
      }
      finally
      {
        RenderTexture.ReleaseTemporary(mapRenderTexture);
      }
    }
    component.cachedTextures[key] = new CachedMapTexture(cache.RenderTexture, false, GenTicks.TicksGame);
    return cache.RenderTexture;
  }

  private static Rect ClampPixelRect(Rect rect, int width, int height)
  {
    var xMin = Mathf.Clamp(rect.xMin, 0f, width);
    var yMin = Mathf.Clamp(rect.yMin, 0f, height);
    var xMax = Mathf.Clamp(rect.xMax, 0f, width);
    var yMax = Mathf.Clamp(rect.yMax, 0f, height);

    if (xMax <= xMin || yMax <= yMin)
      return Rect.zero;

    return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
  }

  private static Rect ToNormalizedSourceRect(Rect pixelRect, int width, int height)
  {
    return new Rect(
      pixelRect.xMin / width,
      pixelRect.yMin / height,
      pixelRect.width / width,
      pixelRect.height / height);
  }

  private void RenderVehicleMap(Map map, Vector3 drawPos, Rot4 rot, RenderTexture targetTexture)
  {
    using (MeshDrawSubmission.BeginCapture(commandList))
    {
      var component = map.GetCachedMapComponent<VehicleSectionLayerManager>();
      foreach (var section in VehiclePawnWithMap.sections(map.mapDrawer))
      {
        DrawSection(section, drawPos, rot, component);
      }
    }

    var cameraView = CameraViewFactory.GetView(camera);
    CameraTextureRenderer.Current?.Render(
      targetTexture, commandList, cameraView, new Rect(0f, 0f, 1f, 1f), Color.clear);
    commandList.Clear();
  }

  private static void DrawSection(Section section, Vector3 drawPos, Rot4 rot, VehicleSectionLayerManager component)
  {
    var rotation = Quaternion.AngleAxis(rot.AsAngle, Vector3.up);
    DrawLayerNow(section.GetLayer(typeof(SectionLayer_TerrainOnVehicle)));
    DrawLayerNow(component.GetLayer(section, typeof(SectionLayer_ThingsGeneral), rot));
    return;

    void DrawLayerNow(SectionLayer layer)
    {
      if (layer is null) return;
      
      for (var i = 0; i < layer.subMeshes.Count; i++)
      {
        var subMesh = layer.subMeshes[i];
        if (subMesh.finalized && !subMesh.disabled)
        {
          Graphics.DrawMesh(subMesh.mesh, drawPos, rotation, subMesh.material, VehicleMapLayer);
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
      Object.Destroy(camera.gameObject);
    }

    camera = null;

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

  private RenderTexture GetRenderTexture(TexSize size)
  {
    for (var i = renderTexturesPool.Count - 1; i >= 0; i--)
    {
      var rt = renderTexturesPool[i];
      if (rt.width == size.width && rt.height == size.height)
      {
        renderTexturesPool.RemoveAt(i);
        return rt;
      }
    }
    return new RenderTexture(size.width, size.height, 24)
    {
      name = "VehicleMapTexture", useMipMap = false, filterMode = FilterMode.Trilinear
    };
  }

  private readonly record struct CacheKey(TexSize size, VehiclePawnWithMap vehicle, Rot4 rot, [UsedImplicitly] GraphicOverlay overlay = null);

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

  [DebugOutput(VehicleMapFramework.CategoryName, true, name = "Write vehicle map textures")]
  private static void WriteRenderTexturesToFile()
  {
    var component = Current.Game.GetComponent<VehicleMapUIRenderer>();
    var active = RenderTexture.active;
    var fileNameBuilder = new StringBuilder();
    foreach (var (key, cachedMapTexture) in component.cachedTextures)
    {
      var renderTexture = cachedMapTexture.RenderTexture;
      var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);

      RenderTexture.active = renderTexture;
      texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
      texture.Apply();

      var bytes = texture.EncodeToPNG();
      fileNameBuilder.Append(key.vehicle.ThingID);
      if (key.overlay is not null) fileNameBuilder.Append($"_{key.overlay.Name}");
      fileNameBuilder.Append($"_{key.rot.ToStringHuman()}.png");
      var fullPath = Path.Combine(Application.persistentDataPath, fileNameBuilder.ToString());
      File.WriteAllBytes(fullPath, bytes);
      Messages.Message($"Saved render texture to {fullPath}", MessageTypeDefOf.NeutralEvent, false);
      fileNameBuilder.Clear();
      Object.Destroy(texture);
    }
    RenderTexture.active = active;
  }
}
