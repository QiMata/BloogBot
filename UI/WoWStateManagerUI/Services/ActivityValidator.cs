using System.Collections.Generic;
using WoWStateManagerUI.Models;

namespace WoWStateManagerUI.Services
{
    /// <summary>One line in a validation report.</summary>
    public sealed record ValidationIssue(Severity Severity, string Path, string Message);

    public enum Severity { Info, Warning, Error }

    /// <summary>One line in the layered trace walk.</summary>
    public sealed record TraceLine(int Depth, string Marker, string Label);

    public sealed record ActivityValidationResult(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        IReadOnlyList<TraceLine> Trace,
        int ObjectiveCount,
        int TaskCount,
        int ActionCount);

    /// <summary>
    /// Walks an <see cref="ActivityTemplate"/>'s Objective → Task → Action tree
    /// and reports validation issues + a flat trace of every layer. Used by the
    /// Config Editor's Activity Detail panel to render a Hierarchy view that
    /// the user can verify before assigning the activity to characters.
    /// </summary>
    public sealed class ActivityValidator
    {
        public ActivityValidationResult Validate(ActivityTemplate template)
        {
            var issues = new List<ValidationIssue>();
            var trace = new List<TraceLine>();
            var objectiveCount = 0;
            var taskCount = 0;
            var actionCount = 0;

            trace.Add(new TraceLine(0, "Activity", $"{template.DisplayName} ({template.Id})"));

            if (template.Objectives == null || template.Objectives.Count == 0)
            {
                issues.Add(new ValidationIssue(Severity.Warning,
                    template.Id,
                    "Activity has no Objectives — the hierarchy is incomplete."));
            }
            else
            {
                foreach (var objective in template.Objectives)
                {
                    objectiveCount++;
                    var objPath = $"{template.Id}/{objective.Id}";
                    trace.Add(new TraceLine(1, "Objective",
                        $"{objective.DisplayName} ({objective.Id})"));

                    if (string.IsNullOrWhiteSpace(objective.Id))
                        issues.Add(new ValidationIssue(Severity.Error, objPath, "Objective has no Id."));
                    if (string.IsNullOrWhiteSpace(objective.DisplayName))
                        issues.Add(new ValidationIssue(Severity.Error, objPath, "Objective has no DisplayName."));

                    if (objective.Tasks == null || objective.Tasks.Count == 0)
                    {
                        issues.Add(new ValidationIssue(Severity.Error, objPath,
                            "Objective has no Tasks."));
                        continue;
                    }

                    foreach (var task in objective.Tasks)
                    {
                        taskCount++;
                        var taskPath = $"{objPath}/{task.Name}";
                        trace.Add(new TraceLine(2, "Task",
                            $"{task.Name}{(task.Family != null ? $" [{task.Family}]" : "")}"));

                        if (string.IsNullOrWhiteSpace(task.Name))
                            issues.Add(new ValidationIssue(Severity.Error, taskPath, "Task has no Name."));

                        if (task.Actions == null || task.Actions.Count == 0)
                        {
                            issues.Add(new ValidationIssue(Severity.Error, taskPath,
                                "Task has no Actions — every Task must emit at least one ActionMessage."));
                            continue;
                        }

                        foreach (var action in task.Actions)
                        {
                            actionCount++;
                            var actionPath = $"{taskPath}/{action.ActionType}";
                            trace.Add(new TraceLine(3, "Action", action.ActionType));

                            if (string.IsNullOrWhiteSpace(action.ActionType))
                                issues.Add(new ValidationIssue(Severity.Error, actionPath,
                                    "Action has no ActionType."));
                        }
                    }
                }
            }

            var isValid = !issues.Exists(i => i.Severity == Severity.Error);
            return new ActivityValidationResult(isValid, issues, trace,
                objectiveCount, taskCount, actionCount);
        }
    }
}
