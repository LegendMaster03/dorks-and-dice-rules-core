namespace RulesCore.Mechanics;

/// <summary>
/// Pure planner over the existing persistent-influence state. The host must
/// authenticate the current snapshot and atomically persist the complete
/// returned state and journal against ExpectedRevision. No Origin, Class,
/// form, inventory, or resource mutation is performed here.
/// </summary>
public static class NativeSupernaturalProgressionPlanner
{
    public static NativeSupernaturalTransitionPlan Plan(
        NativeCharacterState state,
        NativeSupernaturalTransitionJournal journal,
        NativeSupernaturalDefinition definition,
        NativeSupernaturalTransitionRequest request,
        INativeSupernaturalProgressionAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authority);
        var issues = new List<NativeIssue>();
        void Issue(string code, string path, string detail) => issues.Add(new(code, path, detail));
        NativeSupernaturalTransitionPlan Fail(string status) =>
            new(status, request.ExpectedRevision, null, null, [], [],
                issues.OrderBy(x => x.Path, StringComparer.Ordinal)
                    .ThenBy(x => x.Code, StringComparer.Ordinal).ToArray());

        if (string.IsNullOrWhiteSpace(state.CharacterId) ||
            request.CharacterId != state.CharacterId ||
            journal.CharacterId != state.CharacterId ||
            journal.Revision < 0 || journal.Revision == long.MaxValue ||
            request.ExpectedRevision != journal.Revision ||
            !Enum.IsDefined(request.Kind) ||
            !Id(request.EventId) || !Id(request.InfluenceId) ||
            !Id(request.EvidenceId) ||
            journal.ProcessedEventIds is null ||
            journal.ConsumedEligibilityEvidenceIds is null ||
            journal.ProcessedEventIds.Count > 16384 ||
            journal.ConsumedEligibilityEvidenceIds.Count > 16384 ||
            journal.ProcessedEventIds.Any(x => !Id(x)) ||
            journal.ConsumedEligibilityEvidenceIds.Any(x => !Id(x)) ||
            journal.ProcessedEventIds.Distinct(StringComparer.Ordinal).Count() != journal.ProcessedEventIds.Count ||
            journal.ConsumedEligibilityEvidenceIds.Distinct(StringComparer.Ordinal).Count()
                != journal.ConsumedEligibilityEvidenceIds.Count)
        {
            Issue("invalid-transition-journal", "journal",
                "A verified character, unique event journal, and matching expected revision are required.");
            return Fail("invalid");
        }
        if (journal.ProcessedEventIds.Contains(request.EventId, StringComparer.Ordinal))
        {
            Issue("replayed-transition-event", "request.eventId", "The event has already been consumed.");
            return Fail("invalid");
        }
        if (!ValidDefinition(definition))
        {
            Issue("invalid-supernatural-definition", "definition",
                "A version-pinned source and coherent, bounded source-authored nodes are required.");
            return Fail("invalid");
        }
        if (!ValidBasis(request.Basis) ||
            (request.Kind == NativeSupernaturalTransitionKind.Acquire &&
                (request.NodeId is not null || request.NewExpression is not null)) ||
            (request.Kind == NativeSupernaturalTransitionKind.Advance &&
                (!Id(request.NodeId) || request.NewExpression is not null)) ||
            (request.Kind == NativeSupernaturalTransitionKind.ChangeExpression &&
                (request.NodeId is not null || request.Choices is not null ||
                 request.RequirementEvidence is not null || request.NewExpression is null)) ||
            (request.Kind != NativeSupernaturalTransitionKind.Acquire &&
                request.Kind != NativeSupernaturalTransitionKind.Advance &&
                request.Kind != NativeSupernaturalTransitionKind.ChangeExpression))
        {
            Issue("invalid-supernatural-request", "request",
                "Operation fields, exact source decision and evidence must match their action.");
            return Fail("invalid");
        }

