DeepLynx utilizes an event system to allow external applications (primarily data sources) to listen for and be notified of events. For example, an application may be notified that a DeepLynx data source has ingested data, or data sources may use the system to send notifications containing queried data to each other. This allows a workflow to be created where apps no longer need to regularly poll DeepLynx for changes, but can be notified when there is some event to act upon.

### Available Events

Events are messages that are added to a queue within DeepLynx to be processed and sent out. Events may either be created manually via the POST `/events` endpoint, or else created automatically via various actions within DeepLynx as detailed by the table below. In the table below, Events with an Event Source of data_source or container are created automatically when the corresponding Event Type occurs.  

| Event Source | Event Type | Data |
|--------------|------------|------|
| data_source | data_imported | array of imports |
| data_source | data_ingested | import ID, status |
| data_source | file_created | file ID |
| data_source | file_modified | file ID |
| data_source | type_mapping_created (future) | type mapping ID |
| data_source | type_mapping_modified (future) | type mapping ID |
| container | data_source_created | data source ID |
| container | data_source_modified | data source ID |
| container | data_exported | export record |
| manual | manual | any (see below for usage examples) |

### Configuring DeepLynx for Events

Several config parameters can be set through the `.env` file. Set `QUEUE_SYSTEM` to determine what queue system is used. Currently supported values include `database`, `rabbitmq`, and `azure_service_bus`. Events are constantly processed making setting an interval for processing unneccesary. Note that if the `QUEUE_SYSTEM` is `database`, there will be a 500ms delay between processing queries to avoid overloading the database. If using one of the other systems, be sure to provide the appropriate URL or connection string. 

Additionally, the `EMIT_EVENTS` parameter is a boolean flag that indicates whether events should be emitted. Be sure this is set to `true` if you would like events to be emitted. 

### Registering for Events via Event Actions

In order to listen for events, follow the steps below.

1. An application should first query to determine the available containers or data sources and to retrieve the desired ID(s). IDs can be determined by calling the `List Containers` and `List Data Sources` API endpoints (see [OpenAPI Swagger specification](https://gitlab.software.inl.gov/b650/Deep-Lynx/-/blob/development/API%20Documentation/Core.swagger_collection.yaml)).
2. Once the needed ID(s) have been obtained, the application can register for applicable event types (see table above) on that source. This is done through the `Create Event Action` API endpoint (POST `/event_actions`). A body must be included in the request that contains values for `event_type` (required, see list above), `action_type` (required. possible values are `default`, `send_data`, and `email_user`), `container_id`, `data_source_id`, `action_config` (optional object for future use), `destination` (the url to which DeepLynx should send events), `destination_data_source_id` (the id of the data source registering for events, if applicable), and `active` (optional boolean indicating whether the event action is active, defaults to `true`).

For example, if I wanted to listen to `data_ingested` events on a datasource with the ID `123`, in container `1`, my data source ID of `456` and at the url `http://myApp.com/listen`, I would supply the following body to the request:

```json
{
    "container_id": "1",
    "data_source_id": "123",
    "event_type": "data_ingested",
    "action_type": "default",
    "destination": "http://myApp.com/listen",
    "destination_data_source_id": "456"
}
```  

3. When an event is created (such as the `data_ingested` event we want to listen to), DeepLynx will check for event actions that match the `container_id`, `data_source_id`, and `event_type` of the event. Additional filters may be applied and the data sent in the event may be altered, according to the `action_type` and `event_type` (detailed below).

### Action Types and the manual Event Type
  
- An `action_type` of `default` will result in no changes. The event payload will look like the following:  
```json
{
    "id": "the id of the event",
    "event": {"an object containing the information specified in the table above"}
}
```

- An `action_type` of `send_data` will assume that the content of the `event` object within the event is a **valid QraphQL query**. To send an event with this payload, the `event` object should contain query and [variables](https://graphql.org/learn/queries/#variables) fields like the following:  
```json
{
    "query": "{ Document { id    file_name    primary_text }}",
    "variables": {}
}
```
See the [GraphQL section](Querying-Data-with-GraphQL) within this wiki for additional details. Assuming the query can be processed by DeepLynx correctly, DeepLynx will send an event with the following payload:  
```json
{
    "id": "the id of the event",
    "event": {"an object containing the results of the query"},
    "query": {"an object containing the query provided by the event (see example json above)"}
}
```

- An `action_type` of `email_user` will send out an email to the `destination` of the event action, with the `event` body as the content of the email.

- For an `event_type` of `manual`, the `event_config` may contain a key called `destination` with a value of the intended data source ID. If supplied, this will be compared to the `destination_data_source_id` of the event action, and any actions that do not match will not receive the event. For example, to create a `manual` event that will **only be sent to the data source with ID `123`**, the event may look like the following:
```json
{
    "container_id": "1",
    "data_source_id": "456",
    "event_type": "manual",
    "event_config": {
        "destination": "123"
    },
    "event": {
        "query": "{ Document { id   file_name   primary_text }}",
        "variables": {}
    }
}
```
