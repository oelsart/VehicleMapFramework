using SmashTools;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework;

public abstract class SectionLayer_ThingsOnVehicle : SectionLayer_Things
{
    private bool dirty;

    private int dirtyFrame;

    private CellRect bounds;

    public List<LayerSubMesh>[] subMeshesByRot = new List<LayerSubMesh>[4];

    public SectionLayer_ThingsOnVehicle(Section section) : base(section)
    {
        for (var i = 0; i < 4; i++)
        {
            subMeshesByRot[i] = [];
        }
    }

    public override CellRect GetBoundaryRect()
    {
        return bounds;
    }

    //drawPlanetがオフでVehicleMapにフォーカスした時しか呼ばれないよ
    public override void DrawLayer()
    {
        DrawLayer(Rot8.North, Vector3.zero, 0f);
    }

    public void DrawLayer(Rot8 rot, Vector3 drawPos, float extraRotation)
    {
        if (!DebugViewSettings.drawThingsPrinted)
        {
            return;
        }
        //タイミングによってスポーン前にRegenerateされてる気がするので遅延してRegenerateさせるための仕組み
        if (dirty && Time.frameCount != dirtyFrame)
        {
            RegenerateActually();
            dirty = false;
        }
        var angle = Ext_Math.RotateAngle(rot.AsAngle, extraRotation);
        switch (rot.AsByte)
        {
            case Rot8.NorthInt:
            case Rot8.NorthEastInt:
            case Rot8.NorthWestInt:
                DrawMeshes(subMeshesByRot[Rot4.NorthInt], drawPos, angle);
                break;

            case Rot8.SouthInt:
            case Rot8.SouthEastInt:
            case Rot8.SouthWestInt:
                DrawMeshes(subMeshesByRot[Rot4.SouthInt], drawPos, angle);
                break;

            case Rot8.EastInt:
                DrawMeshes(subMeshesByRot[Rot4.EastInt], drawPos, angle);
                break;

            case Rot8.WestInt:
                DrawMeshes(subMeshesByRot[Rot4.WestInt], drawPos, angle);
                break;
        }
    }

    public void DrawMeshes(List<LayerSubMesh> subMeshes, Vector3 drawPos, float extraRotation)
    {
        if (!Visible)
        {
            return;
        }
        int count = subMeshes.Count;
        for (int i = 0; i < count; i++)
        {
            LayerSubMesh layerSubMesh = subMeshes[i];
            if (layerSubMesh.finalized && !layerSubMesh.disabled)
            {
                Graphics.DrawMesh(layerSubMesh.mesh, drawPos, Quaternion.AngleAxis(extraRotation, Vector3.up), layerSubMesh.material, 0);
            }
        }
    }

    public override void Regenerate()
    {
        dirty = true;
        dirtyFrame = Time.frameCount;
    }

    public void RegenerateActually()
    {
        VehicleMapUtility.RotForPrint = Rot4.North;
        bounds = section.CellRect;
        for (var i = 0; i < 4; i++)
        {
            var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(Map);
            component?.cacheMode = true;
            try
            {
                subMeshes = subMeshesByRot[i];
                subMeshes.Clear();
                foreach (IntVec3 intVec in section.CellRect)
                {
                    var list = DeepStorage.Active ? (List<Thing>)DeepStorage.ThingListToDisplay(null, Map, intVec) : intVec.GetThingList(Map);
                    for (var j = 0; j < list.Count; j++)
                    {
                        var thing = list[j];
                        if ((thing.def.seeThroughFog || !Map.fogGrid.IsFogged(thing.Position)) && thing.def.drawerType != DrawerType.None && (thing.def.drawerType != DrawerType.RealtimeOnly || !requireAddToMapMesh) && (thing.def.hideAtSnowOrSandDepth >= 1f || Map.snowGrid.GetDepth(thing.Position) <= thing.def.hideAtSnowOrSandDepth) && thing.Position.x == intVec.x && thing.Position.z == intVec.z)
                        {
                            TakePrintFrom(thing);
                            bounds.Encapsulate(thing.OccupiedDrawRect());
                        }
                    }
                }
                foreach (var subMesh in subMeshes)
                {
                    for (var j = 0; j < subMesh.verts.Count; j++)
                    {
                        var vert = subMesh.verts[j];
                        vert.y /= VehicleMapUtility.YCompress;
                        subMesh.verts[j] = vert;
                    }
                }
                FinalizeMesh(MeshParts.All);
            }
            finally
            {
                component?.cacheMode = false;
                VehicleMapUtility.RotForPrint = VehicleMapUtility.RotForPrint.Rotated(RotationDirection.Clockwise);
            }
        }
        VehicleMapUtility.RotForPrint = Rot4.North;
    }
}