        // Current state verification is not a substitute for authenticating the
        // journal. It also catches duplicate progression/grant IDs across
        // alternate sheets and all already-acquired influences.
        var verified = NativeCharacterInfluenceResolver.Resolve(state,
            NativeCharacterSheetResolver.PrimarySheetId, authority);
        if (verified.Status != "resolved")
        {
            issues.AddRange(verified.Issues);
            return Fail(verified.Status);
        }
        if (!authority.AuthorizesJournal(state.CharacterId, journal) ||
            !authority.AuthorizesDefinition(state.CharacterId, definition, request.Basis))
        {
            Issue("unverified-supernatural-authority", "authority",
                "Current journal and exact source definition require independent host authorization.");
            return Fail("unresolved");
        }

        var influences = state.PersistentInfluences ?? [];
        var current = influences.FirstOrDefault(x => x.Id == request.InfluenceId);
        if (request.Kind == NativeSupernaturalTransitionKind.Acquire && current is not null ||
            request.Kind != NativeSupernaturalTransitionKind.Acquire && current is null)
        {
            Issue("invalid-supernatural-owner", "request.influenceId",
                "Acquisition IDs are unique; other actions require an already owned instance.");
            return Fail("invalid");
        }
        if (current is not null &&
            (current.DefinitionId != definition.Id ||
             current.DefinitionSource != definition.Source))
        {
            Issue("supernatural-source-mismatch", "request.influenceId",
                "The acquired instance is bound to its original definition and version.");
            return Fail("invalid");
        }

        var newProgression = new List<NativeInfluenceResolvedProgression>();
        var newGrants = new List<NativeInfluenceResolvedGrant>();
        var consumed = journal.ConsumedEligibilityEvidenceIds.ToHashSet(StringComparer.Ordinal);
        NativeCharacterPersistentInfluence? proposedInfluence;

