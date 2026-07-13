# DeepLynx Nexus Frontend Development Style Guide

This guide documents frontend development conventions for DeepLynx Nexus. It is written in Markdown so it can be used in GitHub or copied into Confluence.

This guide intentionally excludes backend API, business logic, data access, and .NET conventions.

## Purpose

Use this guide when adding or changing pages, route layouts, client components, frontend services, shared UI components, theme behavior, frontend tests, or user-facing interactions in `deeplynx.UI`.

The goal is consistency:

- Keep routes thin and workflow-specific.
- Keep reusable UI in shared components.
- Keep API calls in service modules.
- Use the established theme tokens.
- Preserve App Router server/client boundaries.
- Make user flows easy to test with Playwright.

## Frontend App Breakdown

The primary frontend lives in `deeplynx.UI` and uses Next.js App Router, React, TypeScript, Tailwind CSS, DaisyUI, and Playwright.

| Project or folder | Responsibility |
|---|---|
| `deeplynx.UI/src/app` | Next.js App Router routes, layouts, providers, hooks, services, schemas, and page-specific components. |
| `deeplynx.UI/src/app/(home)` | Authenticated application routes, shared shell components, project pages, management pages, graph views, records, upload center, and similar workspace flows. |
| `deeplynx.UI/src/app/(orgSelection)` | Organization selection routes and layout. |
| `deeplynx.UI/src/app/(login)` | Login routes. |
| `deeplynx.UI/src/app/contexts` | Global React providers such as session, language, toast, organization session, and project session. |
| `deeplynx.UI/src/app/lib/client_service` | Browser-side API service modules. |
| `deeplynx.UI/src/app/lib/server_service` | Server-side API service modules and server-only guards. |
| `deeplynx.UI/src/app/hooks` | Reusable hooks shared across routes and features. |
| `deeplynx.UI/src/app/schemas` | Shared validation schemas. |
| `deeplynx.UI/src/app/globals.css` | Tailwind import, DaisyUI theme definitions, theme variables, and global styles. |
| `deeplynx.UI/styles` | Additional global styles for third-party UI such as Shepherd tours. |
| `deeplynx.UI/tests` | Playwright end-to-end and UI behavior tests. |
| `deeplynx.docs` | Separate documentation site. Do not place primary application UI here. |

## Route and Component Organization

Use the App Router filesystem as the main organizing boundary:

- Put route files in the relevant route group, for example `(home)`, `(login)`, or `(orgSelection)`.
- Keep `page.tsx` focused on route composition, data loading, and selecting the main client view.
- Move interactive workflows into named client components such as `ProjectDetailClient`, `UploadCenterClient`, or `RecordViewClient`.
- Keep route-specific helper components next to the route that owns them.
- Put components reused by multiple home routes in `src/app/(home)/components`.
- Put feature-specific components in a `components` folder under the feature route.
- Name React components with `PascalCase`.
- Name hooks with `use` prefixes and keep them in `src/app/hooks` when shared.
- Prefer existing shared components before creating a new control for tables, modals, search, pagination, cards, tabs, banners, or skeletons.

Do not add new top-level UI directories unless the component is genuinely shared across route groups and does not belong to `(home)`.

## Server and Client Component Rules

Default to server components when a component only composes markup or loads server-safe data. Add `"use client"` only when the component needs browser APIs, React state, effects, refs, client routing, context hooks, event handlers, or client-side services.

Client components may use:

- `useState`, `useEffect`, and other React client hooks.
- `useRouter`, `usePathname`, and other `next/navigation` client hooks.
- App contexts from `src/app/contexts`.
- Browser storage, dialogs, drag/drop, and other DOM APIs.
- Services from `src/app/lib/client_service`.

Server components and server utilities may use:

- Server services from `src/app/lib/server_service`.
- Server-side route guards.
- Environment variables that must not be shipped to the browser.

Do not import server services into client components. Do not read browser-only state from server components.

## Data Access

Keep API access behind service modules:

- Browser calls belong in `src/app/lib/client_service`.
- Server-side calls belong in `src/app/lib/server_service`.
- Use the existing API client wrappers rather than duplicating base URLs, headers, token handling, or response parsing.
- Keep service functions named after the user action or API behavior they represent.
- Return typed DTOs from service functions where practical.
- Keep parsing and validation close to service boundaries when the data shape is uncertain.

Pages and components should not hand-roll `fetch` or `axios` calls when a service module exists for the domain.

## State and Context

Use the existing providers in `src/app/contexts` for app-wide state:

- `ClientProviders` wraps session, language, toast, and global toaster behavior.
- `OrganizationSessionProvider` owns selected organization state.
- `ProjectSessionProvider` owns selected project state.
- `LanguageProvider` owns translations.
- `ToastProvider` owns shared toast interactions.

