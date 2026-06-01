using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BotRunner.Tests.Spec;

/// <summary>
/// Architecture contract tests for the dependency-direction (layering) rule
/// documented in CLAUDE.md ("Dependency flow (strict top-to-bottom)"):
///
///   GameData.Core -> BotCommLayer -> BotRunner -> WoWSharpClient
///     -> Services -> UI
///
/// These are file-only tests: they parse every <c>*.csproj</c> for
/// <c>&lt;ProjectReference&gt;</c> edges and classify each project by its
/// top-level folder. No assembly is loaded, so they run without the native
/// toolchain (mirrors the markdown-drift style of
/// <see cref="SkillsContractTests"/> / <c>FailureReasonCatalogTests</c>).
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert against
/// on-disk artifacts (the .csproj graph), never against agent-loop state.
///
/// The three invariants below are derived from the ACTUAL reference graph, not
/// an idealized one. In particular this test deliberately does NOT assert
/// "no production project references a Tests/ project": one real edge exists
/// (<c>tools/RecordingMaintenance</c> -> <c>Tests/Navigation.Physics.Tests</c>,
/// a maintenance tool that reuses recorded-test data), and that edge is a
/// documented oddity, not a layering violation we want to forbid here.
/// </summary>
public sealed class ProjectLayeringTests
{
    [Fact]
    public void Layering_GameDataCore_HasNoInRepoProjectReferences()
    {
        var edges = BuildReferenceGraph();

        var offenders = edges
            .Where(e => string.Equals(e.FromProject, "GameData.Core", StringComparison.Ordinal))
            .Select(e => $"GameData.Core -> {e.ToProject} ({e.ToSegment})")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "GameData.Core is the foundation layer and must have ZERO in-repo " +
            "ProjectReferences (CLAUDE.md: \"GameData.Core (interfaces, zero " +
            "dependencies)\"). Found:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Layering_Exports_DoNotReferenceUpward()
    {
        var edges = BuildReferenceGraph();
        var upward = new[] { "Services", "UI", "Tests" };

        var offenders = edges
            .Where(e => string.Equals(e.FromSegment, "Exports", StringComparison.Ordinal))
            .Where(e => upward.Contains(e.ToSegment, StringComparer.Ordinal))
            .Select(e => $"{e.FromRelative} -> {e.ToRelative}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Exports/* is the shared bottom layer and must not depend upward on " +
            "Services/, UI/, or Tests/. Found:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Layering_Services_DoNotReferenceUiOrTests()
    {
        var edges = BuildReferenceGraph();
        var upward = new[] { "UI", "Tests" };

        var offenders = edges
            .Where(e => string.Equals(e.FromSegment, "Services", StringComparison.Ordinal))
            .Where(e => upward.Contains(e.ToSegment, StringComparer.Ordinal))
            .Select(e => $"{e.FromRelative} -> {e.ToRelative}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Services/* sit below UI/ and must never reference UI/ or Tests/ " +
            "projects. Found:\n  " + string.Join("\n  ", offenders));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private readonly struct Edge
    {
        public Edge(string fromRelative, string fromSegment, string fromProject,
                    string toRelative, string toSegment, string toProject)
        {
            FromRelative = fromRelative;
            FromSegment = fromSegment;
            FromProject = fromProject;
            ToRelative = toRelative;
            ToSegment = toSegment;
            ToProject = toProject;
        }

        public string FromRelative { get; }
        public string FromSegment { get; }
        public string FromProject { get; }
        public string ToRelative { get; }
        public string ToSegment { get; }
        public string ToProject { get; }
    }

    private static readonly Regex ProjectReferencePattern =
        new(@"<ProjectReference\s+[^>]*Include\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>
    /// Walks every <c>*.csproj</c> under the repo root (skipping bin/obj) and
    /// returns one <see cref="Edge"/> per &lt;ProjectReference&gt;.
    /// </summary>
    private static List<Edge> BuildReferenceGraph()
    {
        var root = LocateRepoRoot();
        var edges = new List<Edge>();

        foreach (var csproj in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            var normalized = csproj.Replace('/', Path.DirectorySeparatorChar)
                                   .Replace('\\', Path.DirectorySeparatorChar);
            if (normalized.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                normalized.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            var fromRel = ToRepoRelative(root, csproj);
            var fromSeg = FirstSegment(fromRel);
            var fromProject = Path.GetFileNameWithoutExtension(csproj);
            var projDir = Path.GetDirectoryName(csproj)!;

            foreach (Match m in ProjectReferencePattern.Matches(File.ReadAllText(csproj)))
            {
                var include = m.Groups[1].Value
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);

                var targetFull = Path.GetFullPath(Path.Combine(projDir, include));
                var toRel = ToRepoRelative(root, targetFull);
                var toSeg = FirstSegment(toRel);
                var toProject = Path.GetFileNameWithoutExtension(targetFull);

                edges.Add(new Edge(fromRel, fromSeg, fromProject, toRel, toSeg, toProject));
            }
        }

        // Sanity: the graph must be non-trivial, otherwise the enumeration or the
        // repo-root walk-up silently failed and every invariant would falsely pass.
        Assert.True(
            edges.Count >= 30,
            $"Expected to parse >=30 ProjectReference edges from .csproj files under " +
            $"'{root}', got {edges.Count}. The repo-root walk-up or csproj enumeration likely failed.");

        return edges;
    }

    /// <summary>
    /// Walks up from the test output directory until <c>WestworldOfWarcraft.sln</c>
    /// is found (mirrors the convention in <see cref="SkillsContractTests"/>).
    /// </summary>
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WestworldOfWarcraft.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate WestworldOfWarcraft.sln by walking up from " +
            $"'{AppContext.BaseDirectory}'. The layering contract test needs the repo root.");
    }

    private static string ToRepoRelative(string root, string fullPath)
    {
        var rel = Path.GetFullPath(fullPath);
        if (rel.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            rel = rel.Substring(root.Length);
        return rel.TrimStart(Path.DirectorySeparatorChar, '/', '\\')
                  .Replace('\\', '/');
    }

    private static string FirstSegment(string repoRelative)
    {
        var idx = repoRelative.IndexOf('/');
        return idx < 0 ? repoRelative : repoRelative.Substring(0, idx);
    }
}
