using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BotRunner.Tests.Spec;

/// <summary>
/// Spec 15 (Agent Skills) contract tests.
///
/// Skills are agent-loop concepts, not runtime code. These tests assert
/// against:
///   - .claude/skills/&lt;name&gt;/SKILL.md files on disk (frontmatter +
///     required sections), driven by the skill catalog declared in
///     docs/Spec/15_SKILLS.md (drift test — the spec is the source of truth)
///   - the SkillAutoBootstrap tool's JSON output (still pending its slot)
///
/// The three on-disk artifact tests are live: the Plan/11 SKILL.md authoring
/// sweep has landed all skills listed in Spec/15 §Existing skills and
/// §Required new skills. The three SkillAutoBootstrap tests remain
/// <see cref="FactAttribute.Skip"/>-marked until that off-line tool slot
/// (Plan follow-up — 10th orphan service) lands.
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against on-disk artifacts and JSON tool output; never against
/// agent-loop internal state.
/// </summary>
public sealed class SkillsContractTests
{
    private const string SlotPendingAutoBootstrap = "contract pending SkillAutoBootstrap slot (Plan follow-up)";

    /// <summary>The 8 required H2 sections from Spec/15 §Skill registry, in order.</summary>
    private static readonly string[] RequiredSections =
    {
        "Goal",
        "Inputs",
        "Preconditions",
        "Procedure",
        "Verification",
        "Outputs",
        "Failure modes and recovery",
        "Related skills",
    };

    [Fact]
    public void SkillsCatalog_EverySpec15ListedSkillHasSkillMd()
    {
        var repoRoot = LocateRepoRoot();
        var slugs = ParseSpecSkillSlugs(repoRoot);

        // Sanity: the union of §Existing skills (2) + §Required new skills (18).
        Assert.True(
            slugs.Count >= 20,
            $"Expected >=20 skills parsed from docs/Spec/15_SKILLS.md, got {slugs.Count}: " +
            string.Join(", ", slugs));

        var missing = slugs.Where(slug => ResolveSkillMd(repoRoot, slug) is null).ToList();
        Assert.True(
            missing.Count == 0,
            "Spec/15 lists these skills but no SKILL.md exists at .claude/skills/<name>/SKILL.md " +
            $"(nor ../.claude/skills/<name>/): {string.Join(", ", missing)}");
    }

    [Fact]
    public void SkillsCatalog_SkillMdFrontmatterValidYaml()
    {
        var repoRoot = LocateRepoRoot();

        foreach (var slug in ParseSpecSkillSlugs(repoRoot))
        {
            var path = ResolveSkillMd(repoRoot, slug);
            Assert.True(path is not null, $"SKILL.md missing for '{slug}' — run SkillsCatalog_EverySpec15ListedSkillHasSkillMd.");

            var frontmatter = ParseFrontmatter(File.ReadAllText(path!));

            Assert.True(
                frontmatter.TryGetValue("name", out var name) && name == slug,
                $"'{slug}': frontmatter 'name' must equal the folder name '{slug}' but was '{name ?? "<missing>"}'.");
            Assert.True(
                frontmatter.TryGetValue("description", out var description) && !string.IsNullOrWhiteSpace(description),
                $"'{slug}': frontmatter 'description' is missing or empty.");
            Assert.True(
                frontmatter.TryGetValue("trigger", out var trigger) && !string.IsNullOrWhiteSpace(trigger),
                $"'{slug}': frontmatter 'trigger' is missing or empty.");
        }
    }

    [Fact]
    public void SkillsCatalog_SkillMdHasRequiredSections()
    {
        var repoRoot = LocateRepoRoot();

        foreach (var slug in ParseSpecSkillSlugs(repoRoot))
        {
            var path = ResolveSkillMd(repoRoot, slug);
            Assert.True(path is not null, $"SKILL.md missing for '{slug}' — run SkillsCatalog_EverySpec15ListedSkillHasSkillMd.");

            var headings = ParseH2Headings(File.ReadAllText(path!));

            var lastIndex = -1;
            foreach (var section in RequiredSections)
            {
                var idx = headings.IndexOf(section);
                Assert.True(
                    idx >= 0,
                    $"'{slug}': missing required H2 section '## {section}'.");
                Assert.True(
                    idx > lastIndex,
                    $"'{slug}': section '## {section}' is out of order. " +
                    $"Required order: {string.Join(" -> ", RequiredSections)}.");
                lastIndex = idx;
            }
        }
    }

    [Fact(Skip = SlotPendingAutoBootstrap)]
    public void SkillAutoBootstrap_WwowSelfTargetEveryProvenSkillScoresOneTest()
    {
        // GIVEN: the SkillAutoBootstrap off-line tool AND the WWoW
        //        repo path as the target (self-target calibration canary).
        // WHEN:  the tool runs.
        // THEN:  for every existing-and-proven skill (the 2 §Existing
        //        skills today + skills proven in WWoW), the produced
        //        SkillApplicability.score is >= 0.95. A score below 0.95
        //        for a known-working skill is a calibration regression.
        // See Spec/15 §Live-validation guard.
        Assert.Fail("SkillAutoBootstrap slot pending — see docs/Spec/15_SKILLS.md");
    }

