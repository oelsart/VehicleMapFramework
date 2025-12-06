using RimWorld;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

public abstract class Bullet_ZiplineBase : Bullet, IZiplineEnd
{
    public Verb_LaunchZipline launchVerb;
    
    public CustomZipline.ZipLineData ZipLineData { get; set; }

    public override int UpdateRateTicks
    {
        get
        {
            var baseRate = base.UpdateRateTicks;
            if (baseRate == 1) return baseRate;
            return Find.CurrentMap.BaseMapOrCaravan == this.BaseMapOrCaravan ? 1 : baseRate;
        }
    }

    protected float ArcHeightFactor
    {
        get
        {
            var num = def.projectile.arcHeightFactor;
            var num2 = (destination - origin).MagnitudeHorizontalSquared();
            if (num * num > num2 * 0.2f * 0.2f)
            {
                num = Mathf.Sqrt(num2) * 0.2f;
            }
            return num;
        }
    }
    
    public abstract void DrawZipline(Vector3 drawLoc);

    protected override void TickInterval(int delta)
    {
        if (!this.IsOnNonFocusedVehicleMap || landed)
        {
            base.TickInterval(delta);
            return;
        }
        
        var exactPosition = ExactPosition;
        var rect = new Rect(Vector2.zero, Patch_Map_MapUpdate.MeshSize);
        if (exactPosition.InBounds(Map) || !rect.Contains(exactPosition.ToVector2()))
        {
            base.TickInterval(delta);
            return;
        }
        
        lifetime -= delta;
        ticksToImpact -= delta;
        if (ticksToImpact <= 0)
        {
            ImpactSomething();
        }
    }

    protected override void ImpactSomething()
    {
        Impact(null);
    }
}