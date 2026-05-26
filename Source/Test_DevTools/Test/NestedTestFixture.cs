// Copyright (c) 2025 Phil
// Derived from DevTools - Modified and rewritten by OELS (2026)
// Licensed under the MIT License.

using System.Reflection;
using DevTools;
using DevTools.Testing;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Assertions;
using MethodType = DevTools.Testing.MethodType;

namespace VehicleMapFramework.Test_Logics;

public class NestedTestFixture : ITestFixture
{
  private readonly List<ITestFunction> oneTimeSetUps = [];
  private readonly List<ITestFunction> oneTimeTearDowns = [];
  private readonly List<ITestFunction> setUps = [];
  private readonly List<ITestFunction> tearDowns = [];
  private readonly List<ITestFunction> tests = [];

  public NestedTestFixture(Type type, string name, params object[] args)
  {
    Type = type;
    Name = name;
    Args = args ?? [];

    var context = Test.Current.TestCase as ITestFixture ?? ((ITestFunction)Test.Current.TestCase).Fixture;
    Module = context.Module;
    TestType = context.TestType;

    AddFromType(Type);
    foreach (var testFunction in TestFunctions)
    {
      testFunction.Status = Status.NotRun;
    }
  }

  public object[] Args { get; protected internal set; }

  string ITestFixture.SaveFile => "";

  public IEnumerable<ITestFunction> TestFunctions => tests;

  public MetaDataContainer MetaData { get; } = new();

  public TestType TestType { get; }

  public Type Type { get; }

  public ITestModule Module { get; }

  object[] ITestCase.Args
  {
    get => Args;
    set => Args = value;
  }

  public virtual string Name { get; }

  public Status Status { get; set; } = Status.NotRun;

  public bool OneTimeSetUp(object instance)
  {
    return ExecuteAll(instance, oneTimeSetUps);
  }

  public bool OneTimeTearDown(object instance)
  {
    return ExecuteAll(instance, oneTimeTearDowns);
  }

  public bool SetUp(object instance)
  {
    return ExecuteAll(instance, setUps);
  }

  public bool TearDown(object instance)
  {
    return ExecuteAll(instance, tearDowns);
  }

  public virtual object CreateInstance()
  {
    return CreateTestClass();
  }

  public void RunIndependent()
  {
    var instance = CreateInstance();
    var report = new TestReport();
    var testManager = TestRunner.Current.testManager;
    var testToRun = TestFunctions.ToList();

    // DevLog.Write($"Starting nested-tests: {Name}...");
    // DevLog.WriteLine();

    using (new Test.Scope(this))
    {
      using LogWatcher fxWatcher = new(testManager.Config);
      if (!OneTimeSetUp(instance))
      {
        Test.Fail($"Failed to set up {Name}!");
        return;
      }


      foreach (var function in testToRun)
      {
        Assert.IsFalse(function.MissingRequiredMods());

        var testRetries = function.MetaData.Get<ushort>("RetryTest");
        var maxRetries = Mathf.Max(testRetries, testManager.Config.RetryAttempts);
        var attempts = 0;
        do
        {
          using Test.Scope fns = new(function);
          using LogWatcher fnWatcher = new(testManager.Config);
          if (++attempts > 1)
          {
            DevLog.WriteVerbose("Retrying...");
          }
          try
          {
            if (!SetUp(instance))
            {
              Test.Fail($"Failed to set up {Name}!");
              continue;
            }

            if (function.IsSubRoutine())
            {
              throw new NotSupportedException("Sub routine is not supported");
            }
            else
            {
              function.Execute(instance);
            }
          }
          finally
          {
            if (!TearDown(instance))
            {
              Test.Fail($"Failed to tear down {Name}!");
            }
          }
        } while (attempts <= maxRetries && function.Status is Status.Failed);

        report.Add(function);
      }
      if (!OneTimeTearDown(instance))
      {
        Test.Fail($"Failed to tear down {Name}!");
      }
    }

    AccessTools.Method(typeof(Test), "LogResults").Invoke(null, [this, testToRun]);
    // DevLog.Flush();
    // DevLog.WriteLine();
    // DevLog.Write(
    //     $"Nested-tests Completed. ({report.Count(Status.Passed)} Passed, {report.Count(Status.Failed)} Failed, {report.Count(Status.Skipped)} Skipped)");
  }

  private object CreateTestClass()
  {
    return Args.Length != 0 ? Activator.CreateInstance(Type, Args) : Activator.CreateInstance(Type);
  }

  private static bool ExecuteAll(object instance, List<ITestFunction> functions)
  {
    var success = true;
    foreach (var function in functions)
    {
      function.Status = Status.Pending;
      function.Execute(instance);
      success &= function.Status is Status.Pending;
    }
    return success;
  }

  public void AddFromType(Type type)
  {
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Static | BindingFlags.Instance))
    {
      this.AddTestMethods<SetUpAttribute>(method, MethodType.SetUp, setUps);
      this.AddTestMethods<TearDownAttribute>(method, MethodType.TearDown, tearDowns);
      this.AddTestMethods<OneTimeSetUpAttribute>(method, MethodType.SetUp, oneTimeSetUps);
      this.AddTestMethods<OneTimeTearDownAttribute>(method, MethodType.TearDown, oneTimeTearDowns);
      this.AddTestMethods<TestAttribute>(method, MethodType.Test, tests);
    }
  }
}
