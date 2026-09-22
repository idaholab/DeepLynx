DeepLynx features the capability to create a "snapshot" of your data and export it to different storage mediums. Each exporter will allow you to take the data stored as part of DeepLynx for a container, and send it to a different location.


## Configuration
### Gremlin Exporter `gremlin`
_____
This export adapter connects the application to any Gremlin API. Gremlin is used to communicate with graph database solutions such as JanusGraph, Neo4j, and CosmosDB. As long as the storage solution exposes a Gremlin API, this export adapter will function correctly.


**Configuration**
```shell script

// example request body POSTed to /containers/:container-id/data/export
{
    "adapter": "gremlin", - declare adapter name
    "config": {
        "traversal_source": "g", - default is generally 'g', see documentation with questions
        "mime_type": "application/vnd.gremlin-v2.0+json", -  OPTIONAL determines which version of GRAPHson to use, defaults to v3
        "graphson_v1": false, - OPTIONAL force GRAPHson v1, defaults to false 
        "user": "process.env.GREMLIN_PLUGIN_USER || ", - Gremlin user
        "key": "process.env.GREMLIN_PLUGIN_KEY || ", - Gremlin secret key
        "endpoint": "localhost",
        "port": "8182",
        "path": "/gremlin",
        "writes_per_second": 300 - Allows us to throttle the export
    }
}
```

## Export
Once the exporter is configured and you're given its id, simply send a POST request with no body to `{{baseurl}}/containers/{container-id}/data/export/{exporter-id}` and your export will begin. There is currently no way to monitor progress of an export except through log monitoring.
