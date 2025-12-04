using System.Collections.Generic;
using System.Linq;
using SmashTools;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace VehicleMapFramework;

public class VehicleMapUIRenderer(Game game) : GameComponent
{
    private readonly Game game = game;
    
    const int VEHICLE_MAP_LAYER = 28;
    
    private Camera camera;
    
    private CommandBuffer commandBuffer;
    
    private readonly Dictionary<(VehiclePawnWithMap vehicle, Vector2Int size), CachedMapTexture> cachedTextures = [];

    private readonly List<RenderTexture> renderTexturesPool = [];

    private readonly List<(VehiclePawnWithMap, Vector2Int)> toRemove = [];

    private readonly List<(VehiclePawnWithMap, Vector2Int)> toSetDirty = [];

    public override void FinalizeInit()
    {
        CreateCamera();
        GameEvent.OnGameDisposing += Clear;
    }

    public override void GameComponentUpdate()
    {
        foreach (var cache in cachedTextures
                     .Where(cache => cache.Value.Expired))
        {
            toRemove.Add(cache.Key);
            renderTexturesPool.Add(cache.Value.RenderTexture);
        }
        foreach (var key in toRemove)
            cachedTextures.Remove(key);
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
        camera.farClipPlane = 5f;
        commandBuffer = new CommandBuffer { name = "VehicleMapDrawBuffer" };
        camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
    }
    
    public static Texture GetVehicleMapTexture(VehiclePawnWithMap vehicle, Rot4 rot, Vector2Int texSize,
        Vector2? drawSize = null, Vector3? drawOffset = null)
    {
        var component = Current.Game.GetComponent<VehicleMapUIRenderer>();
        if (component?.camera is null || component.commandBuffer is null)
            return BaseContent.BadTex;
        var camera = component.camera;
        var key = (vehicle, texSize);
        var cache = component.GetOrCreateCachedMapTexture(key);
        if (!cache.Dirty) return cache.RenderTexture;
        
        var mapSize = vehicle.VehicleMap.Size.ToVector2();
        var mapOrigin = new Vector3(-mapSize.x / 2f, 0f, -mapSize.y / 2f).RotatedBy(rot);
        var proportions = drawSize ?? mapSize;
        var offset = drawOffset ?? Vector3.zero;
        var maxSize = Mathf.Max(proportions.x, proportions.y);
        
        camera.enabled = true;
        camera.orthographicSize = maxSize / 2f;
        camera.targetTexture = cache.RenderTexture;
        component.commandBuffer.Clear();
        component.RenderVehicleMap(vehicle.VehicleMap, mapOrigin + offset, rot);
        camera.Render();
        camera.targetTexture = null;
        camera.enabled = false;
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
            for (var i = 0; i < layer.subMeshes.Count; i++)
            {
                var subMesh = layer.subMeshes[i];
                if (subMesh.finalized && !subMesh.disabled)
                {
                    commandBuffer.DrawMesh(subMesh.mesh, Matrix4x4.TRS(drawPos, rotation, Vector3.one),
                        subMesh.material);
                }
            }
        }
    }

    public void Clear()
    {
        foreach (var cache in cachedTextures.Values
                     .Where(cache => cache.RenderTexture != null))
        {
            cache.RenderTexture.Release();
            Object.Destroy(cache.RenderTexture);
        }
        cachedTextures.Clear();

        foreach (var renderTexture in renderTexturesPool)
        {
            renderTexture.Release();
            Object.Destroy(renderTexture);
        }
        renderTexturesPool.Clear();
        toRemove.Clear();
        toSetDirty.Clear();
        
        camera.RemoveAllCommandBuffers();
        commandBuffer.Release();
        Object.Destroy(camera.gameObject);
        camera = null;
        commandBuffer = null;
    }

	public static void SetDirty(VehiclePawnWithMap vehicle)
    {
        var component = Current.Game.GetComponent<VehicleMapUIRenderer>();
        var cachedTextures = component.cachedTextures;
        foreach (var key in cachedTextures.Keys.Where(key => key.vehicle == vehicle))
        {
            component.toSetDirty.Add(key);
        }
        foreach (var key in component.toSetDirty)
            cachedTextures[key] = new CachedMapTexture(cachedTextures[key].RenderTexture, true, Time.time);
        component.toSetDirty.Clear();
    }

	private CachedMapTexture GetOrCreateCachedMapTexture((VehiclePawnWithMap vehicle, Vector2Int size) key)
	{
		if (!cachedTextures.TryGetValue(key, out var value))
		{
            cachedTextures[key] = value = new CachedMapTexture(GetRenderTexture(key.size), true, Time.time);
        }
		return value;
	}

	private RenderTexture GetRenderTexture(Vector2Int size)
	{
		var num = renderTexturesPool.FindLastIndex(x => x.width == size.x && x.height == size.y);
		if (num != -1)
		{
			var result = renderTexturesPool[num];
			renderTexturesPool.RemoveAt(num);
			return result;
		}
		return new RenderTexture(size.x, size.y, 24)
		{
			name = "VehicleMapTexture",
			useMipMap = false,
			filterMode = FilterMode.Bilinear
		};
	}
    
    private readonly struct CachedMapTexture(RenderTexture renderTexture, bool dirty, float lastUseTime)
    {
        private const float CacheDuration = 1f;

        public RenderTexture RenderTexture { get; } = renderTexture;

        public bool Dirty { get; } = dirty;

        private float LastUseTime { get; } = lastUseTime;

        public bool Expired => Time.time - LastUseTime > CacheDuration;
    }
}