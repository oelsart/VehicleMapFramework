using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Vehicles.Rendering;
using Verse;

namespace VehicleMapFramework;

#if !DEV
// Copyright (c) 2019-2025 Phil
// Derived from Vehicle Framework - Modified and rewritten by OELS (2026)
// Licensed under the MIT License.
public sealed class VehiclePortrait : IDisposable
{
  private RenderTexture renderTex;
  private RenderTextureIdler idler;

  private Config config;

  public struct Config()
  {
    public float iconScale = 1;
    public float expiryTime = -1;
    public bool forceCentering = false;
  }

  public VehiclePortrait()
  {
    config = new Config();
  }

  public VehiclePortrait(in Config config)
  {
    this.config = config;
  }

  private RenderTexture RenderTexture => idler != null ? idler.RenderTex : renderTex;

  public bool RedrawPortrait
  {
    get
    {
      return field || RenderTexture == null;
    }
    private set;
  } = true;

  public void MarkDirty()
  {
    RedrawPortrait = true;
  }

  public void Dispose()
  {
    renderTex?.ReleaseAndDestroy();
    renderTex = null;
    idler?.Dispose();
    idler = null;
  }

  private void CreateRenderTexture(Rect rect, ref readonly BlitRequest request)
  {
    if (RenderTexture != null)
      return;

    if (config.expiryTime > 0)
    {
      idler = new RenderTextureIdler(VehicleGui.CreateRenderTexture(rect, request), config.expiryTime);
    }
    else
    {
      renderTex = VehicleGui.CreateRenderTexture(rect, request);
    }
  }

  /// <summary>
  /// Draw vehicle portrait
  /// </summary>
  /// <param name="rect">Rect to draw the vehicle portrait inside. Contents will be clipped to the rect.</param>
  /// <param name="request">BlitRequest if render texture needs to be redrawn.</param>
  public void Draw(Rect rect, in BlitRequest request)
  {
    if (Event.current.type != EventType.Repaint)
      return;

    Widgets.BeginGroup(rect);
    Rect vehicleRect = rect.AtZero();
    if (RedrawPortrait)
    {
      CreateRenderTexture(vehicleRect, in request);
      VehicleGui.Blit(RenderTexture, vehicleRect, request, iconScale: config.iconScale, config.forceCentering);
      RedrawPortrait = false;
    }
    GUI.DrawTexture(vehicleRect, RenderTexture);
    Widgets.EndGroup();
  }
}

// Copyright (c) 2019-2025 Phil
// Derived from SmashTools - Modified and rewritten by OELS (2026)
// Licensed under the MIT License.
/// <summary>
/// Wrapper class for binding the lifetime of a <see cref="RenderTexture"/> to a timer.
/// <para/>
/// Each time the render texture is read from, the timer will reset to 0. If the timer reaches the expiry
/// threshold — meaning the resources acquired haven't been accessed for that amount of time — all resources will be
/// freed and its timer will stop.
/// </summary>
public sealed class RenderTextureIdler : IDisposable
{
  private readonly RenderTexture renderTex;

  private readonly float expiryTime;
  private float timeSinceRead;

  private RenderTextureIdler(float expiryTime)
  {
    this.expiryTime = expiryTime;
    var type = GenTypes.GetTypeInAnyAssembly("CoreLib.Performance.UnityThread", "CoreLib.Performance") ??
               GenTypes.GetTypeInAnyAssembly("SmashTools.Performance.UnityThread", "SmashTools.Performance");
    var method = AccessTools.Method(type, "StartUpdate");
    var onUpdate = Delegate.CreateDelegate(method.GetParameters()[0].ParameterType, this, nameof(Update));
    method.Invoke(null, [onUpdate]);
  }

  /// <param name="renderTex">RenderTexture used in this wrapper. Will be freed when timer expires.</param>
  /// <param name="expiryTime">Time till resources contained in this wrapper class are destroyed. Time will reset
  /// every time a resource is read.</param>
  public RenderTextureIdler(RenderTexture renderTex, float expiryTime) : this(expiryTime)
  {
    this.renderTex = renderTex;
  }

  public bool Disposed => !renderTex;

  public RenderTexture RenderTex
  {
    get
    {
      timeSinceRead = 0;
      return renderTex;
    }
  }

  internal void SetTimeDirect(float time)
  {
    timeSinceRead = time;
  }

  private bool Update()
  {
    timeSinceRead += Time.deltaTime;

    if (timeSinceRead < expiryTime)
      return true;

    Dispose();
    return false; // Dequeues from Update loop
  }

  public void Dispose()
  {
    renderTex?.ReleaseAndDestroy();
  }
}

[PublicAPI]
public static class RenderTextureUtil
{
  /// <summary>
  /// Releases gpu-side memory and destroys the render texture
  /// </summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void ReleaseAndDestroy(this RenderTexture renderTex)
  {
    if (!renderTex)
      return;

    if (renderTex.IsCreated())
    {
      renderTex.Release();
    }
    UnityEngine.Object.Destroy(renderTex);
  }
}
#endif