Prefer local component state for temporary UI state such as modal visibility, current tab, local filters, and draft form values. Promote state to a context only when multiple unrelated areas of the app need to read or write the same value.

When switching organizations, clear project-specific state before redirecting to organization-scoped views.

## Theming, Styles, and Colors

The UI is theme-driven. Components should describe their visual intent with DaisyUI and Tailwind theme tokens rather than fixed colors.

Themes are defined in `src/app/globals.css` through DaisyUI theme blocks. Each organization theme has a light and dark variant, such as `default` and `default-dark`. The root layout sets `data-theme` before hydration based on:

- `dlx-theme-mode` in local storage.
- `organizationSession.themeName`.
- The supported organization themes.

### Style Sources

Use Tailwind utility classes and DaisyUI component classes as the default styling layer.

Preferred style sources, in order:

1. Existing shared component variants.
2. DaisyUI component classes such as `btn`, `modal`, `dropdown`, `menu`, `table`, `card`, `badge`, `alert`, `loading`, and `join`.
3. Tailwind utilities using theme tokens.
4. Small component-scoped class combinations.
5. Global CSS only for theme definitions, third-party overrides, or behavior that cannot reasonably live on a component.

Do not add separate CSS files for routine component styling. Prefer class names in the component unless the style is reused globally or must target a third-party library.

### Color Tokens

Use theme tokens instead of hard-coded colors:

| Intent | Preferred classes |
|---|---|
| Page background | `bg-base-100` |
| Subtle surface | `bg-base-200` |
| Stronger surface or divider area | `bg-base-300` |
| Default text | `text-base-content` |
| Primary action | `btn-primary`, `bg-primary`, `text-primary`, `border-primary` |
| Text on primary action | `text-primary-content` |
| Secondary action or emphasis | `btn-secondary`, `bg-secondary`, `text-secondary` |
| Neutral header or shell area | `bg-neutral`, `text-neutral-content` |
| Borders and separators | `border-base-300/50`, `divide-base-300/50` |
| Success state | `btn-success`, `text-success`, `bg-success` |
| Warning state | `btn-warning`, `text-warning`, `bg-warning` |
| Error or destructive state | `btn-error`, `text-error`, `bg-error` |
| Informational state | `btn-info`, `text-info`, `bg-info` |

Hard-coded colors such as `text-black`, `text-white`, hex values, RGB values, and arbitrary color utilities should be rare. They are acceptable only when the element must intentionally ignore organization themes, such as a brand asset, uploaded logo container, chart palette, or third-party override.

When a custom color is needed across the app, add a named CSS variable to every relevant DaisyUI theme block in `globals.css` and use that variable through a class or component abstraction. Do not add a one-off color to a single component and then repeat it elsewhere.

### Theme-Safe Component Rules

When adding or changing UI:

- Test in light and dark mode.
- Check at least the default organization theme and one non-default organization theme.
- Use `base`, `primary`, `secondary`, `accent`, `neutral`, `info`, `success`, `warning`, and `error` tokens according to intent.
- Keep custom CSS based on active theme variables where possible.
- Do not assume `default` is the only visual surface.
- Add new theme variables to every light and dark variant if the variable is required globally.
- Keep contrast readable for normal text, disabled text, table rows, form controls, badges, and buttons.

### Styling Specific UI Patterns

Use the same visual language throughout the app:

- Use `btn-primary` for the main action in a workflow.
- Use neutral or ghost buttons for secondary actions.
- Use `btn-error` or `text-error` for destructive actions.
- Use `bg-base-100` for page-level surfaces and `bg-base-200` or `bg-base-300` for nested panels, table headers, filters, and subtle groupings.
- Use softer base borders such as `border-base-300/50` and `divide-base-300/50` for routine borders and separators.
- Use stronger borders such as `border-base-300`, `border-primary`, or `border-error` only when the border communicates structure, selection, focus, or state.
- Use `rounded-box` or existing DaisyUI radius tokens instead of hard-coded radius values unless matching a specific shared component.
- Use spacing utilities consistently, especially `gap-*`, `p-*`, and `space-y-*`, rather than ad hoc margins between every child.

If a component needs special styling because it is a graph, chart, code-like panel, or third-party widget, isolate that styling behind a named component or a small global override and document why token classes are not enough.

## Layout and Responsive Behavior

DeepLynx is an operational application. Favor dense, scannable, work-focused layouts over marketing-style presentation.

Use these patterns:

- Keep primary actions close to the workflow they affect.
- Use responsive flex/grid layouts with explicit gaps.
- Use `min-w-0`, `truncate`, and `max-w-*` for organization names, project names, filenames, and table controls that may contain long text.
- Use fixed or bounded dimensions for icon buttons, avatars, table controls, and toolbars so loading states and labels do not shift the layout.
- Keep mobile navigation usable through the existing `LayoutShell` and side menu patterns.
- Preserve visible focus states and keyboard reachability for interactive controls.

