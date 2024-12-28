using SmashTools;
using System;
using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class SectionLayer_ThingsOnVehicle : SectionLayer_ThingsGeneral
    {
        public SectionLayer_ThingsOnVehicle(Section section) : base(section)
        {
        }

        public override CellRect GetBoundaryRect()
        {
            return this.bounds;
        }

        public override void DrawLayer()
        {

            if (!this.Visible)
            {
                return;
            }
            var subMeshes = this.subMeshesByRot[Rot4.NorthInt];
            int count = subMeshes.Count;
            for (int i = 0; i < count; i++)
            {
                LayerSubMesh layerSubMesh = subMeshes[i];
                if (layerSubMesh.finalized && !layerSubMesh.disabled)
                {
                    Graphics.DrawMesh(layerSubMesh.mesh, Matrix4x4.identity, layerSubMesh.material, 0);
                }
            }
        }

        public void DrawLayer(VehiclePawnWithMap vehicle, Vector3 drawPos, float extraRotation)
        {
            if (!DebugViewSettings.drawThingsPrinted)
            {
                return;
            }
            var angle = Ext_Math.RotateAngle(vehicle.FullRotation.AsAngle, extraRotation);
            switch (vehicle.FullRotation.AsByte)
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
                this.subMeshes = new List<LayerSubMesh>();
                VehiclePawnWithMapCache.cacheMode = true;
                foreach (IntVec3 intVec in this.section.CellRect)
                {
                    foreach (var thing in intVec.GetThingList(base.Map))
                    {
                        if ((thing.def.seeThroughFog || !base.Map.fogGrid.IsFogged(thing.Position)) && thing.def.drawerType != DrawerType.None && (thing.def.drawerType != DrawerType.RealtimeOnly || !this.requireAddToMapMesh) && (thing.def.hideAtSnowDepth >= 1f || base.Map.snowGrid.GetDepth(thing.Position) <= thing.def.hideAtSnowDepth) && thing.Position.x == intVec.x && thing.Position.z == intVec.z)
                        {
                            this.TakePrintFrom(thing);
                            this.bounds.Encapsulate(thing.OccupiedDrawRect());
                        }
                    }
                }
                VehiclePawnWithMapCache.cacheMode = false;
                this.FinalizeMesh(MeshParts.All);
                this.subMeshesByRot[i] = this.subMeshes;    
                VehicleMapUtility.rotForPrint.Rotate(RotationDirection.Clockwise);
            }
        }

        protected override void TakePrintFrom(Thing t)
        {
            try
            {
                t.Print(this);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat(new object[]
                {
                    "Exception printing ",
                    t,
                    " at ",
                    t.Position,
                    ": ",
                    ex
                }));
            }
        }

        private CellRect bounds;

        public List<LayerSubMesh>[] subMeshesByRot = new List<LayerSubMesh>[4];
    }
}
