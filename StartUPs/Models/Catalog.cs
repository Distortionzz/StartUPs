namespace StartUPs.Models;

/// <summary>A named group of apps, e.g. "Gaming".</summary>
public class Category
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<AppEntry> Apps { get; set; } = new();
}

/// <summary>Root object of catalog.json.</summary>
public class Catalog
{
    public int Version { get; set; }
    public string Updated { get; set; } = "";
    public List<Category> Categories { get; set; } = new();
}