    [Fact(Skip = SlotPendingAutoBootstrap)]
    public void SkillAutoBootstrap_CrossGameSmokeTestPasses()
    {
        // GIVEN: each monorepo-shared skill at
        //        ~/.claude/skills/mmo-*/SKILL.md.
        // WHEN:  the skill's cross-game smoke test (declared in the
        //        SKILL.md Verification section) runs against the FF XI
        //        repo (the default validation target).
        // THEN:  smoke test exits 0; trace at
        //        tmp/test-runtime/traces/Skill_<name>_FfxiSmoke/<ts>.jsonl
        //        contains kind="outcome" with completion="complete".
        Assert.Fail("SkillAutoBootstrap slot pending — see docs/Spec/15_SKILLS.md");
    }

    [Fact(Skip = SlotPendingAutoBootstrap)]
    public void Skills_DynamicProgressive_DispatchedSkillClosesTaskItemTest()
    {
        // GIVEN: trace files at tmp/test-runtime/traces/Skill_<name>_*/
        //        from >=2 prior invocations of the same skill against
        //        DIFFERENT target repos.
        // WHEN:  scanning the outcome lines.
        // THEN:  (dynamic) the per-target-repo task-id sets that the
        //        skill closed differ across the two invocations
        //        (different repos have different open items).
        // AND:   (progressive) each invocation either closed >=1
        //        TASKS.md item OR the outcome line carries an explicit
        //        "infrastructure_only=true" annotation. Aggregate
        //        roster_distance_delta_cross_game across the involved
        //        target repos is <= 0.
        // See Spec/15 §Dynamic-progressive invariant.
        Assert.Fail("SkillAutoBootstrap slot pending — see docs/Spec/15_SKILLS.md");
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Walks up from the test output directory until docs/Spec/15_SKILLS.md is
    /// found (mirrors the convention in <c>FailureReasonCatalogTests</c>).
    /// </summary>
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "Spec", "15_SKILLS.md")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate docs/Spec/15_SKILLS.md by walking up from " +
            $"'{AppContext.BaseDirectory}'. The skills contract test needs the repo root.");
    }

    /// <summary>
    /// Returns the absolute path to a skill's SKILL.md, preferring the repo-local
    /// registry and falling back to the monorepo-shared one, or null if neither
    /// exists.
    /// </summary>
    private static string? ResolveSkillMd(string repoRoot, string slug)
    {
        var local = Path.Combine(repoRoot, ".claude", "skills", slug, "SKILL.md");
        if (File.Exists(local))
            return local;

        var shared = Path.Combine(repoRoot, "..", ".claude", "skills", slug, "SKILL.md");
        return File.Exists(shared) ? Path.GetFullPath(shared) : null;
    }

    /// <summary>
    /// Parses the skill slug list from docs/Spec/15_SKILLS.md: the backtick-wrapped
    /// kebab/single-word tokens in the §Existing skills bullets and the first column
    /// of the §Required new skills table.
    /// </summary>
    private static List<string> ParseSpecSkillSlugs(string repoRoot)
    {
        var specPath = Path.Combine(repoRoot, "docs", "Spec", "15_SKILLS.md");
        var lines = File.ReadAllLines(specPath);

        var slugs = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var section = string.Empty;
        var slugToken = new Regex("`([a-z][a-z0-9-]*)`");

        void Add(string s)
        {
            if (seen.Add(s))
                slugs.Add(s);
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                section = trimmed[3..].Trim();
                continue;
            }

            if (section.StartsWith("Existing skills", StringComparison.OrdinalIgnoreCase)
                && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                var m = slugToken.Match(trimmed);
                if (m.Success)
                    Add(m.Groups[1].Value);
            }
            else if (section.StartsWith("Required new skills", StringComparison.OrdinalIgnoreCase)
                     && trimmed.StartsWith("|", StringComparison.Ordinal))
            {
                // First backtick token on a table row is the skill slug (column 1);
                // header/separator rows carry no backticks and are skipped.
                var m = slugToken.Match(trimmed);
                if (m.Success)
                    Add(m.Groups[1].Value);
            }
        }

        return slugs;
    }

    /// <summary>
    /// Parses the leading <c>---</c> YAML frontmatter block as simple key: value
    /// pairs (no external YAML dependency). Values keep everything after the first
    /// colon, trimmed of surrounding quotes.
    /// </summary>
    private static Dictionary<string, string> ParseFrontmatter(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var normalized = content.Replace("\r\n", "\n").TrimStart('﻿', '\n', ' ');

        if (!normalized.StartsWith("---", StringComparison.Ordinal))
            return result;

        var end = normalized.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
            return result;

        var block = normalized.Substring(3, end - 3);
        foreach (var rawLine in block.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"', '\'');
            result[key] = value;
        }

        return result;
    }

    /// <summary>Returns the ordered list of H2 (<c>## </c>) heading texts in a markdown file.</summary>
    private static List<string> ParseH2Headings(string content)
    {
        var headings = new List<string>();
        foreach (var raw in content.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal) && !line.StartsWith("### ", StringComparison.Ordinal))
                headings.Add(line[3..].Trim());
        }
        return headings;
    }
}
