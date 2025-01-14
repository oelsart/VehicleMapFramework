using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class Graphic_VehicleOpacity : Graphic_Vehicle
    {
        public float Opacity
        {
            get
            {
                return this.opacityInt;
            }
            set
            {
                this.opacityInt = value;
                this.Notify_OpacityChanged();
            }
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            Log.Warning(string.Format("Retrieving {0} Colored Graphic from vanilla GraphicDatabase which will result in redundant graphic creation.", base.GetType()));
            return GraphicDatabase.Get<Graphic_VehicleOpacity>(this.path, newShader, this.drawSize, newColor, newColorTwo, this.DataRGB, null);
        }

        private void Notify_OpacityChanged()
        {
            if (!this.materials.NullOrEmpty())
            {
                foreach (var mat in this.materials)
                {
                    mat?.SetFloat("_Opacity", this.opacityInt);
                }
            }
        }

        public override void DrawWorker(Vector3 loc, Rot8 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (this.Opacity == 0f) return;

            Mesh mesh = this.MeshAtFull(rot);
            Quaternion quaternion = base.QuatFromRot(rot);
            //if ((this.EastDiagonalRotated && (rot == Rot8.NorthEast || rot == Rot8.SouthEast)) || (this.WestDiagonalRotated && (rot == Rot8.NorthWest || rot == Rot8.SouthWest)))
            //{
            //    quaternion *= Quaternion.Euler(-Vector3.up);
            //}
            if (extraRotation != 0f)
            {
                quaternion *= Quaternion.Euler(Vector3.up * extraRotation);
            }
            if (this.data != null && this.data.addTopAltitudeBias)
            {
                quaternion *= Quaternion.Euler(Vector3.left * 2f);
            }
            loc += base.DrawOffset(rot);
            Material mat = this.MatAtFull(rot);
            this.DrawMeshInt(mesh, loc, quaternion, mat);
            base.ShadowGraphic?.DrawWorker(loc, rot, thingDef, thing, extraRotation);
        }

        private float opacityInt = 1f;
    }
}