Do not nest cards inside cards unless the inner card is a repeated item or a modal body that needs its own frame.

## Components and Interaction Patterns

Use the established control vocabulary:

- Buttons: DaisyUI `btn` variants with icons from `@heroicons/react` when the action benefits from an icon.
- Modals: native `dialog` with DaisyUI `modal` and `modal-box`.
- Tables: reuse `GenericTable` or feature-specific table components before building a new table.
- Search: reuse `SearchInput`, `SearchBar`, or `AdvancedSearchBar` where appropriate.
- Pagination: reuse existing pagination controls or match the `join-item btn` pattern.
- Loading: use `loading`, skeleton components, or route-level `loading.tsx` files.
- Empty states: show a concise explanation and the next available action.
- Confirmation: use existing modal patterns for destructive or irreversible actions.

Buttons should declare `type="button"` unless they intentionally submit a form. Icon-only buttons must have an accessible name with `aria-label` or visible text.

## Forms and Validation

Forms should be predictable and resilient:

- Use controlled inputs when the component needs validation, conditional UI, or submit-time payload construction.
- Keep labels, placeholders, and button text clear and translatable.
- Disable submit actions while a request is in flight.
- Show errors near the affected field when possible.
- Use `zod` schemas for shared or non-trivial validation.
- Keep payload construction close to the service call so route IDs and context values are easy to audit.

Do not rely only on placeholders when a form is complex or the user may need persistent field meaning after typing.

## Translations and Copy

Use `useLanguage` and the shared translations where text is already represented in `src/app/lib/translations.ts`.

When adding user-facing copy:

- Prefer short, direct labels.
- Add translation entries for repeated or navigational text.
- Keep table headers, actions, modal titles, and status text consistent with nearby features.
- Avoid embedding long explanatory text inside dense workflow surfaces.

## Errors, Toasts, and Loading States

Every async user action should have an intentional result state:

- Show a loading spinner, disabled action, skeleton, or route loading state while work is pending.
- Report success through an existing toast or local confirmation when the outcome is not obvious.
- Parse and display client-safe API errors with existing error helpers where available.
- Log unexpected client errors only when it helps debugging; do not expose raw stack traces to users.
- Keep destructive failures recoverable by preserving the user's draft state when practical.

## Accessibility

Use semantic HTML and accessible component behavior:

- Prefer real `button`, `a`, `input`, `select`, `textarea`, `table`, and `dialog` elements.
- Use `Link` from `next/link` for navigation.
- Give icon-only controls an accessible name.
- Keep keyboard navigation working for dropdowns, modals, tabs, and menus.
- Do not remove focus outlines unless replacing them with an equally visible focus style.
- Associate labels with inputs for non-trivial forms.
- Ensure theme colors maintain readable contrast in light and dark variants.

## TypeScript

Keep TypeScript useful without overcomplicating components:

- Type component props with `type` or `interface`.
- Reuse DTO types from the existing frontend type folders when available.
- Avoid `any`; use `unknown` at boundaries and narrow it.
- Keep generics for reusable components such as tables, selectors, and hooks.
- Prefer derived types over duplicate hand-written shapes when the source type is local and stable.
- Keep optional values explicit with `null` or `undefined` according to the API contract being consumed.

## Testing

Frontend tests live in `deeplynx.UI/tests` and use Playwright.

Add or update tests when changing:

- Login, organization selection, or route guards.
- Project, record, upload, graph, management, settings, or RBAC flows.
- Modals, destructive actions, or multi-step workflows.
- Client/service behavior that affects navigation or persisted UI state.
- Bug fixes that can regress through normal user interaction.

Prefer user-visible locators:

- `getByRole` for buttons, links, dialogs, and form controls.
- Labels and accessible names for fields.
- Stable `data-*` attributes only for app-specific hooks such as tours or complex controls.

Run targeted tests during development and the broader suite before submitting significant UI changes:

```bash
cd deeplynx.UI
npm run test
```

## Local Development Commands

From `deeplynx.UI`:

```bash
npm run dev
npm run build
npm run lint
npm run test
```

Use `npm run all` when you need both the UI and docs app running together.

## Pull Request Checklist

Before submitting frontend changes:

- The route or component follows the existing App Router organization.
- Server and client component boundaries are intentional.
- API calls go through the correct service module.
- New UI uses DaisyUI/Tailwind theme tokens and works in light and dark mode.
- Long names, filenames, and table content do not break the layout.
- Loading, empty, success, and error states are handled.
- User-facing copy is consistent and translated where appropriate.
- Keyboard and screen reader behavior are not regressed.
- Relevant Playwright tests are added or updated.
- `npm run build`, `npm run lint`, and relevant Playwright tests have been run when practical.
