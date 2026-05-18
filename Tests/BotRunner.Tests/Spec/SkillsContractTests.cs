using Xunit;

namespace BotRunner.Tests.Spec;

/// <summary>
/// Spec 15 (Agent Skills) contract tests.
///
/// Skills are agent-loop concepts, not runtime code. These tests assert
/// against:
///   - .claude/skills/&lt;name&gt;/SKILL.md files on disk (frontmatter +
///     required sections)
///   - the SkillAutoBootstrap tool's JSON output
///   - Spec/20 §6 trace artifacts under tmp/test-runtime/traces/Skill_*
///
/// All tests are <see cref="SkipAttribute"/>-marked until the
/// SkillAutoBootstrap tool slot lands (Plan follow-up — 10th orphan
/// service) and the SKILL.md authoring sweep completes for the 18
/// skills listed in Spec/15 §Required new skills.
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against on-disk artifacts and JSON tool output; never against
/// agent-loop internal state.
/// </summary>
public sealed class SkillsContractTests
{
    private const string SlotPendingSkillMd = "contract pending Plan/11 skill-authoring sweep";
    private const string SlotPendingAutoBootstrap = "contract pending SkillAutoBootstrap slot (Plan follow-up)";

    [Fact(Skip = SlotPendingSkillMd)]
    public void SkillsCatalog_EverySpec15ListedSkillHasSkillMd()
    {
        // GIVEN: the union of §Required new skills + §Existing skills
        //        from docs/Spec/15_SKILLS.md.
        // WHEN:  iterating the skill-name list.
        // THEN:  for each name, a file at
        //          .claude/skills/<name>/SKILL.md
        //        OR
        //          ../.claude/skills/<name>/SKILL.md  (monorepo-shared)
        //        exists and parses successfully.
        Assert.Fail("Plan/11 sweep pending — see docs/Spec/15_SKILLS.md");
    }

    [Fact(Skip = SlotPendingSkillMd)]
    public void SkillsCatalog_SkillMdFrontmatterValidYaml()
    {
        // GIVEN: each .claude/skills/<name>/SKILL.md.
        // WHEN:  the leading YAML frontmatter block is parsed.
        // THEN:  parsing succeeds; the 'name' field equals the
        //        containing folder name; 'description' and 'trigger'
        //        are non-empty strings.
        Assert.Fail("Plan/11 sweep pending — see docs/Spec/15_SKILLS.md");
    }

    [Fact(Skip = SlotPendingSkillMd)]
    public void SkillsCatalog_SkillMdHasRequiredSections()
    {
        // GIVEN: each .claude/skills/<name>/SKILL.md.
        // WHEN:  scanning for the 7 required H2 headings from
        //        docs/Spec/15 §Skill registry.
        // THEN:  all 7 are present in order: Goal, Inputs,
        //        Preconditions, Procedure, Verification, Outputs,
        //        Failure modes and recovery, Related skills.
        Assert.Fail("Plan/11 sweep pending — see docs/Spec/15_SKILLS.md");
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
}
