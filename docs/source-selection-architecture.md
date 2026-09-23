# Active source selection architecture

This document defines the planned boundary between source access, source processing policy, and the source set a user or campaign elects to use.

The design is inspired by the 5e.tools source-selection workflow, but it preserves Rules Core's stronger provenance, access-control, and published-rules guarantees.

## Core rule

**Being allowed to read a source is not the same as choosing to use it.**

Rules Core must keep three independent questions separate:

1. **Access** — may this principal read the package-owned source content?
2. **Disposition** — should Rules Core globally process/normalize a package for Rules Lawyer workflows?
3. **Selection** — is a publication active in the current user or campaign content context?

Changing one of these must not silently change the others.

## Selection boundary

`SourcePackage` remains the physical access/distribution boundary. It is intentionally not the source-selection boundary because one package may contain many publications and source codes.

Source selection should operate primarily on canonical publication identity. This matches the user-facing concept of choosing books, releases, UA/playtest documents, third-party publications, or homebrew sources while allowing independently imported representations of the same publication to resolve to the same selection.

A package may therefore be readable while all of its publications are inactive, or one publication from a multi-publication package may be active while another is not.

## Source classes

Selection needs a presentation-oriented source class independent of D&D edition.

The planned classes are:

- **Official** — finalized first-party published material, SRDs, and applicable official errata.
- **Prerelease** — UA/playtest/preview material that is not part of the default finalized rules corpus.
- **Third-party** — published non-first-party material.
- **Homebrew** — user/community-authored material.
- **Other** — accessible material that can not yet be classified safely.

The existing `SourceReleaseKinds` remain bibliographic/release metadata. They are evidence for classification but should not be overloaded as the complete selection model.

Classification should be deterministic when provenance is sufficient and explicitly reviewable/overridable when it is ambiguous. Import adapters must not invent a class merely to satisfy UI needs.

## Default profile

The default active-source profile is:

- Official: enabled
- Prerelease: disabled
- Third-party: disabled
- Homebrew: disabled
- Other: disabled

This default is category-based rather than a snapshot of publication IDs. Newly recognized official publications therefore become active by default without rewriting every stored profile.

A profile may add per-publication overrides:

- explicitly include a publication whose class is disabled;
- explicitly exclude a publication whose class is enabled.

This supports the common workflow of "all official sources, plus these two UA documents and this homebrew book" without forcing the user to maintain a giant explicit allow-list.

## Scope

Two persisted selection scopes are planned:

### User default

A signed-in user's default source profile applies to ordinary browsing and tools when no campaign-specific source profile is active.

### Campaign selection

A campaign may override the user's default profile for campaign-scoped rules/content.

Campaign selection is authoritative for campaign consumers such as Character Sheet and other campaign-aware tools. A user's personal selection must not silently change a campaign's effective rules/content.

The UI should make the active scope obvious.

## Effective source set

A future `IEffectiveSourceSelectionService` should resolve an immutable effective source-selection view from:

1. accessible canonical publications;
2. the applicable user/campaign category policy;
3. explicit include/exclude overrides;
4. published ruleset dependencies;
5. source-access constraints.

Conceptually:

`accessible publications ∩ selected publications + required accessible dependencies`

Required dependencies need special handling rather than silent filtering. If a published rule/campaign override depends on a publication that the profile would otherwise exclude, the UI should identify it as **required by the active ruleset**. If no accessible equivalent occurrence can satisfy that dependency, resolution should surface an explicit unavailable-source state instead of silently changing rule meaning.

Canonical recognition may allow an accessible equivalent occurrence from another selected representation/publication to satisfy a rule without granting access to the originally used private representation. Existing source-access invariants remain authoritative.

## Browsing versus rules meaning

The active source set defines the **available content universe** for the current context. Page-local filters may narrow that universe further but must not broaden it beyond the effective source set.

Source selection must not mutate global or campaign rule decisions merely because a checkbox changed.

Rules Lawyer publication and campaign overrides remain explicit operations. Source selection may:

- control which content appears in normal browsers;
- control which optional source material is available to campaign-aware consumers;
- narrow candidate discovery/review surfaces;
- influence which accessible source occurrence can materialize an already-defined canonical rule;
- expose conflicts where an active ruleset depends on an inactive/inaccessible publication.

It must not silently create, delete, or rewrite a published decision.

## Planned persistence model

The exact schema should be introduced only when implementation begins, but the model should support:

- `SourceSelectionProfile`
  - profile ID
  - owner/scope (user or campaign)
  - category-default flags
  - created/updated metadata
- `SourceSelectionOverride`
  - profile ID
  - canonical publication ID
  - disposition: include/exclude
- publication source-class metadata/evidence
  - canonical publication ID
  - source class
  - evidence/method
  - optional reviewed override

Do not store a duplicated list of every official publication in every profile.

## Planned API surface

The eventual API should expose operations equivalent to:

- get effective source selection;
- get selectable accessible publications grouped by source class;
- update category defaults;
- include/exclude individual publications;
- reset to default official-only behavior;
- get selection dependencies/conflicts;
- campaign-specific get/update operations subject to campaign authority.

Read endpoints should expose enough metadata to render source name, publication/release information, class, selected state, override state, access state, and whether a source is required by the current ruleset.

## UI pattern

Rules Core should treat source choice as a shared workspace control, not as a separate giant administration page.

The compact control should show the active state, for example:

`Sources: Official + 3`

Opening it should provide a searchable grouped selector with:

- quick preset: **Official** (default);
- quick preset: **Official + prerelease**;
- groups for Official, Prerelease, Third-party, Homebrew, and Other;
- search by publication/source name;
- per-publication include/exclude;
- an active-scope indicator (personal or campaign);
- Apply/Save and Reset-to-default actions;
- clear indication of sources forced active because the published ruleset depends on them.

A normal page's Source column/filter remains useful for narrowing results *within* the active source universe.

Source management/import remains separate. Adding homebrew makes it accessible; selecting it makes it active.

## Architectural integration

The UI refactor should provide a reusable source-selection control contract now, even before persistence is implemented.

Future consumers should receive an effective source-selection context instead of independently interpreting package grants or source codes. This keeps Source Library, Rules Library, Rules Lawyer, Cross-version Review, Character mechanics, and campaign tools consistent.

The implementation must preserve the existing rule:

`physical source -> access-controlled Source Layer -> canonical recognition -> explicit Rules Layer -> campaign overrides -> resolved rules`

Active source selection is a context applied across that pipeline, not a replacement for any layer.

## Non-goals

The first modular/UI refactor does not need to:

- add the persistence schema immediately;
- automatically classify every historical/imported source;
- change published global rules;
- convert source grants into selections;
- make private homebrew globally visible;
- make every accessible source active.

The immediate requirement is to keep the architecture and shared UI primitives compatible with this model so source selection can be implemented without another broad rewrite.
