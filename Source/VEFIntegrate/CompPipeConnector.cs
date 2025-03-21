using HarmonyLib;
using PipeSystem;
using RimWorld;
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
                    var newConnectors = this.PipeNet.connectors.Where(c => c.parent.Map == this.parent.Map);
                    if (!pipeNetManager.pipeNets.Remove(this.PipeNet))
                    {
                        pipeNetCount(pipeNetManager)++;
                    }
                    this.PipeNet = PipeNetMaker.MakePipeNet(newConnectors, this.parent.Map, this.pipeNet);
                    pipeNetManager.pipeNets.Add(this.PipeNet);
                    foreach (var connector in newConnectors.ToArray())
                    {
                        this.Pair.PipeNet.UnregisterComp(connector);
                    }
                    this.Pair = null;
                }
                this.connectReq = false;
            }   
            else
            {
                var flag = false;
                foreach (var c in this.parent.OccupiedRect().ExpandedBy(1).Cells)
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
                            if (compConnector.pipeNet == this.pipeNet)
                            {
                                compConnector.connectReq = true;
                                if (this.PipeNet != compConnector.PipeNet)
                                {
                                    this.Pair = compConnector;
                                    compConnector.Pair = this;
                                    var pipeNet = compConnector.PipeNet;
                                    for (var i = 0; i < pipeNet.connectors.Count; i++)
                                    {
                                        this.PipeNet.RegisterComp(pipeNet.connectors[i]);
                                    }
                                    compConnector.PipeNet = this.PipeNet;
                                    pipeNet.Destroy();
                                    var component = MapComponentCache<PipeNetManager>.GetComponent(compConnector.parent.Map);
                                    pipeNetCount(component) = component.pipeNets.Count;
                                    this.parent.DirtyMapMesh(this.parent.Map);
                                }
                                flag = true;
                                break;
                            }
                        }
                    }
                }
                if (!flag)
                {
                    this.Pair = null;
                }
            }
        }

        public static readonly AccessTools.FieldRef<PipeNetManager, int> pipeNetCount = AccessTools.FieldRefAccess<PipeNetManager, int>("pipeNetsCount");

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
                            this.pipeNet = d;
                            this.parent.DrawColor = d.resource.color;
                            this.PipeNet.UnregisterComp(this);
                            this.PipeNetManager.RegisterConnector(this);
                            pipeNetCount(this.PipeNetManager) = this.PipeNetManager.pipeNets.Count;

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