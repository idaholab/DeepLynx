# April 2021 Refactor of DeepLynx

In March of 2021, the internal development team of DeepLynx made the decision to completely refactor and reorganize the existing code base. This move was taken to accomplish the following goals.

1. **Adopt an obvious and easy to understand code organization structure.** Internal teams decided that the current organization did not lend itself to easy understanding of what the actual code was accomplishing. This made it difficult for new users of DeepLynx to begin or continue development. This also made diagramming DeepLynx difficult and hampered the ability of the team to describe the system quickly and accurately to interested parties.
2. **Optimize database interaction.** The current iteration of DeepLynx combined many domain logic operations (such as validation) and database interaction into single functions. This led to confusion and complications as one or other part of the function needed to be updated or changed. To streamline the process DeepLynx adopted a couple of design patterns, mainly Domain Objects, Table Mappers, and Repositories. This allowed us to separate the database interaction from most domain logic, enabling those functions to evolve separately. Once that separate was completed, the team was able to optimize most database interactions - even down to the literal SQL being sent to the database. We were able to drastically reduce statements being made against the database and optimize our interaction across all aspects of DeepLynx.
3. **Build foundation for future features.** DeepLynx&#39;s development roadmap contains some very complicated upcoming features. The best example of this being the database and ontology versioning feature. The current structure would needlessly complicate the implementation of this and other similar features and so had to be redone. By adopting the design patterns discussed in the point above, DeepLynx is now able to implement these upcoming features more easily – and do so by using industry standard design patterns and practice.
4. **Maintain an environment in which multiple developers can work in parallel.** This refactor has allowed us to separate DeepLynx&#39;s core features more clearly. By doing so we feel we have created an environment that can more readily sustain developers working on different features in parallel. Previously, the interconnectedness of domain logic and database statements made it difficult to work on each piece separately, even if they were completely different areas.

This refactor was completed successfully in April of 2021 and has, so far, accomplished all stated goals. The refactor took roughly 3 weeks and the full-time efforts of the lead developer on the DeepLynx project, John Darrington. Any questions about this document, the refactor, or DeepLynx in general can be addressed by sending an email either to Christopher Ritter ([Christopher.ritter@inl.gov](mailto:Christopher.ritter@inl.gov)) or John Darrington ([john.darrington@inl.gov](mailto:john.darrington@inl.gov)).

The rest of this document consists of short in-depth looks at each aspect of DeepLynx that was modified, added, or removed. Each step was taken in accordance with the stated goals above but are not necessarily organized here in the order stated above. Not all changes have been captured in this document, either because they are small enough to not merit statement, do not align with the goals, or were simply forgotten. If you have any questions or comments concerning the document, please reach out.

# Refactor Details

## Database

