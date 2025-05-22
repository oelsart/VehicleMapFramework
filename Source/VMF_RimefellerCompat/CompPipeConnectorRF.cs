using Rimefeller;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class CompPipeConnectorRF : CompPipe, IPipeConnector
    {
        new public CompProperties_PipeConnectorRF Props => (CompProperties_PipeConnectorRF)props;

        public CompPipeConnector.PipeMod Mod => CompPipeConnector.PipeMod.Rimefeller;

        public CompPipeConnector CompPipeConnector
        {
            get
            {
                if (compPipeConnector == null)
                {
                    if (!parent.TryGetComp(out compPipeConnector))
                    {
                        Log.Error($"[VehicleMapFramework] CompPipeConnector not found with {parent.LabelCap}.");
                    }
                }
                return compPipeConnector;
            }
        }

        private CompPipeConnectorRF PairComp
        {
            get
            {
                if (pairComp == null)
                {
                    pairComp = CompPipeConnector.Pair?.parent.TryGetComp<CompPipeConnectorRF>();
                }
                return pairComp;
            }
        }

        public IEnumerable<FloatMenuOption> FloatMenuOptions
        {
            get
            {
                yield return new FloatMenuOption(OilPipeline.LabelCap, () =>
                {
                    this.parent.DrawColor = new Color(200f, 200f, 200f);
                    mode = PipeType.Oil;
                });
            }
        }

        public Texture GizmoIcon
        {
            get
            {
                return OilPipeline.uiIcon;
            }
        }

        public bool ConnectCondition(CompPipeConnector another)
        {
            return another.parent.TryGetComp<CompPipeConnectorRF>(out var compPipeConnectorRF) && this.mode == compPipeConnectorRF.mode;
        }

        public void ConnectedTickAction()
        {
            if (PairComp != null)
            {
                pumpUp = pumpUp || pairComp.pumpUp;
                if (pumpUp)
                {
                    var oil = Math.Min(Math.Min(flowAmount * CompPipeConnector.ticksInterval, pipeNet.TotalOil), pairComp.pipeNet.OilStorage.Sum(o => o.space));
                    pipeNet.PullOil(oil);
                    pairComp.pipeNet.PushCrude(oil);
                    var fuel = Math.Min(Math.Min(flowAmount * CompPipeConnector.ticksInterval, pipeNet.TotalFuel), pairComp.pipeNet.FuelStorage.Sum(f => f.space));
                    pipeNet.PullFuel(fuel);
                    pairComp.pipeNet.PushFuel((float)fuel);
                }
                else
                {
                    var oil = Math.Min(Math.Min(flowAmount * CompPipeConnector.ticksInterval, pairComp.pipeNet.TotalOil), pipeNet.OilStorage.Sum(o => o.space));
                    pipeNet.PushCrude(oil);
                    pairComp.pipeNet.PullOil(oil);
                    var fuel = Math.Min(Math.Min(flowAmount * CompPipeConnector.ticksInterval, pairComp.pipeNet.TotalFuel), pipeNet.FuelStorage.Sum(f => f.space));
                    pipeNet.PushFuel((float)fuel);
                    pairComp.pipeNet.PullFuel(fuel);
                }
            }
        }

        public void DisconnectedAction()
        {
            pairComp = null;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (CompPipeConnector.selectedComp == this)
            {
                yield return new Command_ToggleIcon
                {
                    defaultLabel = "VMF_PumpUp".Translate(),
                    labelTwo = "VMF_Drain".Translate(),
                    icon = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/PumpUp"),
                    iconTwo = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/Drain"),
                    isActive = () => pumpUp,
                    toggleSound = SoundDefOf.Checkbox_TurnedOn,
                    toggleAction = () =>
                    {
                        pumpUp = !pumpUp;
                        if (PairComp != null)
                        {
                            pairComp.pumpUp = pumpUp;
                        }
                    },
                };
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref mode, "mode");
            Scribe_Values.Look(ref pumpUp, "pumpUp");
        }

        new public PipeType mode;

        public bool pumpUp = true;

        private CompPipeConnector compPipeConnector;

        private CompPipeConnectorRF pairComp;

        private static readonly ThingDef OilPipeline = DefDatabase<ThingDef>.GetNamed("OilPipeline");

        private const double flowAmount = 3f;
    }
}
