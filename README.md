# DeepLynx Nexus

## Feature Requests and Bug Reporting

- INL Users & Developers: Please use INL's Jira instance to submit both new feature requests and to report bugs
- External Users & Developers: Please use the GitHub Issues page of this repository for both feature requests and bug reports.

## Prerequisites

1. Postgres download:
   - Download [PostgreSQL](https://www.postgresql.org/) natively, OR
   - Download [Docker](https://docs.docker.com/engine/install/)

2. .NET SDK: Ensure .NET SDK version 10.0 is installed on your system. Download [.NET 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). You can verify you are using the correct version by running `dotnet --version` in the command line.

## Docker Setup

This application may be run from docker using the following docker command:

```
docker compose up
```

Built containers must always be rebuilt after code changes, including pulled code from GitHub, using the following:

```
docker compose up --build
```

### Running with DeepLynx Insight

The `deeplynx.insight` services are optional and are behind a Docker Compose profile. By default they are excluded from a standard `docker compose up`, so you can run Nexus without them if you don't need the insight functionality.

**Without Insight (default):**
```bash
docker compose up
```

**With Insight:** (Will run everything including Insight)
```bash
docker compose --profile insight up
```


### PostgreSQL Configuration

The Insight services connect to the same `nx-postgres` container as the rest of Nexus, but they use different variable names for the connection settings. Its just a naming difference between the two codebases that we hope to merge eventually into copying the way the rest of Nexus uses the variables

The values need to point at the same database. If you change the credentials, make sure both sets match in `docker-compose.yaml`:

| Setting | Nexus (`server`, `nx-postgres`, `db-version-check`) | Insight (`insight-fastapi`, `insight-rabbitmq-runner`) |
|---|---|---|
| Host | `POSTGRES_DB_HOST` | `PG_HOST` |
| Port | `POSTGRES_PORT` | `PG_PORT` |
| User | `POSTGRES_USER` | `PG_USER` |
| Password | `POSTGRES_PASSWORD` | `PG_PASSWORD` |
| Database | `POSTGRES_DB_NAME` | `PG_DBNAME` |


### Developing Insight

The default env file used by `insight-fastapi` and `insight-rabbitmq-runner` in `docker-compose.yaml` is `.env.production`, which has no model endpoints configured. If you are actively developing Insight and want default model endpoints wired in (which for production is not needed as Nexus will be configured to pass that information to Insight when Insights endpoints are called), swap the `env_file` for those two services in `docker-compose.yaml` to point at one of the development env files instead:

- `./deeplynx.insight/.env.hpc.local` -- for HPC-hosted models via the INL API
- `./deeplynx.insight/.env.ollama.local` -- for local models running via Ollama


```yaml
env_file:
  - ./deeplynx.insight/.env.hpc.local
```

If you are using `.env.hpc.local`, make sure to fill in the auth tokens for the services you want to use as defaults (`LLM_AUTH_TOKEN`, `MM_AUTH_TOKEN`, `EMB_AUTH_TOKEN`). Those are left blank intentionally and the services will not authenticate without them.

## Local Developmental Setup

### First Step

- Environment variables:
  - Either rename the file `.env_sample` in the root directory to `.env`, or:
    1. Create a new file named `.env` in the root directory.
    2. Copy the contents of `.env_sample` to `.env`.

Once you have a `.env` file, be sure to periodically check `.env_sample` for updates as they won't automatically apply to your `.env`.

1. PostgreSQL Setup:
   - Native Install:
     - Install and launch PostgreSQL.
     - Create a PostgreSQL server.
   - Postgres on Docker:
     - Run the following commands:

     ```
     # Build the deeplynx image from the provided Dockerfile
     docker build -t deeplynx-db -f Dockerfiles/database/Dockerfile.local .

     # Run a container from that image
     docker run --name DeepLynx \
     -e POSTGRES_PASSWORD=postgres \
     -e POSTGRES_DB=deeplynx \
     -d \
     -p 5432:5432 \
     deeplynx-db
     ```

   - Add appropriate credentials for the newly created PostgreSQL server to their respective variables in `.env`. For example:
     - `POSTGRES_DB_HOST=localhost`
     - `POSTGRES_PASSWORD=your_password`

2. .NET SDK Setup:
   - Install the .NET SDK version 10.0.

3. (Optional) Entity Framework CLI to create and run migrations manually:
   - Install the .NET Entity Framework CLI tool globally:
     - `dotnet tool install --global dotnet-ef`

## Development

### Load the Database

Migrations should be applied automatically on application startup either locally or within docker and fail gracefully. The most common reason for failure will be either a PostgreSQL database is not running or listening for incoming connections, or incorrect credentials to an intended PostgreSQL database.

1. Navigate to the root folder in your project directory.

2. Run the following command to apply the latest migrations and update the database:

```
dotnet ef database update -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
```

If the above command fails with a `Could not exeucte` or similar message, `dotnet ef` may need to be added to your PATH.  
Please update your path to include the .NET tools directory, similar to: `export PATH="$PATH:/Users/_username_/.dotnet/tools"`

### Create Migration

If you make changes to the datalayer, create a new database migration with a descriptive name. For example, to add a migration for updating the users table, run:

```
dotnet ef migrations add UpdateUsersExample -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
```

See [CONTRIBUTING](./CONTRIBUTING.md) for more details.

### Additional Notes

- Ensure that your PostgreSQL server is running before attempting to launch the app, update the database, or create migrations.

## Working with the `deeplynx.insight` Submodule

Nexus includes `deeplynx.insight` as a Git submodule. If you don't handle submodules correctly you will end up with an empty folder. Follow the steps below depending on your situation.

---

### Fresh Clone

Clone Nexus and initialize the submodule in one step using the `--recurse-submodules` flag:

```bash
git clone --recurse-submodules <nexus-repo-url>
```

---

### Already Cloned Without the Submodule

If you already cloned Nexus and the `deeplynx.insight` folder is empty, run the following from the Nexus root:

```bash
git submodule update --init --recursive
```

This will fetch and check out the correct commit of `deeplynx.insight` that Nexus currently points to.

---

### Keeping Your Submodule Up to Date

When someone else pushes a new submodule pointer (i.e., someone is updating the insight repo) in Nexus, pull the change and then update the submodule:

```bash
git pull
git submodule update --recursive
```

---

### Making Changes Inside `deeplynx.insight`

When you need to modify `deeplynx.insight` itself (not just Nexus), follow this workflow:

**1. Work inside the submodule**

```bash
cd deeplynx.insight

# Make your code changes, then stage and commit them
git add <changed-files>
git commit -m "Your amazing and super descriptive commit message"

# Push your changes to the deeplynx.insight remote
git push
```

**2. Update the submodule pointer in Nexus**

After pushing from inside `deeplynx.insight`, go back to the Nexus root and update with the new commit reference:

```bash
cd ..

# Stage the updated submodule pointer
git add deeplynx.insight

git commit -m "Update deeplynx.insight submodule to latest commit"

# Push the Nexus change so everyone else gets the updated pointer
git push
```

Git tracks submodules as a commit hash, not a branch! When you push changes inside `deeplynx.insight`, Nexus will still point at the old commit until you stage and commit the new hash from the Nexus root.

---

### Running tests with Playwright

## Using bash

Tests are located in deeplynx.UI/tests and organized by page. When running tests, make sure you are in the deeplynx.UI directory

#To run all tests
npx playwright test

#To run tests in UI mode (This allows to step through tests visually)
npx playwright test --ui

#To run tests in headed mode
npx playwright test --headed

#To run a specific file
npx playwright test tests/organization.spec.ts

#To run a specific test by name (-g with partial name will run all related tests)
npx playwright test -g "user is automatically assigned an organization on startup"

#To view the HTML report after the test
npx playwright show-report

#To stop running tests
Ctrl+C

## Using Playwright plug-in

Go to extensions and search Playwright Test for VSCode. Install plug-in. Detailed instructions can be found in the extension's detail page.

A beaker will appear in the left panel - this is Playwright's main testing area
Additional settings can be configured under Playwright>Settings at the bottom

#To run all tests
- Go to Testing (beaker) click the play button by the Test Explorer on the top - cursor needs to be in the testing area to show the buttons, OR
- Right click deeplynx.UI/tests folder in the file explorer and click Run Tests

#To run tests in a specific folder
- Locate the folder in the testing area and click the play button, OR
- Right click the folder in the file explorer and click Run Tests

#To run tests in a specific file
- Locate the file in the testing area and click the play button, OR
- Right click the file in the file explorer and click Run Tests - You can also click the first play button in the file

#To run a specific test by name
- Type test name in filter bar in the testing area and click the play button on desired test(s), OR
- In the file explorer, locate the specific test and click the play button on the left

#To stop a running test
Click the stop button in the Test Explorer - cursor will need to be in the testing area