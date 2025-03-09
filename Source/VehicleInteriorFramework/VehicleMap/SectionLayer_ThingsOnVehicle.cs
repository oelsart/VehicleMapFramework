using HarmonyLib;
using SmashTools;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public abstract class SectionLayer_ThingsOnVehicle : SectionLayer_Things
    {
        public SectionLayer_ThingsOnVehicle(Section section) : base(section)
        {
            this.deepStorageActive = ModsConfig.IsActive("lwm.deepstorage");
            if (this.deepStorageActive)
            {
                this.m_ThingListToDisplay = MethodInvoker.GetHandler(AccessTools.Method("LWM.DeepStorage.PatchDisplay_SectionLayer_Things_Regenerate:ThingListToDisplay"));
            }
            for (var i = 0; i < 4; i++)
            {
                this.subMeshesByRot[i] = new List<LayerSubMesh>();
            }
        }

        public override CellRect GetBoundaryRect()
        {
            return this.bounds;
        }

        //drawPlanetがオフでVehicleMapにフォーカスした時しか呼ばれないよ
        public override void DrawLayer()
        {
            this.DrawLayer(Rot8.North, Vector3.zero, 0f);
        }

        public void DrawLayer(Rot8 rot, Vector3 drawPos, float extraRotation)
        {
            if (!DebugViewSettings.drawThingsPrinted)
            {
                return;
            }
            var angle = Ext_Math.RotateAngle(rot.AsAngle, extraRotation);
            switch (rot.AsByte)
            {
                case Rot8.NorthInt:
                case Rot8.NorthEastInt:
                case Rot8.NorthWestInt:
                    this.DrawMeshes(this.subMeshesByRot[Rot4.NorthInt], drawPos, angle);
                    break;

                case Rot8.SouthInt:
                case Rot8.SouthEastInt:
                case Rot8.SouthWestInt:
                    this.DrawMeshes(this.subMeshesByRot[Rot4.SouthInt], drawPos, angle);
                    break;

                case Rot8.EastInt:
                    this.DrawMeshes(this.subMeshesByRot[Rot4.EastInt], drawPos, angle);
                    break;

                case Rot8.WestInt:
                    this.DrawMeshes(this.subMeshesByRot[Rot4.WestInt], drawPos, angle);
                    break;
            }
        }

        public void DrawMeshes(List<LayerSubMesh> subMeshes, Vector3 drawPos, float extraRotation)
        {
            if (!this.Visible)
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
            this.bounds = this.section.CellRect;
            for (var i = 0; i < 4; i++)
            {
                var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(base.Map);
                component.cacheMode = true;
                try
                {
                    this.subMeshes = this.subMeshesByRot[i];
                    this.subMeshes.Clear();
                    foreach (IntVec3 intVec in this.section.CellRect)
                    {
                        var list = this.deepStorageActive ? (List<Thing>)this.m_ThingListToDisplay(null, base.Map, intVec) : intVec.GetThingList(base.Map);
                        foreach (var thing in list)
                        {
                            if ((thing.def.seeThroughFog || !base.Map.fogGrid.IsFogged(thing.Position)) && thing.def.drawerType != DrawerType.None && (thing.def.drawerType != DrawerType.RealtimeOnly || !this.requireAddToMapMesh) && (thing.def.hideAtSnowDepth >= 1f || base.Map.snowGrid.GetDepth(thing.Position) <= thing.def.hideAtSnowDepth) && thing.Position.x == intVec.x && thing.Position.z == intVec.z)
                            {
                                this.TakePrintFrom(thing);
                                this.bounds.Encapsulate(thing.OccupiedDrawRect());
                            }
                        }
                    }
                    this.FinalizeMesh(MeshParts.All);
                }
                finally
                {
                    component.cacheMode = false;
                    VehicleMapUtility.rotForPrint.Rotate(RotationDirection.Clockwise);
                }
            }
        }

        private CellRect bounds;

        public List<LayerSubMesh>[] subMeshesByRot = new List<LayerSubMesh>[4];

        private bool deepStorageActive;

        private FastInvokeHandler m_ThingListToDisplay;
    }
}
