# DeepLynx Nexus
## Feature Requests and Bug Reporting
*  INL Users & Developers: Please use INL's Jira instance to submit both new feature requests and to report bugs
*  External Users & Developers: Please use the GitHub Issues page of this repository for both feature requests and bug reports.
## Prerequisites
1. Postgres download:
    * Download [PostgreSQL](https://www.postgresql.org/) natively, OR
    * Download [Docker](https://docs.docker.com/engine/install/)

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
PostgreSQL databse connection credentials are set in `docker-compose.yaml` and the default values are set to connect to the composed database automatically for rapid deployment.

## Local Developmental Setup

### First Step
* Environment variables:
   * Either rename the file `.env_sample` in the root directory to `.env`, or:
      1. Create a new file named `.env` in the root directory.
      2. Copy the contents of `.env_sample` to `.env`.

Once you have a `.env` file, be sure to periodically check `.env_sample` for updates as they won't automatically apply to your `.env`.

1. PostgreSQL Setup:
    * Native Install:
        * Install and launch PostgreSQL.
        * Create a PostgreSQL server.
    * Postgres on Docker:
        * Run the following command: `docker run --name DeepLynx -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=deeplynx -d -p 5432:5432 postgres`
    * Add appropriate credentials for the newly created PostgreSQL server to their respective variables in `.env`. For example: 
        * `POSTGRES_DB_HOST=localhost`
        * `POSTGRES_PASSWORD=your_password`

2. .NET SDK Setup:
    * Install the .NET SDK version 9.0.

3. (Optional) Entity Framework CLI to create and run migrations manually:

    * Install the .NET Entity Framework CLI tool globally:
        * `dotnet tool install --global dotnet-ef`

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
* Ensure that your PostgreSQL server is running before attempting to launch the app, update the database, or create migrations.
