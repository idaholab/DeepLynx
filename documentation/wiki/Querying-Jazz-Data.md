# Using GraphQL to query Jazz data in DeepLynx

DeepLynx ships with the ability to query data that you have ingested in previous steps. This means that users can use the GraphQL interface to query data imported from Jazz.

This guide assumes basic knowledge of querying using GraphQL. You can find a good tutorial and write-up here - https://graphql.org/learn/. You should pay particular attention to how queries are built, how GraphQL returns only the data you explicitly request, and any documentation specific to the language you hope to use to interact with this functionality.  

## Jazz data Classes

In the DeepLynx data warehouse implementation of the DIAMOND ontology, Jazz data is referenced by the "Requirement" class. As such, the queries in this documentation will refer to Jazz data as "Requirements".

## Introspection 

You can use introspection at any time to get a complete list of all potential types, and other information. This is useful as your schema might be different from another user’s based on the container you are querying. This is also helpful because we must perform a slight conversion on your Class names to make them work with GraphQL – and introspection allows you to see exactly how we might have named your custom Class. 

Much like normal queries, you would POST your introspection GraphQL query to `{{yourURL}}/containers/{{containerID}}/data` - and much like normal queries, your POST body should be a JSON body with the following two fields - `query` and `variables`. `query` is an escaped string containing your GraphQL query. `variables` is a JSON object containing any variables which were included in your query.  

More information on formatting a query can be found here - https://graphql.org/learn/serving-over-http/. 

Here is an example introspective query - this one simply returns a list of all possible GraphQL types by name. 

``` 
{ 
  __schema { 
    types { 
      name 
    } 
  } 
} 
``` 

**Note:** *Not all types* that are returned by this query are Classes. Some might be native GraphQL types, or enumerable types needed by a Class. When in doubt, refer back to the container’s ontology to differentiate between what’s a standard GraphQL type and what’s a Class. 

## Writing Queries 

**Please keep in mind that this is actively under construction, meaning things may change slightly and new features may be added. We will endeavor to keep this guide up to date, but if in doubt please contact the development team.** 

Currently, you can only write queries which return a list of node records from the DeepLynx graph database. Plans have been made to extend this functionality to edges in the future, but that functionality currently doesn’t exist. There are also plans to include an HTTP endpoint to which you would pass a node/edge ID and a filter object to receive a response which represents the actual graph structure in which DeepLynx stores its data.  

