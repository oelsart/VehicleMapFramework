using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class CompPipeConnector : ThingComp
    {
        public CompPipeConnector Pair { get; set; }

        public List<IPipeConnector> ConnectorComps
        {
            get
            {
                if (connectorComps == null)
                {
                    connectorComps = parent.AllComps.OfType<IPipeConnector>().ToList();
                }
                return connectorComps;
            }
        }

        private Material PipeMat
        {
            get
            {
                if (this.pipeMat == null)
                {
                    this.pipeMat = MaterialPool.MatFrom("VehicleInteriors/Things/PipeConnector/Pipe", ShaderDatabase.Cutout);
                }
                return this.pipeMat;
            }
        }

        private Graphic PipeEndGraphic
        {
            get
            {
                if (this.pipeEndGraphic == null)
                {
                    this.pipeEndGraphic = GraphicDatabase.Get<Graphic_Single>("VehicleInteriors/Things/PipeConnector/PipeEnd", ShaderDatabase.CutoutComplex);
                }
                return this.pipeEndGraphic.GetColoredVersion(ShaderDatabase.CutoutComplex, this.parent.DrawColor, this.parent.DrawColorTwo);
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (this.Pair != null && this.parent.IsOnVehicleMapOf(out _))
            {
                var y = AltitudeLayer.LightingOverlay.AltitudeFor() - 0.001f;
                var drawPosA = this.parent.DrawPos.WithY(y);
                var drawPosB = this.Pair.parent.DrawPos.WithY(y);
                var graphic = this.PipeEndGraphic;
                var angle = (drawPosB - drawPosA).AngleFlat();
                Graphics.DrawMesh(MeshPool.plane10, drawPosA, Quaternion.AngleAxis(angle, Vector3.up), graphic.MatSingle, 0);
                Graphics.DrawMesh(MeshPool.plane10, drawPosB, Quaternion.AngleAxis(angle + 180f, Vector3.up), graphic.MatSingle, 0);
                GenDraw.DrawLineBetween(drawPosA, drawPosB, -0.001f, this.PipeMat, 1f);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "VMF_AssignPipeNet".Translate(),
                icon = ConnectorComps.Select(c => c.GizmoIcon).FirstOrDefault(t => t != null) ?? BaseContent.ClearTex,
                action = () =>
                {
                    Find.WindowStack.Add(new FloatMenu(ConnectorComps.SelectMany(c => c.FloatMenuOptions).ToList()));
                }
            };
        }

        private Material pipeMat;

        private Graphic pipeEndGraphic;

        protected bool connectReq;

        private List<IPipeConnector> connectorComps;

        public const int ticksInterval = 30;
    }
}
