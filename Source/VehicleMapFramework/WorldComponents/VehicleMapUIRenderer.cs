using System.Collections.Generic;
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
        if (!cache.Dirty)
        {
            component.cachedTextures[key] = new CachedMapTexture(cache.RenderTexture, false, Time.time);
            return cache.RenderTexture;
        }

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
        component.cachedTextures[key] = new CachedMapTexture(cache.RenderTexture, false, Time.time);
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
                    commandBuffer.DrawMesh(subMesh.mesh, Matrix4x4.TRS(drawPos, rotation, Vector3.one),
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

	public static void SetDirty(VehiclePawnWithMap vehicle)
    {
        var component = Current.Game?.GetComponent<VehicleMapUIRenderer>();
        if (component == null) return;

        var cachedTextures = component.cachedTextures;
        foreach (var key in cachedTextures.Keys)
        {
            if (key.vehicle == vehicle)
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