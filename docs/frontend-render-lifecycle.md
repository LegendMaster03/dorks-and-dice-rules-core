# Frontend render lifecycle

Rules Core treats application state and application render functions as the source of truth for its DOM. Application-owned DOM must not use `MutationObserver` as its normal render lifecycle.

## Lifecycle contract

The frontend follows this sequence:

1. Rules Core state or view changes.
2. The owning render function updates the DOM.
3. The explicit presentation phase runs.
4. The DOM settles without further meaningful mutation.

`installRulesCoreUx` installs the view-level presentation lifecycle after the feature modules have composed their render methods. Full `renderActiveView` work therefore receives one presentation pass after the composed view render completes.

Authoring methods that are also called directly for in-place navigation are wrapped once by the UX shell:

- `renderGlobalOverview`
- `renderGlobalConcept`
- `renderCampaignOverview`
- `renderCampaignConcept`

When those methods execute inside a full active-view render, the outer render owns the presentation pass. When they execute directly, the method wrapper explicitly applies the same view presentation phase. A render-depth guard prevents duplicate nested presentation work.

## Partial and in-place updates

Rules Core feature modules build application-owned UI through the shared `element()` helper. That helper invokes the fragment presentation lifecycle as each fragment is created. Tables, responsive table wrappers, forms, lists, and alerts therefore receive their presentation classes before they are inserted or used to replace an existing result region.

This covers partial updates used by the scoped rules browser, Source Library, monster/source detail rendering, source import results, Web-source refresh state, package ignore/restore, normalization results, and source-adapter administration without requiring mutation discovery.

View-level enhancement remains responsible for presentation that depends on the active Rules Core view rather than an individual fragment, including generated page leads, Rules Lawyer workflow copy, secondary-tool disclosure wrappers, and top-level panel/page-lead treatment.

Shared layout primitives live in `ui.js`. Page leads, panels, toolbars, and fields should use those primitives when a feature needs custom markup instead of re-creating Bootstrap card spacing independently. The compact Rules Library remains a specialized list/detail workspace, but its density and spacing define the default direction for the rest of the Rules Core workspace.

## Idempotence

Presentation functions must be safe to repeat against unchanged rendered state:

- fragment classes are added through `classList`, so a repeated pass does not duplicate them;
- generated page leads are created only when one is not already present;
- Rules Lawyer secondary tools are wrapped only when they are still direct cards;
- disclosure event handlers are attached only when a disclosure is first generated;
- workflow copy is assigned only when its text actually differs.

The validation harness exercises repeated full-view and fragment passes, generated-element counts, event-listener counts, view switching, and direct in-place authoring renders.

## MutationObserver policy

Rules Core currently has no `MutationObserver` in its frontend lifecycle. No current Rules Core DOM boundary requires one: the Tool Host supplies context and routing through explicit integration inputs, while Rules Core owns the rendered workspace DOM itself.

A future observer may be introduced only for a genuine external DOM boundary that Rules Core can not receive through an explicit callback, event, or render hook. Such an observer must be centralized, documented at the boundary, and guarded against reacting recursively to DOM changes that Rules Core itself performs.
