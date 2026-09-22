DeepLynx follows the Data Mapper design pattern as a part of its data translation layer. More information on the pattern can be found [here](https://martinfowler.com/eaaCatalog/dataMapper.html).

All Data Mappers in our code extend the Postgres database Mapper class and allows the user to map a data structure to and from the attached database. The Data Mappers are designed to be as simple as possible and should not contain things like validation or transformation of the data prior to storage - those operations should live in a Repository or on the data structure's Domain Object class itself. Listing functions should also be avoided in Data Mapper creation as those are generally covered in the Repository. To learn more about DeepLynx Repositories, click [here](Repository-Pattern).

All Data Mappers are contianed in `src->data_access_layer->mappers` and are sorted and organized in a matter which represents the system as a whole. This organization is reflected through the rest of the application and is as follows
- `access_management`: regarding users, api keys, and interactions with the rest of the system
- `data_warehouse`: this is the bulk of the system and is further broken down into `data`, `etl`, `import`, `export` and `ontology`. You will find the majority of data mappers here
- `event_system`: data mappers representing the internal and external facing event system of DeepLynx
- `task_runner`: data mappers associated with storing task records in DeepLynx

Data Mappers send data received from repositories to the database directly using raw SQL statements. Mappers should not use validation or domain logic. Each mapper uses public methods which are called by the Repository layer, and these public methods execute the appropriate private SQL statement to interact with the database. Mappers may use methods from the default Mapper class to execute these statements. These methods can be used for starting, running, and rolling back translations, running statements singularly or in bulk, and retrieving one to many rows (or a row count) as the result of a query.

To see how Mappers fit together with Repositories, Migrations and Domain Objects, click [here](Adding-New-Data-Structures-and-Storage).