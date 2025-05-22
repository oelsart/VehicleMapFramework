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

        public override void CompTick()
        {
            base.CompTick();
            if (Find.TickManager.TicksGame % ticksInterval != 0 || selectedComp == null) return;

            if (this.parent.IsOnVehicleMapOf(out _))
            {
                if (Pair != null && !this.connectReq)
                {
                    selectedComp.DisconnectedAction();
                    Pair = null;
                }
                this.connectReq = false;
            }
            else
            {
                var flag = false;
                foreach (var c in this.parent.OccupiedRect().ExpandedBy(2).Cells)
                {
                    if (!c.InBounds(this.parent.Map)) continue;

                    if (c.TryGetVehicleMap(this.parent.Map, out var vehicle))
                    {
                        var c2 = c.ToVehicleMapCoord(vehicle);
                        if (!c2.InBounds(vehicle.VehicleMap)) continue;

                        var connector = c2.GetFirstThingWithComp<CompPipeConnector>(vehicle.VehicleMap);
                        if (connector != null)
                        {
                            var compConnector = connector.GetComp<CompPipeConnector>();
                            if (compConnector.selectedComp?.Mod != selectedComp.Mod) continue;

                            if (selectedComp.ConnectCondition(compConnector))
                            {
                                compConnector.connectReq = true;
                                if (Pair != compConnector || compConnector.Pair != this)
                                {
                                    Pair = compConnector;
                                    compConnector.Pair = this;
                                }
                                selectedComp.ConnectedTickAction();
                                flag = true;
                                break;
                            }
                        }
                    }
                }
                if (!flag)
                {
                    Pair = null;
                }
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
                icon = selectedComp?.GizmoIcon ?? BaseContent.ClearTex,
                action = () =>
                {
                    Find.WindowStack.Add(new FloatMenu(ConnectorComps.SelectMany(c => c.FloatMenuOptions.Select(f =>
                    {
                        f.action += () =>
                        {
                            selectedComp = c;
                        };
                        return f;
                    })).ToList()));
                }
            };
        }

        public override string CompInspectStringExtra()
        {
            return (selectedComp as ThingComp)?.CompInspectStringExtra();
        }

        public override void PostExposeData()
        {
            var mod = selectedComp?.Mod;
            Scribe_Values.Look(ref mod, "selectedComp");
            selectedComp = ConnectorComps.FirstOrDefault(c => c.Mod == mod);
        }

        public IPipeConnector selectedComp;

        private Material pipeMat;

        private Graphic pipeEndGraphic;

        protected bool connectReq;

        private List<IPipeConnector> connectorComps;

        public const int ticksInterval = 30;

        public enum PipeMod
        {
            VanillaExpandedFramework,
            DubsBadHygiene,
            Rimefeller
        }
    }
}
