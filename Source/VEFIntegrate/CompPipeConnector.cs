using HarmonyLib;
using PipeSystem;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class CompPipeConnector : CompResource
    {
        public CompPipeConnector Pair { get; set; }

        private Material PipeMat
        {
            get
            {
                if (this.pipeMat == null)
                {
                    this.pipeMat = MaterialPool.MatFrom("VehicleInteriors/Things/Pipe", ShaderDatabase.Cutout);
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
                    this.pipeEndGraphic = GraphicDatabase.Get<Graphic_Single>("VehicleInteriors/Things/PipeEnd", ShaderDatabase.CutoutComplex);
                }
                return this.pipeEndGraphic.GetColoredVersion(ShaderDatabase.CutoutComplex, this.parent.DrawColor, this.parent.DrawColorTwo);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Find.TickManager.TicksGame % ticksInterval != 0 || this.PipeNet == null) return;

            if (this.parent.IsOnVehicleMapOf(out _))
            {
                if (this.Pair != null && !this.connectReq)
                {
                    var pipeNetManager = MapComponentCache<PipeNetManager>.GetComponent(this.parent.Map);
                    this.PipeNet = pipeNetManager.CreatePipeNetFrom(this, this.pipeNet, out var treated);
                    pipeNetManager.pipeNets.Add(this.PipeNet);
                    pipeNetCount(pipeNetManager)++;
                    for (var i = 0; i < treated.Count; i++)
                    {
                        this.Pair.PipeNet.UnregisterComp(treated.ElementAt(i));
                    }
                    this.Pair = null;
                }
                else
                {
                    this.connectReq = false;
                }
            }
            else
            {
                var flag = false;
                foreach (var c in this.parent.OccupiedRect().ExpandedBy(1).Cells)
                {
                    if (!c.InBounds(this.parent.Map)) continue;

                    if (c.ToVector3Shifted().TryGetVehicleMap(this.parent.Map, out var vehicle, false))
                    {
                        var c2 = c.ToVehicleMapCoord(vehicle);
                        if (!c2.InBounds(vehicle.VehicleMap)) continue;

                        var receiver = c2.GetFirstThingWithComp<CompPipeConnector>(vehicle.VehicleMap);
                        if (receiver != null)
                        {
                            var compConnector = receiver.GetComp<CompPipeConnector>();
                            if (compConnector.pipeNet == this.pipeNet)
                            {
                                compConnector.connectReq = true;
                                if (compConnector.Pair != this)
                                {
                                    this.Pair = compConnector;
                                    compConnector.Pair = this;
                                    var pipeNet = compConnector.PipeNet;
                                    for (var i = 0; i < pipeNet.connectors.Count; i++)
                                    {
                                        this.PipeNet.RegisterComp(pipeNet.connectors[i]);
                                    }
                                    pipeNet.Destroy();
                                    pipeNetCount(MapComponentCache<PipeNetManager>.GetComponent(compConnector.parent.Map))--;
                                }
                            }
                            flag = true;
                            break;
                        }
                    }
                }
                if (!flag)
                {
                    this.Pair = null;
                }
            }
        }

        private static readonly AccessTools.FieldRef<PipeNetManager, int> pipeNetCount = AccessTools.FieldRefAccess<PipeNetManager, int>("pipeNetsCount");

        public override void PostDraw()
        {
            base.PostDraw();
            if (this.Pair != null && this.parent.IsOnVehicleMapOf(out _))
            {
                var y = AltitudeLayer.MetaOverlays.AltitudeFor();
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
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            yield return new Command_Action
            {
                defaultLabel = "VMF_AssignPipeNet".Translate(),
                icon = this.pipeNet?.pipeDefs?.FirstOrDefault()?.uiIcon ?? BaseContent.ClearTex,
                action = () =>
                {
                    Find.WindowStack.Add(new FloatMenu(DefDatabase<PipeNetDef>.AllDefs.Select(d =>
                    {
                        return new FloatMenuOption(d.resource.name, () =>
                        {
                            var pipeNetManager = MapComponentCache<PipeNetManager>.GetComponent(this.parent.Map);
                            pipeNetManager.UnregisterConnector(this);
                            this.pipeNet = d;
                            this.parent.DrawColor = d.resource.color;
                            pipeNetManager.RegisterConnector(this);
                        });
                    }).ToList()));
                }
            };
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref this.pipeNet, "pipeNetDef");
        }

        public PipeNetDef pipeNet = DefDatabase<PipeNetDef>.GetNamed("VMF_UnassignedNet");

        private Material pipeMat;

        private Graphic pipeEndGraphic;

        protected bool connectReq;

        public const int ticksInterval = 30;
    }
}