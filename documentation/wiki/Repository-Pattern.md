DeepLynx follows the Repository design pattern as a part of its data translation layer. More information on the pattern can be found [here](https://martinfowler.com/eaaCatalog/repository.html).

All Repositories in our code consist of methods for persisting and retrieving domain objects to storage as well as managing things like validations. Users should interface with repositories when possible and not mappers. Repositories contain additional logic such as validation or transformation prior to storage or returning, and are used to contain and chain together query logic. Mappers are used for direct interaction with the database. For more information on mappers, click [here](Data-Mapper-Pattern).

All Repositories are contained in `src->data_access_layer->repositories` and are sorted and organized in a matter which represents the system as a whole. This organization is reflected throughout the rest of the application and is as follows
- `access_management`: regarding users, api keys, and interactions with the rest of the system
- `data_warehouse`: this is the bulk of the system and is further broken down into `data`, `etl`, `import`, `export` and `ontology`. You will find the majority of repositories here
- `event_system`: repositories representing the internal and external facing event system of DeepLynx
- `task_runner`: repositories associated with storing task records in DeepLynx

Repositories may be built directly atop only one data mapper, or may be the composition of many mappers. Repository methods are called most commonly as the result of a hit to an API endpoint. For more information about the DeepLynx API and the HTTP Server, click [here](HTTP-Server).

Most repositories in DeepLynx share the same basic structure, containing the core methods `delete`, `save` (for both creation and updating), and `findByID` for retrieval of data structures. Repositories may also contain methods for bulk operations and validation, count and list methods, and methods for querying the properties of the associated domain object.

To see how Repositories fit together with Mappers, Migrations and Domain Objects, click [here](Adding-New-Data-Structures-and-Storage).