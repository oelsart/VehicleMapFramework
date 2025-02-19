using HarmonyLib;
using PipeSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class CompPipeConnector : CompResource
    {
        new public CompProperties_PipeConnector Props => (CompProperties_PipeConnector)this.props;

        public CompPipeConnector Pair { get; set; }

        public override void CompTick()
        {
            base.CompTick();
            if (Find.TickManager.TicksGame % ticksInterval != 0) return;

            if (this.parent.IsOnVehicleMapOf(out _))
            {
                if (!this.connectReq)
                {
                    this.Pair.PipeNet.nextTickRDirty = true;
                    this.PipeNet = PipeNetMaker.MakePipeNet(new[] { this }, this.parent.Map, this.pipeNetDef);
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
                            if (compConnector.pipeNetDef == this.pipeNetDef)
                            {
                                compConnector.connectReq = true;
                                if (compConnector.Pair != this)
                                {
                                    this.Pair = compConnector;
                                    compConnector.Pair = this;
                                    this.PipeNet.Merge(compConnector.PipeNet);
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

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            yield return new Command_Action
            {
                defaultLabel = "VMF_AssignPipeNet".Translate(),
                icon = this.pipeNetDef != null ? uiIcon(this.pipeNetDef) : null,
                action = () =>
                {
                    Find.WindowStack.Add(new FloatMenu(DefDatabase<PipeNetDef>.AllDefs.Select(d =>
                    {
                        return new FloatMenuOption(d.resource.name, () =>
                        {
                            this.pipeNetDef = d;
                            this.parent.DrawColor = d.resource.color;
                        });
                    }).ToList()));
                }
            };
        }

        private static AccessTools.FieldRef<PipeNetDef, Texture2D> uiIcon = AccessTools.FieldRefAccess<PipeNetDef, Texture2D>("uiIcon");

        public override void PostExposeData()
        {
            base.PostExposeData();
        }

        private PipeNetDef pipeNetDef;

        private bool connectReq;

        public const int ticksInterval = 30;
    }
}