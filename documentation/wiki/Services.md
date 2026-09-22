The `src -> services` folder contains various services and utilities associated with DeepLynx. There are various folders associated with specific services, as well as a few standalone files as follows:

- `blob_storage`: folder containing code that handles file storage either locally or using Azure's Blob Storage service. To learn more about uploading files to DeepLynx, click [here](Uploading-Files)
- `cache`: handles cache implementation, including methods to implement getting, setting, and clearing the cache. DeepLynx uses Redis for caching.
- `email`: handles email services for DeepLynx
- `gremlin`: contains DeepLynx's implementation of gremlin. Gremlin is used to communicate with graph database solutions such as JanusGraph, Neo4j, and CosmosDB. To learn more about gremlin, click [here](https://tinkerpop.apache.org/gremlin.html)
- `queue`: contains the implementation of DeepLynx's queueing system
- `config.ts`: contains variables representing the application's configuration and environment at the time of original construction. This configuration pulls defaults from your `.env` environment file.
- `logger.ts`: contains the code to implement DeepLynx's logging system
- `ultilities.ts`: contains various utility functions which don't directly relate to any one aspect of the data warehouse, but are useful in multiple contexts and can be imported into files for use during development.