        if (request.Kind == NativeSupernaturalTransitionKind.ChangeExpression)
        {
            if (!SameKeys(definition.InitialExpression, request.NewExpression!) ||
                request.NewExpression!.Any(x => !Id(x.Key) || !Id(x.Value, 1024)))
            {
                Issue("invalid-supernatural-expression", "request.newExpression",
                    "Expression must use only the source-defined dimensions and valid values.");
                return Fail("invalid");
            }
            if (!authority.AuthorizesExpressionChange(state.CharacterId, current!, request))
            {
                Issue("unverified-expression-change", "request",
                    "A source rule or scoped table decision must authorize this expression change.");
                return Fail("unresolved");
            }
            proposedInfluence = current! with
            {
                Expression = new Dictionary<string, string>(request.NewExpression!,
                    StringComparer.Ordinal),
                ExpressionEvidenceId = request.EvidenceId
            };
        }
        else
        {
            if (request.Kind == NativeSupernaturalTransitionKind.Acquire)
            {
                if (!authority.AuthorizesAcquisition(state.CharacterId, definition, request))
                {
                    Issue("unverified-supernatural-acquisition", "request",
                        "Acquisition of this exact source by this character was not verified.");
                    return Fail("unresolved");
                }
                proposedInfluence = new NativeCharacterPersistentInfluence(
                    request.InfluenceId, definition.Id, definition.Source,
                    request.EvidenceId, request.EvidenceId,
                    new Dictionary<string, string>(definition.InitialExpression, StringComparer.Ordinal),
                    CopyEffects(definition.AcquisitionEffects, request.InfluenceId,
                        "acquisition", request.EvidenceId), [], []);
            }
            else
            {
                proposedInfluence = current!;
            }
            var nodes = request.Kind == NativeSupernaturalTransitionKind.Acquire
                ? definition.Nodes.Where(x => x.OnAcquisition).ToArray()
                : definition.Nodes.Where(x => x.Id == request.NodeId).ToArray();
            if (request.Kind == NativeSupernaturalTransitionKind.Advance &&
                (nodes.Length != 1 || nodes[0].OnAcquisition))
            {
                Issue("unknown-supernatural-progression", "request.nodeId",
                    "Only an existing source-authored nonacquisition node can advance independently.");
                return Fail("invalid");
            }
            var existing = (proposedInfluence.Progression ?? []).ToList();
            var existingIds = existing.Select(x => x.DefinitionId).ToHashSet(StringComparer.Ordinal);
            var allEffects = proposedInfluence.Effects.ToList();
            var allDecisions = (proposedInfluence.GrantApplicability ?? []).ToList();
            var requiredChoices = new HashSet<string>(StringComparer.Ordinal);
            var requiredEvidence = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (existingIds.Contains(node.Id))
                {
                    Issue("duplicate-supernatural-progression", "nodes." + node.Id,
                        "This source-qualified progression was already earned by this instance.");
                    return Fail("invalid");
                }
                if (node.RequiredNodeIds?.Any(x => !existingIds.Contains(x)) == true)
                {
                    Issue("supernatural-progression-prerequisite", "nodes." + node.Id,
                        "Required earlier progression has not been earned by this influence.");
                    return Fail("ineligible");
                }
                if (node.ExclusiveGroup is not null &&
                    definition.Nodes.Any(x => x.Id != node.Id &&
                        x.ExclusiveGroup == node.ExclusiveGroup && existingIds.Contains(x.Id)))
                {
                    Issue("supernatural-exclusive-branch", "nodes." + node.Id,
                        "A mutually exclusive progression branch was already chosen.");
                    return Fail("ineligible");
                }
                if (node.MinimumCharacterLevel is int min)
                {
                    var levels = state.PrimaryMechanics.Advancements;
                    if (levels is null || levels.Any(x => x is null || x.Level < 1 ||
                        !Id(x.ClassId) || !Id(x.OccurrenceId)) ||
                        levels.Select(x => x.ClassId).Distinct(StringComparer.Ordinal).Count() != levels.Count)
                    {
                        Issue("unverified-character-level", "nodes." + node.Id,
                            "A valid and independently authenticated character-level total is required.");
                        return Fail("unresolved");
                    }
                    var total = levels.Sum(x => (long)x.Level);
                    if (total < min)
                    {
                        Issue("supernatural-level-prerequisite", "nodes." + node.Id,
                            "The verified character has not reached the authored minimum level.");
                        return Fail("ineligible");
                    }
                }
                foreach (var condition in node.RequiredEligibilityKeys ?? [])
                {
                    var verdict = authority.EvaluateRequirement(state.CharacterId,
                        request.InfluenceId, condition, request);
                    if (verdict != NativeSupernaturalEligibilityVerdict.Satisfied)
                    {
                        Issue("supernatural-eligibility-" + verdict.ToString().ToLowerInvariant(),
                            "nodes." + node.Id + "." + condition,
                            "A source-specific eligibility requirement is not satisfied or not verifiable.");
                        return Fail(verdict == NativeSupernaturalEligibilityVerdict.Failed
                            ? "ineligible" : "unresolved");
                    }
                }
                foreach (var key in node.RequiredEventKeys ?? [])
                {
                    if (!requiredEvidence.Add(key) ||
                        request.RequirementEvidence is null ||
                        !request.RequirementEvidence.TryGetValue(key, out var receipt) ||
                        !Id(receipt) || !consumed.Add(receipt))
                    {
                        Issue("missing-or-replayed-supernatural-event", "nodes." + node.Id + "." + key,
                            "Each required event needs distinct unconsumed evidence.");
                        return Fail("invalid");
                    }
                    if (!authority.AuthorizesEventRequirement(state.CharacterId,
                        request.InfluenceId, key, receipt, request))
                    {
                        Issue("unverified-supernatural-event", "nodes." + node.Id + "." + key,
                            "An event label or receipt is not evidence of an actual eligible event.");
                        return Fail("unresolved");
                    }
                }
                var selected = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var choice in node.Choices ?? [])
                {
                    var key = request.Kind == NativeSupernaturalTransitionKind.Acquire
                        ? node.Id + "/" + choice.Id : choice.Id;
                    requiredChoices.Add(key);
                    if (request.Choices is null ||
                        !request.Choices.TryGetValue(key, out var selection) ||
                        !choice.Options.Contains(selection, StringComparer.Ordinal))
                    {
                        Issue("invalid-supernatural-choice", "nodes." + node.Id + "." + choice.Id,
                            "Every finite choice must select exactly one source-authored option.");
                        return Fail("invalid");
                    }
                    selected.Add(choice.Id, selection);
                }
                if (request.Kind == NativeSupernaturalTransitionKind.Advance &&
                    !authority.AuthorizesAdvancement(state.CharacterId, proposedInfluence,
                        node, request))
                {
                    Issue("unverified-supernatural-progression", "nodes." + node.Id,
                        "The character has not been authorized to earn this exact node.");
                    return Fail("unresolved");
                }

