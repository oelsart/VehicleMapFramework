using System;
using System.Collections.Generic;
using Verse;

namespace VehicleMapFramework;

public class FrameDelay(Game game) : GameComponent
{

  private static List<IJob> currentJobs = [];
  private static List<IJob> nextJobs = [];

  // ReSharper disable once ChangeFieldTypeToSystemThreadingLock
  private static readonly object lockObj = new();
  private readonly Game game = game;

  public static void DelayOne<T>(Action<T> action, T state)
  {
    lock (lockObj)
    {
      nextJobs.Add(Job<T>.Get(action, state));
    }
  }

  public override void GameComponentUpdate()
  {
    lock (lockObj)
    {
      if (nextJobs.Count == 0) return;
      (currentJobs, nextJobs) = (nextJobs, currentJobs);
    }

    for (var i = 0; i < currentJobs.Count; i++)
    {
      try
      {
        currentJobs[i].Execute();
      }
      catch (Exception ex)
      {
        VMF_Log.Error($"Error in FrameDelay: {ex}");
      }
      finally
      {
        currentJobs[i].Return();
      }
    }
    currentJobs.Clear();
  }

  private interface IJob
  {
    void Execute();

    void Return();
  }

  private class Job<T> : IJob
  {
    private Action<T> action;
    private T state;

    public void Execute()
    {
      action(state);
    }

    public void Return()
    {
      state = default;
      action = null;
      SimplePool<Job<T>>.Return(this);
    }

    public static Job<T> Get(Action<T> action, T state)
    {
      var job = SimplePool<Job<T>>.Get();
      job.state = state;
      job.action = action;
      return job;
    }
  }
}
