# DeepLynx UI — E2E Testing Guide

This project uses [Playwright](https://playwright.dev) with a custom fixture layer that handles authentication, org/project scoping, and role/permission provisioning for you. Instead of logging in manually or hand-rolling test data, you declare **who** a test runs as, and the framework takes care of the rest.

## Table of Contents

- DeepLynx UI — E2E Testing Guide
  - [Getting Started](#getting-started)
  - [How it fits together](#how-it-fits-together)
  - [Test directory structure \& the UAT list](#test-directory-structure--the-uat-list)
  - [Configuring orgs \& projects](#configuring-orgs--projects)
  - [Configuring roles \& permissions](#configuring-roles--permissions)
    - [Built-in roles](#built-in-roles)
    - [Custom roles](#custom-roles)
    - [Defining a test-local custom role](#defining-a-test-local-custom-role)
  - [Configuring test accounts](#configuring-test-accounts)
  - [The setup project](#the-setup-project)
  - [Using the fixtures in a test](#using-the-fixtures-in-a-test)
  - [Testing workflows against multiple accounts](#testing-workflows-against-multiple-accounts)

---

## Getting Started

Before running any tests, ensure authentication is enabled.</br>
```dotenv
  NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION=false
  DISABLE_BACKEND_AUTHENTICATION=false
```
*Do this in the root .env and the deeplynx.UI/.env*

Next, ensure that a test account with system admin priviges has it's api key and secret in the deeplynx.UI/.env </br>
The framework needs a bootstrap system-admin identity it can use to provision everything else (orgs, projects, roles, and every other test account). This identity is supplied via two env vars in the `deeplynx.UI` env file:

```dotenv
TEST_SYSADMIN_API_KEY=
TEST_SYSADMIN_SECRET=
```

`setup.ts` uses these to authenticate as `sysAdmin` and provision everything else declared in `deeplynx-config.ts` — so tests won't run without them.

### 1. Create a test account

Hit the create test account endpoint, found in the Scalar docs under:

**Administration → Test Accounts → Create Test Account**

This creates a backend user you'll promote to sysAdmin in the next step.

### 2. Grant it SysAdmin rights

Using the user ID from step 1, hit:

**Administration → User → Grant or Remove System Admin Rights**

This elevates the test account so it has unrestricted access — required since it will be provisioning orgs, projects, roles, and other accounts on behalf of every test.

### 3. Generate an API key and secret

Still under Test Accounts, hit:

**Administration → Test Accounts → Generate API Key and Secret**

for the account you just promoted. Take the returned key/secret pair and add them to your `deeplynx.UI` env file as `TEST_SYSADMIN_API_KEY` and `TEST_SYSADMIN_SECRET`.

### 4. Run the tests

Tests live in `deeplynx.UI/tests`, organized by page. Before running anything, make sure you're in the `deeplynx.UI` directory and have a backend running in a separate terminal via `dotnet run dev`.

**Run all tests**
```bash
npx playwright test
# or
npm run test
```

**Run in UI mode** (step through tests visually)
```bash
npx playwright test --ui
```

**Run in headed mode**
```bash
npx playwright test --headed
```

**Run a specific file**
```bash
npx playwright test tests/organization.spec.ts
```

**Run a specific test by name** (`-g` with a partial name matches all related tests)
```bash
npx playwright test -g "user is automatically assigned an organization on startup"
```

**View the HTML report after a run**
```bash
npx playwright show-report
```

**Stop a running test run**

`Ctrl+C` in the terminal running the command.

---


## How it fits together

| File | Responsibility |
|---|---|
| `deeplynx-config.ts` | Declares orgs, projects, roles, and test accounts. Also has small UI helpers for setting org/project session state. |
| `setup.ts` | Runs once before any test worker starts. Creates/finds every org, project, role, and account declared in `deeplynx-config.ts` against the real backend, and caches the results to disk. |
| `deeplynx-fixtures.ts` | Exposes the `test`/`expect` you import in spec files, along with the `actAs`, `actingUser`, `actingOrg`, `actingProject`, `page`, and `request` fixtures. |

You should almost never need to touch the backend by hand — declare what you need in `deeplynx-config.ts`, and the setup project provisions it.

---

## Test directory structure & the UAT list

  Test directories are intentionally structured to mirror the sections of the **UAT (User Acceptance Testing) list**. If the UAT list has a "Site Administration" section, the corresponding specs live under `tests/site-administration/`, and so on. When adding a new spec, find (or create) the folder that matches its UAT section rather than grouping by feature/component name.

---

## Configuring orgs & projects

Orgs and projects are just named records the setup project will create (or reuse, if they already exist) in the backend:

```ts
// deeplynx-config.ts
export const ORGS = {
  orgA: { name: 'PW Org A' },
  orgB: { name: 'PW Org B' },
} as const satisfies Record<string, TestOrg>;

export const PROJECTS = {
  projectX: { name: 'PW Project X', org: ORGS.orgA },
} as const satisfies Record<string, TestProject>;
```

Add a new entry here when a test needs an org or project that doesn't already exist. The setup project resolves these to real backend IDs and caches them in `playwright/.auth/scopeCache.json`, keyed by name — so re-running setup won't create duplicates.

---

## Configuring roles & permissions

### Built-in roles

`Roles.user` refers to the default, built-in "User" role that already exists on every org in the backend. It carries whatever baseline permissions that role has been configured with server-side — the framework doesn't create or modify it.

### Custom roles

For anything more specific, use `defineRole()`. It accepts either:

- `'all'` — every `PermissionResource` with every `PermissionAction`, or
- a map of `PermissionResource` → `PermissionAction[]` (you can also pass `PermissionAction.All` as shorthand for `[Read, Write, Update]` on that resource)

```ts
export const ROLES = {
  allPermissions: defineRole('all'),
  reportsViewer: defineRole({
    [PermissionResource.Insight]: [PermissionAction.Read],
    [PermissionResource.RecordCollection]: [PermissionAction.Read],
  }),
} as const satisfies Record<string, CustomRole>;
```

Two things worth knowing about `defineRole()`:

1. **It doesn't hit the backend.** It just builds a `CustomRole` object (`{ name, permissions }`). The actual "create the role, attach permission IDs" work happens later in `setup.ts`.
2. **The role name is a hash of its permission set**, e.g. `PW Custom Role a1b2c3d4e5`. This means two calls to `defineRole()` with the same permissions always produce the same role name — so the setup project will find and reuse the existing role instead of creating a new one every run. If you tweak the permissions, you effectively get a "new" role (new hash), which is intentional — it keeps roles and their permission sets from drifting apart silently.

### Defining a test-local custom role

Most custom roles only matter to a single spec. Rather than growing `deeplynx-config.ts` with roles nobody else uses, define the role (and its account) **in the spec file**, export it, and import it back into the config so it still gets provisioned by the setup project:

```ts
// tests/site-administration/admin-setting-options.spec.ts
import { test, expect } from '../../deeplynx-fixtures';
import {
  defineRole, defineTestAccount, ORGS, PROJECTS,
  PermissionResource, PermissionAction,
} from '../../deeplynx-config';

// Local to this spec — only exported so the config can pick it up for provisioning.
export const settingsAuditorRole = defineRole({
  [PermissionResource.Organization]: [PermissionAction.Read],
  [PermissionResource.User]: [PermissionAction.Read],
});

export const settingsAuditorUser = defineTestAccount(
  { role: settingsAuditorRole, org: ORGS.orgA, project: PROJECTS.projectX },
  'settingsAuditorUser',
);

test.use({ actingUser: settingsAuditorUser });

test('auditor can view but not edit admin settings', async ({ page }) => {
  await expect(page.getByRole('heading', { name: 'Admin Settings' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Save' })).toBeHidden();
});
```

Then, back in `deeplynx-config.ts`, just add the account to `TEST_ACCOUNTS` so `setup.ts` provisions it:

```ts
// deeplynx-config.ts
import { settingsAuditorUser } from './tests/site-administration/admin-setting-options.spec';

export const TEST_ACCOUNTS: TestAccount[] = [
  sysAdmin,
  orgAdminA,
  orgAdminB,
  projectAdminX,
  standardUserX,
  fullPermissionUserX,
  settingsAuditorUser, // defined in admin-setting-options.spec.ts, imported here for provisioning
];
```

The config file only gains a single import + array entry per test-local account — the actual permission definition stays next to the test that cares about it.

---

## Configuring test accounts

Accounts are built with one of the helper functions, all of which produce a `TestAccount`:

| Helper | Produces |
|---|---|
| `defineSysAdmin(name)` | A system administrator (not tied to any org). |
| `defineOrgAdmin(org, name)` | An admin of a specific org. |
| `defineProjectAdmin(project, name)` | An admin of a specific project (implies its org). |
| `defineTestAccount(provision, name)` | A general-purpose account — org/project membership with a specific `role`, or org/project admin flags. |

```ts
export const standardUserX = defineTestAccount(
  { role: Roles.user, org: ORGS.orgA, project: PROJECTS.projectX },
  'standardUserX',
);

export const fullPermissionUserX = defineTestAccount(
  { role: ROLES.allPermissions, org: ORGS.orgA, project: PROJECTS.projectX },
  'fullPermissionUserX',
);
```

A couple of validation rules enforced during provisioning (see `assignRole` in `setup.ts`) are worth keeping in mind when defining a `provision`:

- `role` requires `project` — roles are granted via project membership, not at the org level.
- `role` and `isProjectAdmin` are mutually exclusive — project admins get access via admin status, not a permission-bearing role.

Every account you define must be added to the `TEST_ACCOUNTS` array to actually get provisioned:

```ts
export const TEST_ACCOUNTS: TestAccount[] = [
  sysAdmin,
  orgAdminA,
  orgAdminB,
  projectAdminX,
  standardUserX,
  fullPermissionUserX,
  // Add new accounts here.
];
```

---

## The setup project

`setup.ts` runs once, before any browser test starts (`chromium`/`firefox`/`webkit` all declare it as a `dependencies: ['setup']` project). For every account in `TEST_ACCOUNTS` it will, as needed:

1. Create or find the account's org and project.
2. Create or find any custom role, resolve its permission names to backend permission IDs, and attach them.
3. Create (or reuse cached) API credentials for the account and add it to the relevant org/project with the right role or admin flags.
4. Log in and save a Playwright storage state to `playwright/.auth/<accountName>.json`.

Results are cached on disk (`scopeCache.json`, `roleCacheFile.json`, `testUserCache.json`) so re-runs are fast and idempotent — you only pay the provisioning cost once per new/changed account, org, project, or role.

If you add a new org, project, role, or account, run just the setup project to provision it before running specs:

```bash
npx playwright test --project=setup
```

(A normal `npx playwright test` run also triggers this automatically via the `dependencies` config.)

---

## Using the fixtures in a test

Import `test`/`expect` from the fixtures file (not directly from `@playwright/test`), and declare `actingUser` before using `page` or `request`:

```ts
import { test, expect } from '../../deeplynx-fixtures';
import { standardUserX } from '../../deeplynx-config';

test.use({ actingUser: standardUserX });

test('standard user sees the project dashboard', async ({ page }) => {
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
});
```

What you get:

- **`page`** — already logged in as `actingUser`, with the correct org/project session set, and navigated to that scope's landing page.
- **`request`** — an `APIRequestContext` already authenticated as `actingUser`, for hitting the backend directly.
- **`actingOrg` / `actingProject`** — optional overrides for accounts that aren't tied to a single org/project (e.g. `sysAdmin`), or when you want to test an account in a scope other than its default provision:

```ts
test.use({ actingUser: orgAdminA, actingProject: PROJECTS.projectX });
```

- **`actAs`** — the lower-level building block `page` is built on. Use it directly when you need more than one logged-in `Page` in a single test (e.g. simulating two users interacting):

```ts
test('org admin can revoke access mid-session', async ({ actAs }) => {
  const adminPage = await actAs(orgAdminA);
  const userPage = await actAs(standardUserX);
  // ... drive both pages within the same test
});
```

---

## Testing workflows against multiple accounts

A common need: check that the same page/action behaves differently (or the same) across several roles. Pair up the accounts you want to test with their expected outcome, and loop over that list using `test.describe` + `test.use` per account — this keeps each account's run as its own reported test, with its own pass/fail:

```ts
// tests/site-administration/admin-setting-options.spec.ts
import { test, expect } from '../../deeplynx-fixtures';
import { sysAdmin, orgAdminA, standardUserX } from '../../deeplynx-config';
import type { TestAccount } from '../../deeplynx-config';

interface AccessExpectation {
  account: TestAccount;
  canEditSettings: boolean;
}

const cases: AccessExpectation[] = [
  { account: sysAdmin, canEditSettings: true },
  { account: orgAdminA, canEditSettings: true },
  { account: standardUserX, canEditSettings: false },
];

for (const { account, canEditSettings } of cases) {
  test.describe(`as ${account.name}`, () => {
    test.use({ actingUser: account });

    test('admin settings edit access matches expectation', async ({ page }) => {
      const saveButton = page.getByRole('button', { name: 'Save' });
      if (canEditSettings) {
        await expect(saveButton).toBeVisible();
      } else {
        await expect(saveButton).toBeHidden();
      }
    });
  });
}
```

This produces one reported test per account (e.g. `as sysAdmin > admin settings edit access matches expectation`), so a failure for one account doesn't hide failures for the others.

**Alternative — single test, sequential accounts:** if you'd rather keep it as one test (e.g. the check is cheap and you don't need per-account reporting), drive multiple accounts with `actAs` directly instead of `test.use`, since `test.use` can't be called mid-test:

```ts
import { test, expect, gotoScope } from '../../deeplynx-fixtures';
import { sysAdmin, orgAdminA, standardUserX, ORGS, PROJECTS } from '../../deeplynx-config';

const cases = [
  { account: sysAdmin, canEditSettings: true },
  { account: orgAdminA, canEditSettings: true },
  { account: standardUserX, canEditSettings: false },
];

test('admin settings edit access across roles', async ({ actAs }) => {
  for (const { account, canEditSettings } of cases) {
    const page = await actAs(account);
    await gotoScope(page, ORGS.orgA, PROJECTS.projectX);

    const saveButton = page.getByRole('button', { name: 'Save' });
    if (canEditSettings) {
      await expect(saveButton).toBeVisible();
    } else {
      await expect(saveButton).toBeHidden();
    }
  }
});
```

Prefer the `test.describe` loop for anything you'd want isolated pass/fail results on in CI; reach for the `actAs` loop when the accounts genuinely belong to one continuous scenario.