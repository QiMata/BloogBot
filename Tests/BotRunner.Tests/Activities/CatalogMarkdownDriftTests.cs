using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WoWStateManager.Activities;
using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// Phase 0 slot S0.4 — catalog vs. markdown drift test (Spec/04 invariant 6).
///
/// The compiled <see cref="ActivityCatalog"/> and
/// <c>docs/Plan/Activities/00_INDEX.md</c> must agree on row count and on
/// the exact set of activity Ids. The doc is the spec-side authority for
/// what activities exist; the catalog is the code-side implementation.
/// When this test fires, the divergence is real spec-vs-code drift and a
/// follow-up slot is opened to reconcile.
/// </summary>
public sealed class CatalogMarkdownDriftTests
{
    [Fact]
    public void Catalog_Row_Count_Matches_00_INDEX()
    {
        var catalog = new ActivityCatalog();
        var docPath = LocateIndexDoc();
        var docContent = File.ReadAllText(docPath);
        var docIds = ExtractCatalogIds(docContent);

        // Row count must agree.
        Assert.Equal(catalog.All.Count, docIds.Count);

        // Set-equality is stricter — surface the diff in both directions.
        var catalogIds = catalog.All.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var inCatalogNotDoc = catalogIds.Except(docIds, StringComparer.Ordinal).OrderBy(s => s).ToList();
        var inDocNotCatalog = docIds.Except(catalogIds, StringComparer.Ordinal).OrderBy(s => s).ToList();

        Assert.True(
            inCatalogNotDoc.Count == 0 && inDocNotCatalog.Count == 0,
            "Catalog vs docs/Plan/Activities/00_INDEX.md drift. " +
            "In ActivityCatalog but missing from 00_INDEX.md: [" +
            string.Join(", ", inCatalogNotDoc) + "]. " +
            "In 00_INDEX.md but missing from ActivityCatalog: [" +
            string.Join(", ", inDocNotCatalog) + "].");
    }

    private static string LocateIndexDoc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "Plan", "Activities", "00_INDEX.md");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate docs/Plan/Activities/00_INDEX.md by walking up from " +
            $"'{AppContext.BaseDirectory}'. The catalog drift test needs the index doc to run.");
    }

    private static HashSet<string> ExtractCatalogIds(string content)
    {
        // Catalog id-like tokens inside backticks. Prefix set is anchored
        // to the families the catalog uses; extending the catalog with a
        // new family head requires extending this regex.
        var pattern = new Regex(
            @"`((?:quest|dungeon|raid|bg|prof|econ|rep|attune|event|boss)\.[a-z0-9.\-]+)`");

        var hits = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in pattern.Matches(content))
        {
            hits.Add(m.Groups[1].Value);
        }
        return hits;
    }
}
