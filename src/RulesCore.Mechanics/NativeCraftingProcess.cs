namespace RulesCore.Mechanics;

/// <summary>
/// A source-authored input or product of a crafting operation. Amounts are
/// recipe units, not inferred gold values; inventory changes belong to the
/// character workflow only after a successful procedure.
/// </summary>
public sealed record NativeCraftingMaterialQuantity(string MaterialId, int Amount)
{
    // A recipe may document the part of its input retained in the manufactured
    // product. This describes physical contents; it does not change inventory
    // consumption, costs, or the shared process stage validator.
    public bool RetainedInProduct { get; init; }
}

/// <summary>
/// One refining, alloying, assembly, enchanting, or other manufacturing stage.
/// A stage can require a different Competency and an earlier completed stage.
/// All readiness facts and completed stages must come from a trusted caller.
/// </summary>
public sealed record NativeCraftingProcessStep(
    string StepId,
    string CompetencyId,
    int MinimumRanks,
    int? PerformerRanks,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<NativeCraftingMaterialQuantity> Inputs,
    IReadOnlyList<NativeCraftingMaterialQuantity> Outputs,
    bool? HasRequiredKnowledge,
    bool? HasAvailableInputs,
    bool? HasRequiredFacilities,
    bool Completed = false);

public sealed record NativeCraftingProcessStepResult(
    string StepId, string Status, IReadOnlyList<string> Reasons);

public sealed record NativeCraftingProcessResult(
    string Status, IReadOnlyList<NativeCraftingProcessStepResult> Steps,
    IReadOnlyList<string> Issues);

/// <summary>
/// A read-only stage planner. It validates resource and dependency structure,
/// including cycles, before reporting ready, blocked or unresolved stages.
/// It never produces output materials, consumes inputs or rolls a craft check.
/// </summary>
public static class NativeCraftingProcess
{
    public static NativeCraftingProcessResult Evaluate(
        IReadOnlyList<NativeCraftingProcessStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var issues = new List<string>();
        if (steps.Count is < 1 or > 100)
            return new("invalid", [], ["invalid-crafting-stage-count"]);

        var byId = new Dictionary<string, NativeCraftingProcessStep>(
            StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (step is null || !ValidId(step.StepId) ||
                !ValidId(step.CompetencyId) ||
                step.MinimumRanks is < 0 or > 1_000_000 ||
                step.Dependencies is null || step.Dependencies.Count > 100 ||
                step.Inputs is null || step.Inputs.Count > 100 ||
                step.Outputs is null || step.Outputs.Count > 100 ||
                !ValidMaterials(step.Inputs) ||
                !ValidMaterials(step.Outputs))
            {
                issues.Add("invalid-crafting-stage-definition");
                continue;
            }
            if (!byId.TryAdd(step.StepId, step))
                issues.Add("duplicate-crafting-stage-id");
        }

        if (issues.Count > 0)
            return new("invalid", [], issues.Distinct().ToArray());

        foreach (var step in steps)
        {
            if (step.Dependencies.Distinct(StringComparer.Ordinal).Count() !=
                step.Dependencies.Count ||
                step.Dependencies.Any(id => !ValidId(id) ||
                    id == step.StepId || !byId.ContainsKey(id)))
                issues.Add("invalid-crafting-stage-dependency");
        }
        if (issues.Count > 0)
            return new("invalid", [], issues.Distinct().ToArray());

        // Cycle rejection is necessary even when every stage is externally
        // claimed complete; otherwise a cyclic recipe could bypass ordering.
        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (states.GetValueOrDefault(id) == 1) return false;
            if (states.GetValueOrDefault(id) == 2) return true;
            states[id] = 1;
            foreach (var dependency in byId[id].Dependencies)
                if (!Visit(dependency)) return false;
            states[id] = 2;
            return true;
        }
        if (byId.Keys.Any(id => !Visit(id)))
            return new("invalid", [], ["cyclic-crafting-stage-dependency"]);

        // A persisted completed stage cannot depend on an unfinished stage.
        // Accepting that contradictory state would allow later stages to be
        // marked ready without their prerequisites ever being performed.
        if (steps.Any(step => step.Completed &&
            step.Dependencies.Any(id => !byId[id].Completed)))
            return new("invalid", [], ["completed-stage-has-unfinished-dependency"]);

        var results = new List<NativeCraftingProcessStepResult>();
        foreach (var step in steps)
        {
            if (step.Completed)
            {
                results.Add(new(step.StepId, "completed", []));
                continue;
            }
            var denied = new List<string>();
            var missing = new List<string>();
            if (step.Dependencies.Any(id => !byId[id].Completed))
                denied.Add("dependent-stage-not-complete");
            if (step.PerformerRanks is not int ranks || ranks is < 0 or > 1_000_000)
                missing.Add("performer-rank-unresolved");
            else if (ranks < step.MinimumRanks)
                denied.Add("performer-rank-too-low");
            Check(step.HasRequiredKnowledge, "technique", denied, missing);
            Check(step.HasAvailableInputs, "materials", denied, missing);
            Check(step.HasRequiredFacilities, "facilities", denied, missing);

            results.Add(denied.Count > 0
                ? new(step.StepId, "blocked", denied.Concat(missing).ToArray())
                : missing.Count > 0
                    ? new(step.StepId, "unresolved", missing)
                    : new(step.StepId, "ready", []));
        }

        return new(results.All(r => r.Status == "completed") ? "completed" :
            results.Any(r => r.Status == "ready") ? "ready" :
            results.Any(r => r.Status == "unresolved") ? "unresolved" :
            "blocked", results, []);
    }

    private static void Check(bool? value, string label,
        List<string> denied, List<string> missing)
    {
        if (value is null) missing.Add(label + "-unresolved");
        else if (!value.Value) denied.Add(label + "-missing");
    }

    private static bool ValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 160;

    private static bool ValidMaterials(
        IReadOnlyList<NativeCraftingMaterialQuantity> materials) =>
        materials.All(material => material is not null &&
            ValidId(material.MaterialId) && material.Amount > 0) &&
        materials.Select(material => material.MaterialId)
            .Distinct(StringComparer.Ordinal).Count() == materials.Count;
}
