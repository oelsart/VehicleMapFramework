using JetBrains.Annotations;

namespace VehicleMapFramework;

public static class LinqUtility
{
  extension<T>([ItemCanBeNull] IEnumerable<T> enumerable) where T : class
  {
    [ItemNotNull] public IEnumerable<T> NonNull => enumerable.Where(x => x is not null)!;

    public List<T> AsList => enumerable as List<T> ?? [.. enumerable];

    public IReadOnlyList<T> AsReadOnlyList => enumerable as IReadOnlyList<T> ?? [.. enumerable];
  }
  
  extension<T>([ItemCanBeNull] IEnumerable<T?> enumerable) where T : struct
  {
    public IEnumerable<T> NonNull => enumerable.OfType<T>();
  }
}