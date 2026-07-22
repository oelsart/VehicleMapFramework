using System;
using System.Collections.Generic;
using System.Linq;
using Rimefeller;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class CompPipeConnectorRF : CompPipe, IPipeConnector
{

  private const double flowAmount = 3f;

  private static readonly ThingDef OilPipeline = DefDatabase<ThingDef>.GetNamed("OilPipeline");

  public new PipeType mode;

  private CompPipeConnectorRF pairComp;

  public bool pumpUp = true;

  public new CompProperties_PipeConnectorRF Props => (CompProperties_PipeConnectorRF)props;

  public CompPipeConnector CompPipeConnector
  {
    get
    {
      if (field == null)
      {
        if (!parent.TryGetComp(out field))
        {
          Log.Error($"[VehicleMapFramework] CompPipeConnector not found with {parent.LabelCap}.");
        }
      }
      return field;
    }
  }

  private CompPipeConnectorRF PairComp
  {
    get
    {
      pairComp ??= CompPipeConnector.Pair?.parent.TryGetComp<CompPipeConnectorRF>();
      return pairComp;
    }
  }

  public CompPipeConnector.PipeMod Mod => CompPipeConnector.PipeMod.Rimefeller;

  public IEnumerable<FloatMenuOption> FloatMenuOptions
  {
    get
    {
      yield return new FloatMenuOption(OilPipeline.LabelCap,
        () =>
        {
          parent.DrawColor = new Color(200f, 200f, 200f);
          mode = PipeType.Oil;
        });
    }
  }

  public Texture GizmoIcon => OilPipeline.uiIcon;

  public bool ConnectCondition(CompPipeConnector another)
  {
    return another.parent.TryGetComp<CompPipeConnectorRF>(out var compPipeConnectorRF) && mode == compPipeConnectorRF.mode;
  }

  public void ConnectedTickAction()
  {
    if (PairComp is { parent.Spawned: true })
    {
      pumpUp = pumpUp || pairComp.pumpUp;
      if (pumpUp)
      {
        var oil = Math.Min(Math.Min(flowAmount * CompPipeConnector.TicksInterval, pipeNet.TotalOil), pairComp.pipeNet.OilStorage.Sum(o => o.space));
        pipeNet.PullOil(oil);
        pairComp.pipeNet.PushCrude(oil);
        var fuel = Math.Min(Math.Min(flowAmount * CompPipeConnector.TicksInterval, pipeNet.TotalFuel), pairComp.pipeNet.FuelStorage.Sum(f => f.space));
        pipeNet.PullFuel(fuel);
        pairComp.pipeNet.PushFuel((float)fuel);
      }
      else
      {
        var oil = Math.Min(Math.Min(flowAmount * CompPipeConnector.TicksInterval, pairComp.pipeNet.TotalOil), pipeNet.OilStorage.Sum(o => o.space));
        pipeNet.PushCrude(oil);
        pairComp.pipeNet.PullOil(oil);
        var fuel = Math.Min(Math.Min(flowAmount * CompPipeConnector.TicksInterval, pairComp.pipeNet.TotalFuel), pipeNet.FuelStorage.Sum(f => f.space));
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
        icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/PumpUp"),
        iconTwo = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/Drain"),
        isActive = () => pumpUp,
        toggleSound = SoundDefOf.Checkbox_TurnedOn,
        toggleAction = () =>
        {
          pumpUp = !pumpUp;
          if (PairComp != null)
          {
            pairComp.pumpUp = pumpUp;
          }
        }
      };
    }
  }

  public override void PostExposeData()
  {
    base.PostExposeData();
    Scribe_Values.Look(ref mode, "mode");
    Scribe_Values.Look(ref pumpUp, "pumpUp");
  }
}
