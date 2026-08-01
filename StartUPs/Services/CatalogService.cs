using System.IO;
using System.Reflection;
using System.Text.Json;
using StartUPs.Models;

namespace StartUPs.Services;

/// <summary>Reads catalog.json, which is embedded inside the .exe at build time.</summary>
public static class CatalogService
{
    private const string ResourceName = "StartUPs.catalog.json";

    public static Catalog Load()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' was not found. " +
                "Check that catalog.json is marked as an EmbeddedResource in StartUPs.csproj.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var catalog = JsonSerializer.Deserialize<Catalog>(stream, options)
            ?? throw new InvalidOperationException("catalog.json could not be parsed.");

        // Stamp each app with its parent category so the UI can group and filter a flat list.
        foreach (var category in catalog.Categories)
        {
            foreach (var app in category.Apps)
            {
                app.CategoryId = category.Id;
                app.CategoryName = category.Name;
            }
        }

        return catalog;
    }
}
