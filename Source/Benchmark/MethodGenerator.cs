using System.Text;

const int Count = 1000;
var sb = new StringBuilder();
sb.AppendLine("using HarmonyLib; using BenchmarkDotNet.Attributes;");
sb.AppendLine("public class Dummy {");
for (var i = 0; i < Count; i++)
{
    sb.AppendLine($"public static void MethodA_{i}(){{}}");
    sb.AppendLine($"public static void MethodB_{i}(){{}}");
    sb.AppendLine($"public void MethodC_{i}(){{}}");
    sb.AppendLine($"public static void MethodD_{i}(){{}}");
}
sb.AppendLine("}");
for (var i = 0; i < Count; i++)
    sb.AppendLine($"public static class Dummy_{i}{{public static void Method(){{}}}}");

sb.AppendLine("[WarmupCount(0)] public class GetMethodFirstTime {");
        
sb.AppendLine("[Benchmark] public void Delegate() {");
for(var i = 0; i < Count; i++)
    sb.AppendLine($"_ = ((Delegate)Dummy.MethodA_{i}).Method;");
sb.AppendLine("}");
sb.AppendLine("[Benchmark] public void SymbolExtensions() {");
for(var i = 0; i < Count; i++)
    sb.AppendLine($"_ = HarmonyLib.SymbolExtensions.GetMethodInfo(() => Dummy.MethodB_{i});");
sb.AppendLine("}");
sb.AppendLine("[Benchmark] public void InstanceCreation() {");
for(var i = 0; i < Count; i++)
    sb.AppendLine($"_ = ((Action)new Dummy().MethodC_{i}).Method;");
sb.AppendLine("}");
        
sb.AppendLine("[Benchmark] public void AccessTools1000Methods() {");
for(var i = 0; i < Count; i++)
    sb.AppendLine($"_ = AccessTools.Method(typeof(Dummy), \"MethodD_{i}\");");
sb.AppendLine("}");
sb.AppendLine("[Benchmark] public void AccessTools1Methods() {");
for(var i = 0; i < Count; i++)
    sb.AppendLine($"_ = AccessTools.Method(typeof(Dummy_{i}), \"Method\");");
sb.AppendLine("}");
sb.AppendLine("}");

sb.AppendLine("public struct DummyStruct {");
for (var i = 0; i < Count; i++)
{
  sb.AppendLine($"public void MethodA_{i}(){{}}");
  sb.AppendLine($"public void MethodB_{i}(){{}}");
}
sb.AppendLine("}");

sb.AppendLine("[WarmupCount(0)] public class GetStructMethodFirstTime {");
sb.AppendLine("[Benchmark] public void Delegate() {");
for(var i = 0; i < Count; i++)
  sb.AppendLine($"_ = ((Delegate)default(DummyStruct).MethodA_{i}).Method;");
sb.AppendLine("}");
sb.AppendLine("[Benchmark] public void AccessTools2000Methods() {");
for(var i = 0; i < Count; i++)
  sb.AppendLine($"_ = AccessTools.Method(typeof(DummyStruct), \"MethodB_{i}\");");
sb.AppendLine("}");
sb.AppendLine("}");

File.WriteAllText("GetMethodFirstTime.cs", sb.ToString());