                var progressionId = request.InfluenceId + ":p:" + node.Id;
                var grants = (node.Grants ?? []).Select(g => new NativeSheetEarnedGrant(
                    progressionId + ":g:" + g.Id, g.DefinitionId,
                    g.SourceOccurrence, request.EvidenceId, g.LevelReference)).ToArray();
                var earned = new NativeSheetEarnedProgression(progressionId, node.Id,
                    node.SourceOccurrence, request.EvidenceId, grants, selected);
                existing.Add(earned);
                existingIds.Add(node.Id);
                newProgression.Add(new(request.InfluenceId, earned));
                foreach (var grant in grants)
                    newGrants.Add(new(request.InfluenceId, earned.Id, grant,
                        NativeOriginGrantApplicabilityState.Unresolved, null));
                foreach (var g in node.Grants ?? [])
                {
                    var grantId = progressionId + ":g:" + g.Id;
                    foreach (var rule in g.UseRules ?? [])
                        allDecisions.Add(new NativeInfluenceGrantApplicability(
                            grantId, rule.State, rule.Basis,
                            new Dictionary<string, string>(rule.RequiredExpression,
                                StringComparer.Ordinal),
                            request.EvidenceId, rule.ExpressionConditions?.ToArray(),
                            rule.RequiredEarned?.ToArray()));
                }
                allEffects.AddRange(CopyEffects(node.Effects, request.InfluenceId,
                    node.Id, request.EvidenceId));
            }
            if (request.Choices?.Keys.Except(requiredChoices, StringComparer.Ordinal).Any() == true ||
                request.RequirementEvidence?.Keys.Except(requiredEvidence, StringComparer.Ordinal).Any() == true)
            {
                Issue("unauthorized-supernatural-input", "request",
                    "Choice and event evidence fields must be required by the selected authored nodes.");
                return Fail("invalid");
            }
            proposedInfluence = proposedInfluence with
            {
                Progression = existing.ToArray(),
                Effects = allEffects.ToArray(),
                GrantApplicability = allDecisions.ToArray()
            };
        }

        // Old sheets, primary mechanics, and other influences are never
        // reconstructed or re-keyed by supernatural progression changes.
        var proposed = state with
        {
            PersistentInfluences = request.Kind == NativeSupernaturalTransitionKind.Acquire
                ? influences.Append(proposedInfluence).ToArray()
                : influences.Select(x => x.Id == request.InfluenceId ? proposedInfluence : x).ToArray()
        };
        // Never return collections backed by caller-mutable dictionaries or lists.
        proposed = proposed with
        {
            PersistentInfluences = proposed.PersistentInfluences!.Select(CopyInfluence).ToArray()
        };
        var proposedJournal = new NativeSupernaturalTransitionJournal(
            journal.CharacterId, checked(journal.Revision + 1),
            journal.ProcessedEventIds.Append(request.EventId).ToArray(),
            consumed.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        return new NativeSupernaturalTransitionPlan("planned", request.ExpectedRevision,
            proposed, proposedJournal, newProgression.ToArray(), newGrants.ToArray(), []);
    }

    private static bool Id(string? value, int max = 160) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= max;

    private static bool ValidBasis(NativeOriginDecisionBasis? basis) =>
        basis is not null &&
        basis.Adjudication is NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized &&
        (Id(basis.NativeRuleReference) || Id(basis.TableDecisionId));

    private static bool SameKeys(IReadOnlyDictionary<string, string> authored,
        IReadOnlyDictionary<string, string> requested) =>
        authored.Count == requested.Count &&
        authored.Keys.All(k => requested.Keys.Contains(k, StringComparer.Ordinal));

    private static bool ValidDefinition(NativeSupernaturalDefinition source)
    {
        if (!Id(source.Id) || source.Source is null ||
            !Enum.IsDefined(source.Source.Kind) || !Id(source.Source.Id) ||
            !Id(source.Source.Version) || source.Nodes is null ||
            source.Nodes.Count > 256 || source.InitialExpression is null ||
            source.InitialExpression.Count > 128 ||
            source.InitialExpression.Any(x => !Id(x.Key) || !Id(x.Value, 1024)) ||
            (source.AcquisitionEffects?.Count ?? 0) > 512 ||
            !ValidEffects(source.AcquisitionEffects, source.Source))
            return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in source.Nodes)
        {
            if (node is null || !Id(node.Id, 100) || !ids.Add(node.Id) ||
                !Occurrence(node.SourceOccurrence, source.Source) ||
                node.MinimumCharacterLevel is < 1 or > 100000 ||
                !ValidKeys(node.RequiredNodeIds) ||
                !ValidKeys(node.RequiredEventKeys) ||
                !ValidKeys(node.RequiredEligibilityKeys) ||
                (node.RequiredNodeIds?.Contains(node.Id, StringComparer.Ordinal) == true) ||
                (node.Choices?.Count ?? 0) > 128 ||
                (node.Grants?.Count ?? 0) > 256 ||
                (node.Effects?.Count ?? 0) > 512 ||
                (node.ExclusiveGroup is not null && !Id(node.ExclusiveGroup)) ||
                !ValidEffects(node.Effects, source.Source))
                return false;
            var choices = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choice in node.Choices ?? [])
                if (choice is null || !Id(choice.Id) || !choices.Add(choice.Id) ||
                    choice.Options is null || choice.Options.Count is < 1 or > 128 ||
                    choice.Options.Any(x => !Id(x)) ||
                    choice.Options.Distinct(StringComparer.Ordinal).Count() != choice.Options.Count)
                    return false;
            var grants = new HashSet<string>(StringComparer.Ordinal);
            foreach (var grant in node.Grants ?? [])
                if (grant is null || !Id(grant.Id, 80) || !grants.Add(grant.Id) ||
                    !Id(grant.DefinitionId) ||
                    !Occurrence(grant.SourceOccurrence, source.Source) ||
                    (grant.LevelReference is not null &&
                        (grant.LevelReference.Source != NativeSheetLevelSource.TotalClassLevels ||
                         grant.LevelReference.ClassId is not null) &&
                        (grant.LevelReference.Source != NativeSheetLevelSource.SpecificClassLevel ||
                         !Id(grant.LevelReference.ClassId))) ||
                    (grant.UseRules?.Count ?? 0) > 128 ||
                    grant.UseRules?.Any(rule => rule is null ||
                        !Enum.IsDefined(rule.State) || !ValidBasis(rule.Basis) ||
                        rule.RequiredExpression is null ||
                        rule.RequiredExpression.Count > 128 ||
                        rule.RequiredExpression.Any(x => !Id(x.Key) || !Id(x.Value, 1024)) ||
                        rule.ExpressionConditions?.Any(x => x is null ||
                            !Id(x.Dimension) || !Id(x.Value, 1024) ||
                            !Enum.IsDefined(x.Comparison)) == true ||
                        rule.RequiredEarned?.Any(x => x is null ||
                            !Id(x.DefinitionId) ||
                            !Occurrence(x.Occurrence, source.Source)) == true) == true)
                    return false;
        }
        return source.Nodes.All(n =>
            (n.RequiredNodeIds ?? []).All(ids.Contains) &&
            (!n.OnAcquisition || n.RequiredNodeIds is null || n.RequiredNodeIds.Count == 0));
    }

    private static bool ValidKeys(IReadOnlyList<string>? keys) =>
        keys is null || keys.Count <= 128 &&
        keys.All(x => Id(x)) &&
        keys.Distinct(StringComparer.Ordinal).Count() == keys.Count;

    private static bool Occurrence(NativeOriginSourceOccurrence? occurrence,
        NativeOriginSourceReference source) =>
        occurrence is not null && occurrence.Source == source &&
        Id(occurrence.OccurrencePath, 1024);

    private static bool ValidEffects(IReadOnlyList<NativeCharacterInfluenceEffect>? effects,
        NativeOriginSourceReference source) =>
        effects is null || effects.All(x => x is not null && Id(x.Id, 80) &&
            Occurrence(x.DefinitionOccurrence, source) && ValidBasis(x.Basis) &&
            x.TargetSheetIds is not null && x.TargetSheetIds.Count is > 0 and <= 128 &&
            x.RequiredExpression is not null && x.RequiredExpression.Count <= 128 &&
            x.RequiredExpression.All(p => Id(p.Key) && Id(p.Value, 1024)) &&
            x.RequiredEarned?.All(p => p is not null &&
                Occurrence(p.Occurrence, source)) != false);

    private static NativeCharacterInfluenceEffect[] CopyEffects(
        IReadOnlyList<NativeCharacterInfluenceEffect>? effects, string owner,
        string nodeId, string evidence) =>
        (effects ?? []).Select(x => x with
        {
            Id = owner + ":e:" + nodeId + ":" + x.Id,
            AuthorizationEvidenceId = evidence,
            RequiredExpression = new Dictionary<string, string>(
                x.RequiredExpression, StringComparer.Ordinal),
            TargetSheetIds = x.TargetSheetIds.ToArray(),
            ExpressionConditions = x.ExpressionConditions?.ToArray(),
            RequiredEarned = x.RequiredEarned?.ToArray()
        }).ToArray();

    private static NativeCharacterPersistentInfluence CopyInfluence(
        NativeCharacterPersistentInfluence item) => item with
    {
        Expression = new Dictionary<string, string>(item.Expression, StringComparer.Ordinal),
        Effects = item.Effects.Select(x => x with
        {
            RequiredExpression = new Dictionary<string, string>(x.RequiredExpression,
                StringComparer.Ordinal),
            TargetSheetIds = x.TargetSheetIds.ToArray(),
            ExpressionConditions = x.ExpressionConditions?.ToArray(),
            RequiredEarned = x.RequiredEarned?.ToArray()
        }).ToArray(),
        Progression = item.Progression?.Select(p => p with
        {
            Grants = p.Grants.ToArray(),
            Choices = p.Choices is null ? null
                : new Dictionary<string, string>(p.Choices, StringComparer.Ordinal)
        }).ToArray(),
        GrantApplicability = item.GrantApplicability?.Select(x => x with
        {
            RequiredExpression = new Dictionary<string, string>(
                x.RequiredExpression, StringComparer.Ordinal),
            ExpressionConditions = x.ExpressionConditions?.ToArray(),
            RequiredEarned = x.RequiredEarned?.ToArray()
        }).ToArray()
    };
}
