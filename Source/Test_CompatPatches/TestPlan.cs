namespace Test_CompatPatches;

[Serializable]
public class TestPlan
{
    public string Name { get; set; }
    
    public List<string> Mods { get; set; }
    
    public Dictionary<string, string> WorkshopIds { get; set; }
    
    public List<string> Categories { get; set; }
}