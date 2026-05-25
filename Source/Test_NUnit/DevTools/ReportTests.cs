using System.Text;
using System.Xml.Linq;

namespace VehicleMapFramework.Test_DevTools;

[TestFixture]
[Category("Local")]
public class ReportTests
{
  [Test]
  [TestCaseSource(typeof(TestCoRunner), nameof(TestCoRunner.TestResults))]
  public void ReportResult(XElement result)
  {
    foreach (var failure in result.Elements("failure"))
    {
      var builder = new StringBuilder(failure.Attribute("message")?.Value ?? "");
      builder.AppendLine(failure.Value);
      Assert.Fail(builder.ToString());
    }
    Assert.Pass(result.Attribute("assertions")?.Value ?? "");
  }
}
