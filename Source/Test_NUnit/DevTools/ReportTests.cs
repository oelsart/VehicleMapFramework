using System.Xml.Linq;

namespace VehicleMapFramework.Test_DevTools;

[TestFixture]
public class ReportTests
{
    [Test]
    [TestCaseSource(typeof(TestCoRunner), nameof(TestCoRunner.TestResults))]
    public void ReportResult(XElement result)
    {
        foreach (var failure in result.Elements("failure"))
        {
            Assert.Fail(failure.Attribute("message")?.Value ?? "");
        }
        Assert.Pass(result.Attribute("assertions")?.Value ?? "");
    }
}