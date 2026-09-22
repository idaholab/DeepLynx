We follow the Domain Object design pattern. Some information on that can be found [here](https://martinfowler.com/eaaCatalog/domainModel.html).

All Domain Objects in our code base consist of a class representing a domain. The `Metatype` class, for example, represents a Class in the ontology. Each domain object also contains the validation rules for each field in the data structure which the class can be converted to.

All Domain Objects are contained in `src->domain_objects` and are sorted and organized in a manner which represents the system as a whole. This organization is reflected throughout the rest of the application and is as follows
- access_management: regarding users, api keys, and interactions with the rest of the system
- data_warehouse: this is the bulk of the system and is further broken down into data, etl, import, export, and ontology. You will find the majority of classes here
- event_system: domain objects representing the internal and external facing event system of DeepLynx


Domain Objects need to follow some rules:
- All validation rules should live inside the domain object
- All domain logic **which does not involve the database or external services** should be considered methods on the domain object class
- Naming should be explicit
- Constructor's root parameters must be optional (class-transformer will break things otherwise)
- Property names must be snake_case - this is so that we don't have to do any manipulation when dealing with PostgreSQL column names

To see how Domain Objects are integrated with Mappers and Repositories in DeepLynx, check out [this article](Adding-New-Data-Structures-and-Storage).