- Most data types had a corresponding class called `{type name}\_storage.ts`. The suffix of `storage` was completely dropped from these files and replaced with `mapper`. This was done to bring it in line with the [Data Mapper](https://martinfowler.com/eaaCatalog/dataMapper.html) design pattern.
- The mapper parent class (`src/data_access_layer/mappers/mapper.ts`) was refactored in the following ways –
  - Functions for initiating, terminating, and rolling back data transactions simplified, and better error handling added.
  - Functions which ran SQL statements modified to include optional patterns, some of which utilized the `class-transformer` library to return a user specified Class. While still generic functions and still able to return simple user-defined objects – this changed allowed us to adopt the return of Classes vs. Objects more quickly when dealing with return data from the database.
  - Increased error log generation to facilitate future debugging.
- Most create and update statements were completely rewritten to take advantage of the `RETURNING` keyword in PostgreSQL. This allows us to use a single statement with creating/updating records and return the data after it is modified in its entirety to the user. Previously, we would either backfill a data object with user provided data after we were sure the statement ran successfully or made an additional database transaction to retrieve that information. This change allows us to display the data more accurately to the user and to half the amount of database transactions when working with create/update statements. While this cements our dependency on PostgreSQL – previous decisions had already been made that made that dependency acceptable and necessary.
- Create/Update statements rewritten to allow bulk record creation/modification.
- Domain logic such as validation, transformation, and sanitization has been moved out of these mapping classes and into the domain object for a given data type (discussed later in the document)
- Majority of listing or filtering database calls moved out of data mappers and into [Repositories](https://martinfowler.com/eaaCatalog/repository.html)
- Migration scripts reorganized and moved into the `data_access_layer` folder. Migrate functionality refactored to include more error logging and graceful failure handling.
- Implementation of the npm package [pg-format](https://www.npmjs.com/package/pg-format) – used to sanitize user provided SQL input and build dynamic queries. Mapper layer was updated to accommodate these newly formatted queries as well as the previous iteration of a query object.



## Domain Objects

Data types were previously declared as static objects using the [io-ts](https://www.npmjs.com/package/io-ts) library. While this allowed us to have a quick validation system out of the box, it significantly tied our hands when extending types or performing more rigorous validation. To solve these problems all io-ts types were removed and data types were declared as classes instead. This was as significant undertaking as every function that accepted or returned these types had to be modified to instead interact with the classes. As discussed previously, the database operations had to be modified to return classes instead of objects as well. The following occurred –

- All io-ts types converted to classes with properties which generally mirrored their database schema counterpart.
- Domain objects with a parent-child relationship were modeled more accurately in these newly created classes. This allowed us to understand, modify, and track a user's interaction with the ontology more easily.
- Validation was implemented using the [class-validator](https://www.npmjs.com/package/class-validator) library. This allowed use to use decorators above a class's property declarations to direct a validation function on how to validate any given class. This change also allowed use to create more rigorous and complex validation functions, such as verifying that a relationship pair in the ontology was valid prior to database insertion.
- Domain logic not related to database interaction was moved into the proper class. This allowed us to subsume many functions that had been created outside the storage layer to separate domain logic from server/database interactions. In almost all cases we were able to completely remove these files and functions and include them automatically as part of the domain object.
- User management benefited greatly from this move, with the OAuth operations being spread across more specifically named classes and organization much more apparent to the end user. The reorganization of user management into domain objects also allowed us to streamline the OAuth portion of DeepLynx and make it easier to debug.
- Conversion to classes also made it possible for the team to implement change tracking in the future, allowing the domain object itself to maintain a record of changes made to it. This will become necessary as we implement database versioning.
- Reorganized the folder structure completely, as the use of domain objects allows us to arbitrarily organize files and structure as we see fit.



## Repositories

With the adoption of domain objects and the data mapper pattern we were then able to implement the repository pattern as defined by many industry sources. Note that this is not a perfect implementation as both language and end goal presented obstacles to that achievement. The discussion of how it was implemented is not in the scope of this document, please contact the development team if you wish to learn more.

- A generic repository base class and interface was created which contains basic database listing and filtering functionality and other common operations. All repositories should use this generic class as its parent and implement the repository interface. This allows us to achieve unification across the project as well as enforce good development procedure.
- Nearly all domain objects had a corresponding repository created for it. This repository can be viewed as the bridge between the domain object and data mapper classes and contains functions that must be completed prior to manipulation of a domain object in the database.
- Each repository class implemented its interface, which included a `save` function. This function is how the user would go about creating or updating a domain object in the database. These save functions oversaw performing all necessary operations prior to storage, such as validation or transformation. Certain repositories also included a `bulkSave` function, allowing users to manipulate large number of records easily.
- Certain domain object's repositories also implemented methods for saving/creating their children in the database. Examples of this include the Metatype class with its Metatype Property children.
- Relevant repositories also implemented database listing and filtering functions for its domain class. These functions allow the developer to chain a complicated set of queries together easily and without having to write SQL for each given variation. Almost all previous listing SQL statements were subsumed into this query building system.
- Each repository had a full suite of unit tests created for it.



## Data Processing and Data Sources

- Data processing loop completely rewritten to use repositories over the data mappers directly. In many cases this simplified the function being refactored as validation or transformation that occurred as part of data processing now was automatically done by the repository.
- The data source repository does not actually deal with the data source domain object. Instead, it deals with a newly created data source interface. Using an interface has allowed use to create different implementations of data source, with the only implementations existing at this time being standard and http. Using this pattern, we were able to move the data processing functions from a standalone file into a method on the data source itself. This has allowed us to simplify the processing loop and tie it more directly to the type of data source that is providing the data – allowing for future iterations to build custom data processing loops depending on the data source it is being implemented for.
- Standalone child process rewritten to use repositories and interfaces. This process will now start independently of the main DeepLynx processing thread and manage the data processing loops for any active data sources. This process will automatically start and stop data source processing depending on data source status.
- Standalone child process created for managing http data source's long-term polling operations. This allows DeepLynx to receive data from http data sources without interrupting the main thread.

## Data Exporting

- Repository and domain object written following a similar pattern to the data source explained above. A Data Exporter interface was created, allowing us to create and modify data exporters based on type while being able to reuse basic functionality. Currently only the `gremlin` data exporter type exists.
- Standalone child process rewritten to utilize domain objects and repositories.

## HTTP Server

- All functions specific to the [express.js](https://expressjs.com/) http server now all reside inside the `http_server` folder. Organization of this folder mirrors the organization of the domain objects themselves and the hope is that this will lead to easy understanding of where to look when modifying the http server's functionality.
- User management functions and middleware completely refactored. Functions either moved into the set of user management domain objects, converted to middleware functions, or were removed. This includes the OAuth identity provider service and interface.
- Various configuration options moved from statically declared to injectable via the environment.
- Middleware created for retrieval of most major domain objects. This allowed us to streamline the actual route handler functions as in many cases we would first have to fetch and verify a domain object existed before operating on it. Now in many cases the inclusion of proper middleware ensures that the developer no longer needs to insert those checks in every route handler themselves.
- js data object type declarations extended to allow piggybacking of our own data into the route handler functions. This has allowed use to greatly reduce the size of route handler functions by unifying common operations further up the server middleware chain.
- All route handler functions updated to use repositories over using mappers directly. Remember that the mappers had all domain logic and validation pull out of them and put into the repositories, meaning that if the routes had not been updated the user of the server would be communicating directly with the database without any kind of sanitization or validation.
- Assets and views moved into the HTTP server folder and are now separated from logic.

## Organization

- Literally every file was either renamed, moved, combined with another file, or sometimes all three. New naming scheme attempts to follow common industry practices, and directly represents the major design patterns implemented as part of this refactor (domain objects, data mappers, and repositories).
- Folder structure was simplified, separating the http server, data access layer, and tests from their corresponding domain objects. Generally, a file or set of files is organized using the following method –
  - Data Warehouse
    - Ontology
    - Import
    - Export
    - ETL
    - Data
  - User Management
  - Event System
- All wiki pages migrated from the repository itself into the VCS system that it resides on (Github, Gitlab).
- Admin web application moved into a top-level folder and renamed.

# Conclusion

As stated previously this is not a comprehensive list of changes this refactor introduced. If you have a question or concern about a change that is not listed here, reach out to the development team, or review the original refactor's [Pull Request](https://github.com/idaholab/Deep-Lynx/pull/171) on Github. This document has covered the general changes this refactor has made and has attempted to explain some of the reasoning behind them. This refactor took a large amount of effort and development time, but we are sure that we are now in a better place for all future DeepLynx development.