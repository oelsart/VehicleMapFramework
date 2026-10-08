using System.Collections;
using System.Diagnostics;
using DevTools.Testing;
using JetBrains.Annotations;
using UnityEngine.Assertions.Comparers;
using Verse;

namespace VehicleMapFramework.Test_DevTools;

public static class ExpectOrSuspend
{
  [MustUseReturnValue] public static IEnumerator That(Func<bool> validator, string message = null)
  {
    Expect.That(validator, message);
    if (!validator()) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator IsTrue(bool condition, string message = null)
  {
    Expect.IsTrue(condition, message);
    if (!condition) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator IsFalse(bool condition, string message = null)
  {
    Expect.IsFalse(condition, message);
    if (condition) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator IsEmpty<T>(IEnumerable<T> collection, string message = null)
  {
    var result = !collection.Any();
    Signal(result, nameof(IsEmpty), message);
    if (!result) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator IsNotEmpty<T>(IEnumerable<T> collection, string message = null)
  {
    var result = collection.Any();
    Signal(result, nameof(IsNotEmpty), message);
    if (!result) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator All<T>(IEnumerable<T> items, Func<T, bool> validator, string message = null)
  {
    var result = items.All(validator);
    Signal(result, nameof(All), message);
    if (!result) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator Any<T>(IEnumerable<T> items, Func<T, bool> validator, string message = null)
  {
    var result = items.Any(validator);
    Signal(result, nameof(Any), message);
    if (!result) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator None<T>(IEnumerable<T> items, Func<T, bool> validator, string message = null)
  {
    var result = !items.Any(validator);
    Signal(result, nameof(None), message);
    if (!result) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator AreApproximatelyEqual(float expected, float actual, string message = null)
  {
    Expect.AreApproximatelyEqual(expected, actual, message);
    if (!FloatComparer.s_ComparerWithDefaultTolerance.Equals(expected, actual)) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator AreNotApproximatelyEqual(float expected, float actual, string message = null)
  {
    Expect.AreNotApproximatelyEqual(expected, actual, message);
    if (FloatComparer.s_ComparerWithDefaultTolerance.Equals(expected, actual)) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator ReferencesAreEqual<T>(T expected, T actual, string message = null) where T : class
  {
    Expect.ReferencesAreEqual(expected, actual, message);
    if (!ReferenceEquals(expected, actual)) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator ReferencesAreNotEqual<T>(T expected, T actual, string message = null) where T : class
  {
    Expect.ReferencesAreNotEqual(expected, actual, message);
    if (ReferenceEquals(expected, actual)) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator AreEqual<T>(T expected, T actual, string message = null)
  {
    Expect.AreEqual(expected, actual, message);
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator AreNotEqual<T>(T expected, T actual, string message = null)
  {
    Expect.AreNotEqual(expected, actual, message);
    if (EqualityComparer<T>.Default.Equals(expected, actual)) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator GreaterThan<T>(T value, T operand, string message = null) where T : IComparable
  {
    Expect.GreaterThan(value, operand, message);
    if (value.CompareTo(operand) <= 0) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator GreaterThanOrEqualTo<T>(T value, T operand, string message = null)
    where T : IComparable
  {
    Expect.GreaterThanOrEqualTo(value, operand, message);
    if (value.CompareTo(operand) < 0) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator LessThan<T>(T value, T operand, string message = null) where T : IComparable
  {
    Expect.LessThan(value, operand, message);
    if (!(value.CompareTo(operand) < 0)) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator LessThanOrEqualTo<T>(T value, T operand, string message = null) where T : IComparable
  {
    Expect.LessThanOrEqualTo(value, operand, message);
    if (value.CompareTo(operand) >= 0 && !EqualityComparer<T>.Default.Equals(value, operand)) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator IsNull<T>(T obj, string message = null) where T : class
  {
    Expect.IsNull(obj, message);
    if (obj != null) yield return Suspend();
  }

  [MustUseReturnValue] public static IEnumerator IsNotNull<T>(T obj, string message = null) where T : class
  {
    Expect.IsNotNull(obj, message);
    if (obj == null) yield return Suspend();
  }

  [MustUseReturnValue]
  [DebuggerHidden]
  public static IEnumerator Throws<T>(Action action, string message = null) where T : Exception
  {
    T exception = null;
    try
    {
      action();
    }
    catch (T ex)
    {
      exception = ex;
    }
    Signal(exception != null, nameof(Throws), message);
    if (exception == null) yield return Suspend();
  }

  private static void Signal(bool result, string context, string label,
    string failureMessage = null)
  {
    SendSignal(result ? Status.Passed : Status.Failed, context, label, failureMessage,
      skipFrames: 3);
  }

  internal static void SendSignal(Status status, string context, string message,
    string failureMessage = null, int skipFrames = 1)
  {
    if (!TestRunner.Active)
    {
      Log.Error(
        "Using Expect outside of test watcher. Use Assert instead, Expect is exclusively for unit testing.");
      return;
    }

    var current = Test.Current;
    current.Status = status;
    if (status is Status.Failed or Status.Canceled or Status.Skipped)
    {
      current.TestContext ??= context;
      current.FailLabel ??= message;
      current.FailMessage ??= failureMessage;
      StackTrace stackTrace = new(skipFrames, true);
      current.StackTrace ??= stackTrace;
    }
  }

  [MustUseReturnValue] private static IEnumerator Suspend()
  {
    Find.TickManager?.Pause();
    yield return Test.Suspend(-1);
  }
}