namespace VehicleMapFramework.Test_DevTools;

public class NamedLazy<T>(string name, Func<T> factory)
{
  private Lazy<T> innerLazy = new(factory);

  public string Name => name;
  
  public T Value => innerLazy.Value;

  public void Reset() => innerLazy = new Lazy<T>(factory);

  public override string ToString() => name;
}