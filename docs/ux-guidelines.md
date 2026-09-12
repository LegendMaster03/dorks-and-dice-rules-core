# Rules Core UX Guidelines

Rules Core is a rules workspace, not a generic CRUD dashboard. The interface should make the distinction between source material, adjudicated rules, campaign overrides, and maintenance operations obvious without requiring users to understand the storage model first.

## Information architecture

Primary navigation is organized by user intent:

- **Explore** — inspect source material and published rules.
- **Curate** — normalize, compare, adjudicate, and publish global rules.
- **Campaign** — select baselines and manage campaign-specific overrides.
- **Maintenance** — manage hosted definitions, imports, access, and acquisition records.

Maintenance operations must remain visually subordinate to normal rules work. A user browsing or adjudicating rules should not have to distinguish routine actions from import or infrastructure controls by trial and error.

## Layer language

The UI must preserve the Source Layer / Rules Layer boundary:

- Source material is evidence and provenance, not automatically the table rule.
- Binding a source is deliberate.
- Mechanically identical cross-edition implementations may auto-resolve after deliberate binding.
- Mechanical differences require adjudication.
- Publication is always explicit.
- Campaign rules inherit from a selected published global baseline unless overridden.

Copy should describe these behaviors directly rather than exposing persistence terminology unless the technical detail is useful to the task.

## Page hierarchy

Every workspace view should have one clear primary task. Page structure should generally be:

1. page identity and concise purpose;
2. current state or primary action;
3. the main work queue, browser, or comparison surface;
4. secondary setup or maintenance tools.

Do not place setup forms above the primary work surface unless setup is required to continue.

## Progressive disclosure

Secondary authoring and maintenance tools should be collapsed or otherwise visually subordinate by default when their contents can be large. In particular:

- normalization suggestions should not eagerly load a large result set merely because Rules Lawyer opened;
- manual concept creation is an escape hatch, not the default path;
- upstream source maintenance belongs under Maintenance rather than normal Library actions.

Disclosure must not hide an action that is required to complete the current task.

## Work queues

Authoring lists are work queues, not catalogs. Presentation order should surface actionable state first:

- global concepts needing a decision;
- global concepts with unpublished decisions;
- already-published global concepts;
- campaign concepts with unpublished overrides before unchanged inherited rules.

Catalog/browse surfaces should retain deterministic entity/type/name ordering instead.

## Lists and pagination

Large lists use a shared pagination pattern:

- 100 visible rows per page by default;
- stable ordering before offset/cursor application;
- Previous and Next controls with the current range/page visible;
- filter changes reset to page 1;
- opening a detail and returning preserves the prior page where practical;
- labels must distinguish a page count from a total count.

Never silently truncate a list without a way to reach the remaining records.

## Responsive behavior

Desktop Rules Core uses a persistent workspace rail with the primary content beside it. At narrower widths the navigation becomes a compact horizontally scrollable control so the content retains the full viewport width.

Tables may scroll horizontally when necessary; controls must remain usable without requiring a desktop-width viewport. Avoid fixed widths that assume a particular host-site layout.

## Visual system

Rules Core inherits host Bootstrap color variables and therefore supports the host light/dark appearance. The Rules Core stylesheet should primarily define hierarchy, spacing, density, borders, and state treatment rather than creating an independent color theme.

Use:

- strong typography and spacing for page identity;
- quiet surfaces for secondary panels;
- restrained borders instead of stacking heavy cards;
- badges for compact state, not for ordinary labels;
- clear focus-visible states for keyboard navigation;
- reduced-motion support for decorative transitions.

## Accessibility and interaction

- Active navigation uses `aria-current="page"`.
- Navigation has an accessible label.
- Interactive disclosure elements use native `details`/`summary` where possible.
- Meaning must not depend on color alone.
- Keyboard focus must remain visible.
- In-place rerenders must retain the same presentation and interaction affordances as initial renders.

## Scope discipline

A UX pass may change presentation order, layout, wording, progressive disclosure, and client-side view state. It must not silently change rules semantics, source visibility, authorization, publication behavior, or adjudication authority.
