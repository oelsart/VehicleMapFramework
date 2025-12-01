using SmashTools;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace VehicleMapFramework;

[HotSwap]
public static class VehicleMapUIRenderer
{
    const int VEHICLE_MAP_LAYER = 31;
    
    private static Camera Camera { get; } = CreateCamera();
    
    private static CommandBuffer commandBuffer;

    private static Camera CreateCamera()
    {
        var gameObject = new GameObject("VehicleMapCamera", typeof(Camera));
        gameObject.SetActive(false);
        Object.DontDestroyOnLoad(gameObject);
        var component = gameObject.GetComponent<Camera>();
        component.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        component.orthographic = true;
        component.cullingMask = 1 << VEHICLE_MAP_LAYER;
        component.clearFlags = CameraClearFlags.Color;
        component.backgroundColor = new Color(0f, 0f, 0f, 0f);
        component.useOcclusionCulling = false;
        component.renderingPath = RenderingPath.Forward;
        component.transform.position = new Vector3(0f, 5f, 0f);
        component.nearClipPlane = 0f;
        component.farClipPlane = 5f;
        commandBuffer = new CommandBuffer { name = "VehicleMapDrawBuffer" };
        component.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
        return component;
    }
    
    public static Texture GetVehicleMapTexture(VehiclePawnWithMap vehicle, Rot4 rot, Vector2Int texSize,
        Vector2? drawSize = null, Vector3? drawOffset = null)
    {
        var mapSize = vehicle.VehicleMap.Size.ToVector2();
        var mapOrigin = new Vector3(-mapSize.x / 2f, 0f, -mapSize.y / 2f).RotatedBy(rot);
        var proportions = drawSize ?? mapSize;
        var offset = drawOffset ?? Vector3.zero;
        var maxSize = Mathf.Max(proportions.x, proportions.y);
        var renderTexture = RenderTexture.GetTemporary(texSize.x, texSize.y);
        var prevCamera = Camera.current;
        Camera.gameObject.SetActive(true);
        Camera.orthographicSize = maxSize / 2f;
        Camera.targetTexture = renderTexture;
        commandBuffer.Clear();
        RenderVehicleMap(vehicle.VehicleMap, mapOrigin + offset, rot);
        Camera.Render();
        Camera.targetTexture = null;
        Camera.gameObject.SetActive(false);
        prevCamera.gameObject.SetActive(true);
        
        Delay.AfterNSeconds(0f, () => RenderTexture.ReleaseTemporary(renderTexture));
        return renderTexture;
    }

    private static void RenderVehicleMap(Map map, Vector3 drawPos, Rot4 rot)
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

    private static void DrawSection(Section section, Vector3 drawPos, Rot4 rot, VehicleSectionLayerManager component)
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
}