# DeepLynx Wiki
Sections marked with ! are in progress.
 * [Home](Home)
 * [Documentation Structure](Documentation-Structure)

### Building DeepLynx
#### DeepLynx Overview
 * [How Data Warehouses Work](How-Data-Warehouses-Work)
 * [DeepLynx Ecosystem](Deep-Lynx-Ecosystem)

#### Getting Started
 * [Installing and Running DeepLynx](Installing-and-Running-Deep-Lynx)
 * [Verifying Installation](Verifying-Installation)
 * [Next Steps](Next-Steps)

#### Building From Source
 * [DeepLynx Requirements](DeepLynx-Requirements)
 * [Building DeepLynx](Building-DeepLynx)
 * [Gotchas](Gotchas)

#### Admin Web App
 * [When to Use This Section](When-To-Use-AdminWebApp-Section)
 * [Requirements](Requirements)
 * [Installation](Administration-Web-App-Installation)

------------------------------
### Deploying DeepLynx
 * [Deploying DeepLynx to Kubernetes](On-Prem-Production-Deployment)

------------------------------
### Integrating with DeepLynx
 * [HTTP Authentication Methods](Authentication-Methods)
 * [Generating and Exchanging API Keys for Tokens](Generating-and-Using-API-Keys-Token)
 * [Creating a DeepLynx Enabled OAuth2 App](DeepLynx-Enabled-OAuth-Application)
 * [Authentication with DeepLynx Enabled OAuth2 App](Authenticating-with-DeepLynx-Apps)
------------------------------
### Using DeepLynx
#### Ontology
 * [Creating an Ontology](Creating-an-Ontology)
 * [Creating Relationships and Relationship Pairs](Creating-Relationships-and-Relationship-Pairs)
 * [Ontology Versioning](Ontology-Versioning)
 * [Ontology Inheritance](Ontology-Inheritance)

#### Data Ingestion
 * [Standard Data Source](Create-a-Data-Source)
 * [P6 Data Source](P6-Data-Source)
 * [HTTP Data Source](HTTP-Data-Source)
 * [Web Sockets](Web-sockets)
 * [Custom Data Sources](Custom-Data-Source-Templates)

#### Timeseries Data
 * [Querying Tabular (Timeseries) Data](Querying-Tabular-Data-in-DeepLynx)
 * [Timeseries Quick Start](Timeseries-Quick-Start-Guide)
 * [Timeseries Data Source](Timeseries-Data-Sources)
 * [Timeseries Data Source via API](TimeseriesCreationViaApi)

##### Manual Path
 * [Creating a Node](Using-Standard-Source-To-Manually-Create-Nodes)
 * [Creating an Edge](Creating-an-Edge)
##### Automated Path
 * [Imports](Data-Source-Imports)
 * [Type Mapping](Type-Mapping)
 * [Type Mapping via API](Type-Mapping-Via-API)
##### File/Blob Storage
 * [Uploading Files](Uploading-Files)
 * [Creating Test Data](Creating-Test-Data-Through-DeepLynx)


#### Data Querying
 * [Exporting Data](Exporting-Data)
 * [Querying Data](Querying-Data-With-GraphQL)
 * [Querying Timeseries Data](Timeseries-Data-Sources)
 * [Querying Jazz Data](Querying-Jazz-Data)
 * [Querying Data - Legacy](Querying-Data)
 * [Querying Tabular Data](Querying-Tabular-Data-in-DeepLynx)

#### Event System
 * [Creating and Listening to Events](Creating-and-Listening-to-Events)
 
#### Data Targets
 * [Creating Data Targets](Creating-Data-Targets)

-----------------------------------
### Developing DeepLynx
#### Developer Overview
 * [Common Tools](Common-Tools)
 * [Collaboration-Guidelines](Collaboration-Guidelines)

#### Project Structure and Patterns

 * [Domain Object Pattern](Domain-Object-Pattern)
 * [HTTP Server](HTTP-Server)
 * [Services](Services)
 * [Tests](Tests)

#### Data Access Layer
 * [Migrations](Migrations)
 * [Repository Pattern](Repository-Pattern)
 * [Dynamic SQL with the Repository](Forming-Dynamic-SQL-with-the-Repository)
 * [Data Mapper Pattern](Data-Mapper-Pattern)
 * [Adding New Data Structures and Storage](Adding-New-Data-Structures-and-Storage)

#### Development Process
 * [Testing](Tests)
 * [Creating Test Data](Creating-Test-Data-Through-DeepLynx)

#### Current Proposals
* [Reports Layer](Reports-Layer-Proposal)