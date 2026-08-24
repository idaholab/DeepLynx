# Nexus centralized Labels prototype

This development-only App Router page prototypes the centralized sensitivity-label workflow using Nexus's existing Tailwind and DaisyUI default theme.

The prototype reflects these rules:

- Labels have their own Project Management parent tab.
- Label creation, inherited-label details, and user assignments live in one master-detail screen.
- Roles are not assigned sensitivity labels.
- Groups are only a one-time bulk-selection tool.
- Group members are expanded into individual users before saving.
- Duplicate users are removed from the preview.
- Existing assignments are protected during bulk assignment.
- Project managers can remove an assignment from an individual user afterward.

## Add it to Nexus

Copy this folder into:

`src/app/(home)/design/sensitivity-label-concepts/`

The folder should contain:

- `page.tsx`
- `SensitivityLabelConcepts.tsx`

Start Nexus normally and open:

`http://localhost:3000/design/sensitivity-label-concepts`

## Figma capture states

Main Labels screen:

`http://localhost:3000/design/sensitivity-label-concepts?capture=true`

Group and individual-user selection:

`http://localhost:3000/design/sensitivity-label-concepts?capture=true&assign=select`

Expanded, deduplicated user preview:

`http://localhost:3000/design/sensitivity-label-concepts?capture=true&assign=preview`

The centralized content region has the id `figma-capture-target` for element-level capture.

## Notes

- The component uses mock data and local React state.
- It imports icons from `@heroicons/react/24/outline`, which Nexus already uses.
- No changes to `globals.css` are required.
- The assignment modal is interactive: select groups and users, preview the deduplicated users, deselect exceptions, assign, and remove saved user assignments.
- Remove the development route after design review or Figma capture.
