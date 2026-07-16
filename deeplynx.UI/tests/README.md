# Test Fixtures- used for test account auth
### Custom Playwright fixtures for auth, org/project selection, and navigation.

> [!NOTE]
> The setup that runs before tests requires that the `TEST_SYSADMIN_API_KEY` and `TEST_SYSADMIN_SECRET` are set in the .env  
> To get the key and secret, create a test account with system admin privelges and generate API keys

Import from `./fixtures`, not `@playwright/test`:

```typescript
import { test, expect } from './fixtures';
```

## Fixtures

| Fixture | Type | What it does |
|---|---|---|
| `actingUser` | required option | Account the test runs as. |
| `actingOrg` | conditional option | Org to select. Required only for accounts with no `provision.org` (e.g. `sysAdmin`). Ignored for accounts that already have one. |
| `actingProject` | optional | Project to select. Overrides `provision.project` when set. |
| `page` | overridden | Authenticated, org/project-selected, and confirmed loaded (waits for the header) before your test runs. |
| `actAs` | function | Returns a logged-in `Page` for any account, no org/project selection or readiness wait. Used internally by `page`. |

## Basic usage

```typescript
import { test, expect } from './fixtures';
import { orgAdminA } from './deeplynx-config';

test.describe('org admin dashboard', () => {
  test.use({ actingUser: orgAdminA });

  test('shows the dashboard', async ({ page }) => {
    await expect(page.locator('h1')).toBeVisible();
  });
});
```

`actingUser` is required wherever `page` is used — the fixture throws with a
fix-it message if it's missing.

## actingOrg

- Account has `provision.org` then don't set `actingOrg`; it's ignored anyway.
- Account has no `provision.org` then `actingOrg` is required.

```typescript
test.use({ actingUser: sysAdmin, actingOrg: 'PW Org A' });
```

## actingProject

- Account has `provision.project` → selected automatically.
- No provisioned project, but the test needs one anyway (e.g. checking what
  `sysAdmin`/`orgAdminA` see on a project dashboard) → set `actingProject`.

```typescript
test.use({ actingUser: sysAdmin, actingOrg: 'PW Org A', actingProject: 'PW Project X' });
```

`actingProject` always wins over `provision.project` — unlike `actingOrg`,
which is ignored for provisioned accounts. Reasoning: org membership is real
backend identity for the account; project selection is just a viewport.
`actingProject` doesn't grant real backend membership — don't rely on it for
checks that depend on actual project membership rather than role/privilege.

Accounts are limited to one org and one project each. If you need the same
account in a different org/project, add a new `TestAccount` in
`deeplynx-config` instead.

## Scoping test.use()

Applies to the file/describe block it's called in, and everything nested
after it. Calling it after a describe block closes does nothing for that block.

```typescript
test.use({ actingUser: sysAdmin }); // file-level default

test.describe('org A', () => {
  test.use({ actingOrg: 'PW Org A' }); // merges with the file-level default
  test('...', async ({ page }) => { ... });
});
```

## Acting as multiple users in one test

```typescript
test('owner invites a member', async ({ actAs }) => {
  const ownerPage = await actAs(ownerAccount);
  const memberPage = await actAs(memberAccount);
  // actAs skips org/project selection and the readiness wait —
  // call selectOrganization/selectProject yourself if needed.
});
```

Contexts are tracked and closed automatically.

## Where accounts come from

`TestAccount` definitions live in `./deeplynx-config`. `tests/setup.ts`
provisions and authenticates them once before the suite runs (via Playwright
project `dependencies`). Add new accounts/orgs/projects there.

`provision.role` is `'org_admin' | 'project_admin' | 'user'` — `'user'`
resolves to the backend's org-scoped `DEFAULT_ROLE_NAME` ("User"). Custom
roles aren't supported yet.

> [!NOTE]
> Currently as implemented, test users can't be configured for more than one Organization and one Project

## Errors

| Error | Cause | Fix |
|---|---|---|
| `No actingUser declared...` | `page` used without `actingUser`. | `test.use({ actingUser: <account> })` |
| `"<name>" has no provisioned org...` | No `provision.org`, no `actingOrg`. | `test.use({ actingOrg: '<org>' })` |
| `No project to select for "<name>"...` | No `provision.project`, no `actingProject`. | Provision a project, or pass `actingProject`. |
| `No account is provisioned into org/project "<name>"...` | Name doesn't match any account's `provision.org`/`provision.project`. | Check for typos against `ORGS`/`PROJECTS`. |