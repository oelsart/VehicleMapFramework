using RimWorld;
using UnityEngine;
using Vehicles;
using Vehicles.Rendering;
using Verse;

namespace VehicleMapFramework;

public class CompDelayedKill : VehicleComp
{
  private DestroyMode destroyMode;

  private Effecter effecter;
  private bool killTimerStarted;

  private bool spawnWreckage;

  private int ticksUntilKilled;

  public CompProperties_DelayedKill Props => (CompProperties_DelayedKill)props;

  public override bool TickByRequest => true;

  public bool KillOnTick => ticksUntilKilled <= 0;

  public bool KillStarted => killTimerStarted;

  public void StartKillTimer(DestroyMode _destroyMode, bool _spawnWreckage)
  {
    if (killTimerStarted) return;
    destroyMode = _destroyMode;
    spawnWreckage = _spawnWreckage;
    killTimerStarted = true;
    StartTicking();
    if (Props.message is not null)
      Messages.Message(Props.message.Translate(parent.LabelCap),
        parent.HostileTo(Faction.OfPlayer) ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.PawnDeath);
  }

  public override void Initialize(CompProperties _props)
  {
    base.Initialize(_props);
    ticksUntilKilled = Props.delayTicks;
  }

  public override void CompTick()
  {
    base.CompTick();
    ticksUntilKilled--;
    if (KillOnTick)
    {
      var request = BlitRequest.For(Vehicle);
      request.rot = Vehicle.FullRotation;
      var drawSize = Vehicle.DrawSize;
      var max = Mathf.Max(drawSize.x, drawSize.y);
      var drawSizeSquared = new Vector2(max, max);
      var rect = new Rect(Vector2.zero, drawSizeSquared * 256);
      var renderTex = VehicleGui.CreateRenderTexture(rect, in request);
      VehicleGui.Blit(renderTex, rect, in request);
      
      var mote = (MoteThrownSinker)ThingMaker.MakeThing(VMF_DefOf.VMF_MoteSink);
      mote.SetParameters(
        renderTex,
        Quaternion.AngleAxis(Vehicle.ExtraAngle, Vector3.up),
        drawSizeSquared.ToVector3().WithY(1f),
        new ColorInt(19, 29, 36).ToColor,
        new SimpleCurve(
          [
            new CurvePoint(0f, 0f),
            new CurvePoint(0.9f, 0.8f),
            new CurvePoint(0.98f, 0.8f),
            new CurvePoint(1f, 0f),
          ]));
      mote.SetVelocity(180f, 0.05f);
      mote.exactPosition = Vehicle.DrawPos;
      GenSpawn.Spawn(mote, mote.exactPosition.ToIntVec3(), Vehicle.Map);
        
      Vehicle.Kill(null, destroyMode, spawnWreckage);
      effecter?.Cleanup();
      effecter = null;
      return;
    }

    effecter ??= Props.effecterDef?.Spawn(parent, parent.Map);
    effecter?.EffectTick(parent, parent);
  }

  public override void PostExposeData()
  {
    base.PostExposeData();
    Scribe_Values.Look(ref killTimerStarted, nameof(killTimerStarted));
    Scribe_Values.Look(ref ticksUntilKilled, nameof(ticksUntilKilled));
    Scribe_Values.Look(ref destroyMode, nameof(destroyMode));
    Scribe_Values.Look(ref spawnWreckage, nameof(spawnWreckage));
  }
}
