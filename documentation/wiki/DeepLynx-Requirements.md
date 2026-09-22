### **Build From Source**
_________
#### **Requirements**

-   [node.js](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) ^16.x
-   [Typescript](https://www.typescriptlang.org/download) ^4.x.x
-   [npm](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm) ^6.x
-   [yarn](https://classic.yarnpkg.com/lang/en/docs/install/) ^3.6.x
-   [Rust](https://www.rust-lang.org/tools/install) ^1.x.x (set to default stable)
-   [Docker](https://docs.docker.com/engine/install/) ^18.x - _optional_ - for ease of use in development


**_Data Source Requirements_**

These requirements are for a native install of PostgreSQL. You can alternatively build the database using docker as found in step 6a of [this guide](Building-DeepLynx).

- **Required** - PostgreSQL ^12.x
- **Required** - `pg-crypto` Postgres extension (automatically included with Postgres > 12 and in the Docker images)
- [TimescaleDB Postgres Extension](https://www.timescale.com/) - needed for raw data retention and time-series data