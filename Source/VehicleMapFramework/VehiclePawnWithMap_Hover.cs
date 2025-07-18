using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class VehiclePawnWithMap_Hover : VehiclePawnWithMap
{
    public override Vector3 DrawPos
    {
        get
        {
            if (Spawned && Find.CurrentMap != VehicleMap)
            {
                DrawTracker.tweener.PreDrawPosCalculation();
                Vector3 drawPos = DrawTracker.tweener.TweenedPos;
                drawPos.y = def.Altitude;
                if (DrawTracker.recoilTracker.Recoil > 0f)
                {
                    drawPos = drawPos.PointFromAngle(DrawTracker.recoilTracker.Recoil, DrawTracker.recoilTracker.Angle);
                }
                drawPos.z += drawOffset;
                return drawPos;
            }
            return base.DrawPos;
        }
    }

    protected override void Tick()
    {
        prevOffset = drawOffset;
        if (ignition.Drafted)
        {
            if (!ignitionComplete)
            {
                if (ignitionTick == null)
                {
                    ignitionTick = Find.TickManager.TicksGame;
                }
                else
                {
                    var offsetFactor = Mathf.Min((Find.TickManager.TicksGame - ignitionTick.Value) / ignitionDuration, 1f);
                    if (offsetFactor == 1f)
                    {
                        ignitionComplete = true;
                    }
                    drawOffset = offsetDrafted * offsetFactor;
                }
            }
            else
            {
                drawOffset = offsetDrafted;
                drawOffset += Mathf.Sin(Find.TickManager.TicksGame * 0.075f) * 0.035f;
            }
        }
        else if (ignitionTick != null)
        {
            ignitionTick = null;
            ignitionComplete = false;
            landingComplete = false;
        }

        if (!landingComplete)
        {
            drawOffset = Mathf.Max(0f, drawOffset - 0.004f);
            if (drawOffset == 0f)
            {
                landingComplete = true;
            }
        }
        base.Tick();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref drawOffset, "floatingOffset");
        Scribe_Values.Look(ref prevOffset, "floatingOffsetPrev");
        Scribe_Values.Look(ref drawPosZ, "drawPosZ");
        Scribe_Values.Look(ref ignitionTick, "ignitionTick");
        Scribe_Values.Look(ref ignitionComplete, "ignitionComplete");
        Scribe_Values.Look(ref landingComplete, "landingComplete");
    }

    private float drawOffset = 0f;

    private float prevOffset = 0f;

    private float? drawPosZ;

    private int? ignitionTick;

    private bool ignitionComplete;

    private bool landingComplete = true;

    private readonly float offsetDrafted = 0.25f;

    private const float ignitionDuration = 100f;
}
