using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class FrameDelay : MonoBehaviour
{
    public interface IJob
    {
        public void Execute();
        
        public void Return();
    }
    
    private class Job<T> : IJob
    {
        private Action<T> action;
        private T state;

        public static Job<T> Get(Action<T> action, T state)
        {
            var job = SimplePool<Job<T>>.Get();
            job.state = state;
            job.action = action;
            return job;
        }

        public void Execute() => action(state);

        public void Return()
        {
            state = default;
            action = null;
            SimplePool<Job<T>>.Return(this);
        }
    }

    private static readonly List<IJob> currentJobs = [];
    private static readonly List<IJob> nextJobs = [];
    private static readonly Lock lockObj = new();

    private static FrameDelay instance;

    public static void Initialize()
    {
        if (instance != null) return;
        var go = new GameObject("VehicleMapFramework_FrameDelay");
        instance = go.AddComponent<FrameDelay>();
        DontDestroyOnLoad(go);
    }

    public static void DelayOne<T>(Action<T> action, T state)
    {
        lock (lockObj)
        {
            nextJobs.Add(Job<T>.Get(action, state));
        }
    }

    private void Update()
    {
        lock (lockObj)
        {
            if (nextJobs.Count == 0) return;
            currentJobs.AddRange(nextJobs);
            nextJobs.Clear();
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
}