We will not spend a large amount of this guide on *how* to make a GraphQL request. We feel this topic is covered well in other guides, as well as the [official documentation]( https://graphql.org/learn/serving-over-http/) . We follow all standard practices in how a GraphQL query is structured as a JSON object for POSTing to an HTTP endpoint. This guide also assumes that while we display a GraphQL query in its object form that you are aware that it must be encoded as  a string and included in a JSON object as the `query` property.

The **most important thing** to remember is that the endpoint to which you send all GraphQL queries (via HTTP POST requests) is the following: 

`{{yourURL}}/containers/{{yourContainerID}}/data`  

### Querying for Nodes based on Class 

The primary objective of our GraphQL endpoint and functionality is to allow a user to swiftly retrieve a list of all nodes of a given Class and which match a set of filters based on that Class’s properties.  

The following steps will demonstrate how a user might query and receive nodes of the Requirement type.  

A user must know the name of the GraphQL type for “Requirement”. This is done most easily by running the introspective query listed earlier in this page and searching for a type by, or close to, that name. In this case the name of the GraphQL type should be the exact same – “Requirement”. **Note: Class names that consist of more than one word will have their spaces replaced by the `_` character. So “Maintenance Entry” would become “Maintenance_Entry” in the GraphQL schema. A Class whose name starts with a number will have `_` prepended to its name. So “1 Maintenance” would become “_1_Maintenance”.**  

**Optional** - Just like on the main GraphQL schema, you can run an introspective query on the “Requirement” type to see what fields may exist for you to request and query against. The following query illustrates how to accomplish this.  The fields returned from this query represent all valid fields upon which you can filter or request, as well as their datatypes.

``` 
{ 
  __type(name: "Requirement") { 
    name 
    fields { 
      name 
      type { 
        name 
        kind 
      } 
    } 
  } 
} 
``` 

You might see the following response (explanation of fields are in comments next to fields) 

``` 
{ 
    "data": { 
        "__type": { 
            "name": "Requirement", # represents which Class you queried 
            "fields": [ 
                { 
                    "name": "_record", # this is a special object which 
                    "type": {          # contains metadata about the object  
                        "name": "recordInfo", 
                        "kind": "OBJECT" 
                    } 
                }, 
                { 
                    "name": "type", # an actual property of the Requirement
                    "type": { # datatype of the field
                        "name": "String",  
                        "kind": "SCALAR" 
                    } 
                }, 
                { 
                    "name": "active", 
                    "type": { 
                        "name": "Boolean", 
                        "kind": "SCALAR" 
                    } 
                }, 
                { 
                    "name": "id", 
                    "type": { 
                        "name": "Float", 
                        "kind": "SCALAR" 
                    } 
                } 
            ] 
        } 
    } 
} 
``` 

We can further interrogate the `_record` objects (and any other fields of the kind `OBJECT`) using another introspective query based on the type name of these objects:

```
{ 
    __type(
        name: "recordInfo"  #name field of "_record" type above
    ){ 
        fields{
            name
            type{
                name
            }
        }
    }
} 
```

This will allow us to see which sub-fields may be queried in these `OBJECT`-typed fields.

#### Class Field Names

Note that the field names of the Requirement class will not appear the exact same as they do in the Jazz UI. Once a user knows all the possible Requirement data fields (found via introspection), they can then craft the query. Remember that GraphQL **only returns the data you explicitly request**. This means that if you leave a field off your request, the return value will also omit this field.

The query below will request **all** possible fields for the “Requirement” Class *as it currently stands*. As these fields may change based on a different ontology, it is good practice to query "Requirement" introspectively to see if the fields have changed.
**NOTE: there is an extra `metatypes` wrapper around this query as compared to the original query structure. Adding this layer around your Class queries will specify to GraphQL that you are searching on class (aka metatype) and not relationship.**

```
{ 
    metatypes{ # specify that we are querying classes, or metatypes
        Requirement { # start the query with the GraphQL name of your class
            id                  # represents the artifact ID found in Jazz
            name                # artifact title
            basis  
            description
            primary_text        # text found in left pane on artifact page
            type                # eg System Requirement, etc
            customer_specified
            verification_method
            verification_criteria
            heading_type
            experimenter_guide_flag
            creation_date       # these reflect creation and modification in Jazz
            creation_user
            modified_date
            modified_user
            _record {   # contains metadata about the node
                id                # DeepLynx id
                data_source_id    # id corresponding with Jazz datasource
                original_id 
                import_id 
                metatype_id     # class ID
                metatype_name   # class name
                created_at 
                created_by 
                modified_at 
                modified_by 
                metadata 
                limit   # both limit and page are currently under construction 
                page    # and do not affect the data 
            } 
        } 
    }
} 
```

### Filtering Results 

There are many cases where, instead of simply listing all the instances of Requirement, the user will want to filter their results to meet certain criteria. Filtering is easily broken down by fields, with the argument as a string as follows:

```
{
    metatypes{ // classes are known as metatypes in graphQL
        Requirement(name: {operator: "eq", value: "M21 - Total Cesium Loading"}){
            id
            name
            type
            basis
        }
    }
}
```

This query would return all requirements with the specified name from DeepLynx, as well as their id, type, and basis.

#### Using Operators

The object portion of the query actually consists of two parts: a search operator, and the data to search on. The search operators are as follows:

| Operator | Description | Returns | Example |
| -------- | ----------- | ------- | ------- |
| `eq` | Equals or equal to. This is the implicit behavior. | All nodes with specified fields matching the expression | (name: "eq M21 - Total Cesium Loading") |
| `neq` | Non-equal or not equal to. | All nodes except those with specified fields matching the expression | (name: "neq M21 - Total Cesium Loading")
| `like` | Matches results against a passed-in pattern. Uses wildcard `%` for any number of characters and `_` to match 1 character. Wildcard behavior mimics that of [Postgres](https://www.postgresql.org/docs/9.3/functions-matching.html). | All nodes whose specified fields match the given pattern | (name: "like %M21%") searches for "M21" anywhere in the name |
| `in` | Matches one result from an array of options. | All nodes whose specified fields match one option in the array | (id: "in 179,306") |
| `>`, `<` | Check if a numerical field is less than or greater than a certain value. **Please note that >= and <= are not currently supported.** | All nodes whose specified fields are greater than or less than the given value | (id: "> 200") |

#### Filtering on Multiple Fields

There will likely be situations in which you want to filter on multiple fields within a class. This is also possible through GraphQL. To filter on multiple fields, simply separate the fields with whitespace like so:

```
{
    metatypes{ // classes are known as metatypes in graphQL
        Requirement(
            id: {operator: ">" value:"200"}
            name: {operator: "like" value: "%M21%"}
        ){
            id
            name
            type
            basis
        }
    }
}
```

This query would return all requirements whose id matches the condition "> 200" and whose name contains the string "M21". This behavior mimics that of an AND conjunction. Support for OR is not currently available.

#### Filtering on Record Information

As was covered in a previous section, each node of this class will contain metadata about the node record as stored in the `_record` field:

```
{ 
    metatypes{ // classes are known as metatypes in graphQL
        Requirement { 
            _record {   # contains metadata about the node
                id                # DeepLynx id
                data_source_id    # id corresponding with Jazz datasource
                original_id 
                import_id 
                metatype_id     # class ID
                metatype_name   # class name
                created_at 
                created_by 
                modified_at 
                modified_by 
                metadata 
                limit   # both limit and page are currently under construction 
                page    # and do not affect the data 
            } 
        } 
    } 
}
```

This data will be returned according to what was requested, just like other fields in GraphQL syntax. There are a few sub-fields within this record information which can be queried on. They are: `data_source_id`, `original_id`, `import_id`, `page`, and `limit`. In the current state, `page` and `limit` are under construction so querying on them would not be useful. In future updates, these attributes will be used to implement pagination of results.

To query on a sub-property of `_record`, the syntax is as follows:

```
{
    metatypes{ // classes are known as metatypes in graphQL
        Requirement(
            _record: {id: {operator: ">", value:"200"}}
        ){
            _record{
                data_source_id
                original_id
                import_id
            }
            name
            type
        }
    }
}
```

The main difference here is that, because `_record` is an Object, its sub-properties are queried using an additional set of curly braces (`{}`) to specify the sub-field. You can also query for multiple sub-fields just like with a normal query:

```
{
    metatypes{ // classes are known as metatypes in graphQL
        Requirement(
            _record: {
                id: {operator:">" value: "200"} 
                data_source_id: { operator: "eq" , value :"2"}
            }
        ){
            ... <fields>
        }
    }
}
```