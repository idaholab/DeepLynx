While developing DeepLynx, you may often find yourself in a situation where you need to retrieve certain information from the database beyond the default `SELECT * FROM table`. There are various ways to go about this, depending on your needs. This article will serve as a guide for which methods to use in which cases, as well as details on how to use the various functions in the repository layer.

## Using a Mapper Function vs. Using the Repo Layer

The first question to ask yourself is if you even need to use the dynamic repository layer to build your query, or if it is more appropriate to use a mapper function. Mapper functions should be used if you don't need any filters on your query, or if you need the same filter every time you call the query. If there is a filter that is only applied sometimes, it is better to use a repository listing function. Below are a few examples of using mapper functions.

### Mapper Function Example

Mapper functions could be used for a simple retrieval when targeting a specific node:

`SELECT * FROM type_mappings WHERE id = $1` (where $1 is mapping ID)

You also might use a mapper function with a few filters on it to retrieve only specific fields that you need:

`SELECT key, user_id, note FROM keypairs WHERE user_id = $1 AND note = $2` to fetch all key-secret pairs for a user with a specific note (only returning relevant fields)

Or you may use a mapper to apply filters from multiple tables, such as when searching for an edge based on its relationship:

``` sql
SELECT e.* FROM current_edges e
JOIN metatype_relationship_pairs mp
    ON mp.id = e.relationship_pair_id
LEFT JOIN metatype_relationships mr
    ON mp.relationship_id = mr.id
LEFT JOIN metatypes origin
    ON origin.id = mp.origin_id
LEFT JOIN metatypes destination
    ON destination.id = mp.destination_id
WHERE origin.name = $1
AND relationship.name = $2
AND destination.name = $3
```

## Using the Repository to Build Queries

If you find yourself needing something more complex or dynamic than what the mapper layer can provide, it may be time to use the repository layer for data retrieval. The repository layer builds off of the most basic mapper functions but allows for more flexibility in querying. Any interactions with the frontend query layer, graphQL, or optional filters (such as listing datasources with the parameter `timeseries=true`) are accomplished by using the repository layer. 

### Quick Reference

Below is a table describing common query actions, a SQL example, and the repository equivalent. Not all the examples will have every argument option listed, but more detail on arguments as well as examples and use cases will be given in corresponding wiki sections below the table.

Desired Output | SQL Example | Repo Method
---|---|---
|>|**[Repository-Specific Functions](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#repository-specific-functions)**|<
| Select all fields from table|`SELECT t.* FROM table t`|list() with no args
|Count all rows in table|`SELECT COUNT(*) FROM table`|count()
|Filter on a local domain property (column known to this repository)|`WHERE column = 'value'`|where().column(operator, value)
|>|**[Basic Querying](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#basic-querying)**|<
|Filter on any property|`WHERE column = 'value'`|query(column, operator, value)
|Filter on a sub-property of a JSON column|`WHERE column->>'key' = 'value'`|[queryJsonb](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#the-queryjsonb-function)(key, column, operator, value)
|Use multiple filters together (AND conjunction)|`WHERE A AND B`|query().and().query()
|Use one filter or another (OR conjunction)|`WHERE A OR B`|query().or().query()
|>|**[Select-list Manipulation](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#select-list-manipulation)**|<
|Override default SELECT * with custom fields|`SELECT t.x, t.y FROM table t`|[select](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#override-the-select-list)(newFields, tableName?)
|Add fields to current select-list|`SELECT t.*, t.x, t.y FROM table t`|[addFields](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#add-to-the-select-list)(newFields, tableName?)
|[Eliminate duplicate records](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#eliminating-duplicate-rows)|`SELECT DISTINCT t.*`|list({distinct: true})
|Eliminate duplicates for a specific field|`SELECT DISTINCT ON (t.id) t.*`|[distinctOn](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#selecting-distinct-on-specific-fields)(distinctFields, tableName)
|>|**[Table manipulation](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#table-manipulation)**|<
|Override default table|`FROM different_table dt`|[from](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#replace-the-initial-table)(table, alias?)
|Join tables|`FROM origin o JOIN destination d ON o.join_col = d.join_col`|[join](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#join-to-other-tables)(destination, conditions?, options?)
|>|**[Advanced Concepts](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#advanced-concepts)**|<
|[Nested AND logic](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#complex-filter-logic)|`WHERE A AND (B OR C)`|query().and(`new repo object with ANDed clauses`)
|Nested OR logic|`WHERE A OR (B AND C)`|query().or(`new repo object with ORed clauses`)
|[Add a subquery](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#using-subqueries) to the from or where clause|`WHERE id IN (SELECT id FROM table WHERE condition = 'value')`<br/><br/>`SELECT id, sub.name FROM (SELECT name FROM table WHERE condition = 'value') sub`|query('id', 'in', subquery(`new repo object specifying subquery`)<br/><br/>from(subquery(`new repo object specifying subquery`), 'sub')
|Group by certain columns when aggregating data|`GROUP BY id`|[groupBy](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#group-by-function)(fields, tableName?)
|>|**[Listing Options](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#listing-options)**|<
|Sort data by field(s)|`ORDER BY id DESC`|[sortBy](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#sorting)(fields, tableName)
|Limit results to the first *n* results|`LIMIT 25`|list({limit: 25})
|Skip first *n* results (typically used with LIMIT for pagination)|`OFFSET 10`|list({offset: 10})

### A Word about Tables and Aliases

A key component of understanding and effectively using the repository layer (especially when dealing with `from` and `join`) is understanding table aliases. The repository layer automatically qualifies all its columns to avoid ambiguity errors being thrown by the database. What this means is that, in the NodeRepository for example, instead of selecting `SELECT * FROM current_nodes`, the repository actually selects `SELECT x###_current_nodes.* FROM nodes x###_current_nodes`, where `###` is a randomly generated number between 1 and 1000.

The `from` and `join` functions enable users to assign these table aliases manually instead of auto-generating them. These aliases can then be passed as arguments to any functions (such as `query` or `groupBy`) that will be referring to any table other than the default, domain-specific table (eg the `current_nodes` view for the NodeRepository, `reports` for the ReportsRepository, etc).

If you do not know what the default table is for the repository you are working with, you can view the constructor for that particular repository in the code base. It should contain a `super()` function that passes a `xyzMapper._tableName` or `xyzMapper._viewName` to the base Repository constructor. This view name or table name is used as the "default table name" that you see referred to later in the article.

If you need to access the auto-generated `tableAlias` for your default table (or any other table for that matter), you can find it by calling `repo._aliasMap.get(tableName)`. This is the alias that will be used by default to qualify all columns. Any functions called after the `.from()` function will instead use the alias associated with that table.

### Repository-specific Functions

Each domain-specific repository is derived from the base Repository object. The constructor for these domain-specific repositories sets up a base query for listing data, starting at the bare minimum `SELECT * FROM table`. This query can then be run using the `.list()` function, or built upon by chaining together query objects. A `.count()` method is also available in most repositories. In order to execute a straightforward list function for a given domain (let's say `nodes`), the following code can be used:

``` javascript
// initialize the repository
let repo = new NodeRepository();

// list the results- executes SELECT * FROM current_nodes
const results = await repo.list();

// ...or count them- executes SELECT COUNT(*) FROM current_nodes
const count = await repo.count();
```

Note that the `repo` object is declared using `let` instead of `const`. This will come in handy later when chaining together repo query objects.

Domain repositories have their own query functions that can be used for ease of access. Query functions take the structure of `columnName(operator, value)`. These functions are wrapper functions around the core query functions, which are discussed below in the **"Basic Querying"** section. 

### Basic Querying

#### The `.query()` function

As mentioned in the previous section, repository wrapper functions such as `.containerID(operator, value)` are built upon the foundation of the basic query function. The basic query function syntax is as follows:

`repo.query(column, operator, value, {options})`

Column is the query you want to query on, operator determines which comparison to make for the given filter (see the next section), and the value is what to compare to. The other parameters, contained in an `options` object, are completely optional and are detailed in the ["Query Options"](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#query-options) section below.

#### Query Operators

There are several different operator types available:

Operator | Description | Example
---------|-------------|--------
'eq' or '==' | equals | `name = 'bob'`
'neq' or '!=' | not equals | `name != 'bob'`
'like' | wildcard pattern matching (uses `_` and `%` as wildcards) | `name ILIKE 'b_b'` - returns 'bob', 'bib', etc <br/><br/> `name ILIKE 'b%b'` - returns 'bob', 'barb', 'bathtub', etc
'<' | less than | `id < 1000`
'>' | greater than | `id > 10`
'%' | trigram matching [(click here for more info)](https://www.postgresql.org/docs/current/pgtrgm.html) | `name % 'bob'`
'in' | check if object is in list | `name in ('billy','bob','joe')`
'between' | check if object is between two values | `id between 10 AND 1000`
'is null' | checks for null. does not take a value. | `name IS NULL`
'is not null' | checks for not null. does not take a value. | `name IS NOT NULL`

#### The `.queryJsonb()` function

The queryJsonb function is very similar to the query function, with the sole difference being the inclusion of a `key` parameter which is used to specify a sub-property to query on. QueryJsonb should only be used when attempting to query specific values from a jsonb column in Postgres. The syntax is as follows:

`repo.queryJsonb(key, column, operator, value, {options})`

and results in SQL similar to this:

`WHERE column->>'key' <operator> value`

Typically the queryJsonb function is used on columns such as `properties` or `metadata_properties`. 

#### Query Options

The query options are as follows:

`dataType`: this option can be used when one needs to explicitly type-cast a value before comparison takes place in Postgres. It is most frequently used when comparing date values (eg `query(date, 'eq', '2011-01-01', {dataType: 'date'})`).

`tableName`: Used to fully qualify the given field (eg `column.table`). This defaults to the native repo table or view (eg `current_nodes`) if not specified. If an alias exists for this table, the function will use the alias for the given table name instead.

`tableAlias`: Used to fully qualify the given field (eg `column.table`). If you supplied a tableName, you don't need to supply tableAlias.

#### Basic Query Chaining

Here is an example of using a few query functions together to narrow down list results from the node repository:

``` javascript
let repo = new NodeRepository();

const metatypeNames = ['Asset', 'Action', 'Requirement']

// the where function initializes a where clause
repo = repo.where()
    .containerID('eq', 123)
    .and()
    .query('metatype_name', 'in', metatypeNames)
    .list()
// SQL of above statement:
// SELECT * FROM current_nodes
// WHERE container_id = 123
// AND metatype_name IN ('Asset', 'Action', 'Requirement')
```

For more complex query logic, see [this section](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#complex-filter-logic).

### Select-list manipulation

There may be scenarios in which you find that you need a more specific field list than just the default `SELECT *`. The following functions are used to meet these needs.

#### Override the Select-list

To override the existing select-list, use the `select(fields, tableName)` function. The parameters are as follows:

`fields`: takes a string or list of strings, or an object of fields and aliases. If a field-alias object is supplied, each field supplied in this context will have its respective alias applied to it.

`tableName` (optional): used to qualify columns. By default, columns will be qualified using the default table alias if no table name or alias is specified here. Typically only used when pulling from multiple tables.

Here is a usage example:

``` javascript
let repo = new NodeRepository();

// will return results of SELECT * FROM current_nodes
let results = await repo.list();

// now we replace the select-list
repo = repo.select(['id', 'name'])

// will return results of SELECT id, name FROM current_nodes
results = await repo.list();
```

#### Add to the Select-list

To add fields to the select-list, use the `addFields(fields, tableName)` function. The parameters are the same as the `select` parameters. `addFields` is typically only used when pulling from multiple tables or adding aggregate functions to the select-list. See the example below for various applications of the `fields` parameter:

``` javascript
let repo = new NodeRepository();
const edgeRepo = new EdgeRepository();
const table = edgeRepo._tableName

// addFields can be called multiple times 
// to account for various field types
repo = repo
    // fake join syntax for this example
    .join(edgeRepo)
    // add singluar field
    .addFields('metatype_relationship_name', table)
    //add multiple fields
    .addFields(['origin_id', 'destination_id'], table)
    // add fields with aliases
    .addFields({id: 'edge_id', properties: 'edge_properties'}, table)

const results = await repo.list()
```

This code is the equivalent of the following query (but will use, auto-generaged aliases)

``` sql
SELECT n.*, -- included by default
    e.metatype_relationship_name, -- added in first addFields
    e.origin_id, e.destination_id, -- second addFields
    e.id AS edge_id, -- fields with aliases from third addFields
    e.properties AS edge_properties
FROM current_nodes n
JOIN current_edges e ... -- join logic will be discussed later
```

#### Eliminating duplicate rows

You may find yourself needing to select only unique records, or eliminate duplicates of a certain column value (such as ID). This can be accomplished using the following listing options like so:

``` javascript
const repo = new NodeRepository();

// returns SELECT DISTINCT * FROM current_nodes,
// eliminating rows where all fields are duplicated
let results = await repo.list({distinct: true})

// returns SELECT DISTINCT ON (n.id) n.* FROM current_nodes n,
// eliminating rows where n.id is duplicated. The sortBy of
// createdAt DESC will be used to determine which row is kept
// (fetching only the most recently created nodes in this case)
let results = await repo.list({
    distinct_on: {column: 'id', table: 'current_nodes'},
    sortBy: 'createdAt', sortDesc: true
})
```

#### Selecting Distinct on Specific Fields

If your needs go beyond the basic "DISTINCT" clause and you need to ensure uniqueness on more than one field, use the `distinctOn` function instead of the listing option. The distinct function's arguments are as follows:

`fields`: takes a string or list of strings. These will be the fields to select distinct on.

`tableName` (optional): used to qualify columns. By default, columns will be qualified using the default table alias if no table name or alias is specified here. You may call `distinctOn` several times if you need to select distinct from different tables.

Here is an example of using `distinctOn`:

``` javascript
// get all edges and establish uniqueness with
// a combination of origin_id, destination_id,
// data_source_id, and relationship_pair_id
let repo = new EdgeRepository();

repo = repo
    .distinctOn([
        'origin_id', 'destination_id', 
        'data_source_id', 'relationship_pair_id'
    ]) // next we need to also sort by these fields
    .sortBy([
        'origin_id', 'destination_id',
        'data_source_id', 'realtionship_pair_id'
    ]) // also sort by created_at DESC to get most recent
    .sortBy('created_at', tableName=undefined, desc=true)

results = await repo.list();
// returns results of
// SELECT DISTINCT ON (origin_id, destination_id, 
//      data_source_id, realtionship_pair_id)
// FROM edges
// ORDER BY origin_id, destination_id, data_source_id,
//      relationship_pair_id, created_at DESC;
```

### Table manipulation

In addition to changing which fields to return to fit your needs, you may also need to pull data from multiple tables. While this can be accomplished using a mapper function, mapper functions do not allow for filtering flexibility like the repository layer does. Fortunately, the `from` and `join` functions allow you to replace and add tables from your query, similar to the `select` and `addFields` functions.

#### Replace the initial table

The `from` function takes the following parameters:

`tableName`: The new table to select from.

`tableAlias` (optional): The alias by which the table should be referred to by. If this is not specified, an alias will be auto-generated and can be fetched by calling `repo._aliasMap.get(tableName)`.

**Important Note**: the `from` function should be called before other functions in order for the alias to be applied to the other functions. Any functions called before `from` need to pass in the designated `from` alias to the `tableName` argument in order for them to work properly. For example:

``` javascript
// this should not work. it outputs the results of
// SELECT n.id, n.name
// FROM edges e
// WHERE e.id = 123
// because select is called before from and uses
// the default nodes table alias instead of the new edges one.
const repo1 = new NodeRepository();
repo1.select(['id', 'name'])
    .from('edges', 'e')
    .where().query('id', 'eq', 123)

// this should work because 'e' is specified as the table alias.
// query: SELECT e.id, e.name
// FROM edges e
// WHERE e.id = 123
const repo2 = new NodeRepository();
repo1.select(['id', 'name'], 'e')
    .from('edges', 'e')
    .query('id', 'eq', 123)

// this should also work because select is called after from.
// it outputs the same query as repo2.
// the order in which the functions are chained does not effect
// the order they will appear in the SQL statement, but does
// effect the way aliases are applied.
const repo3 = new NodeRepository();
repo1.from('edges', 'e')
    .select(['id', 'name'])
    .query('id', 'eq', 123)
```

For more information on working with table aliases, see ["A Word about Tables and Aliases"](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#complex-filter-logic) above.

#### Join to other tables

The `.join()` function allows you to combine multiple tables and [even draw data from subqueries](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#using-subqueries). The syntax of the join function is as follows:

`join(destination, conditions, origin?, options?)`

The parameters are:

`destination`: The table to join to (`FROM table JOIN `**`destination`**)

`conditions`: an object or list of objects containing origin_col, operator, and destination_col:
- `origin_col`: the join column in the origin table
- `operator` (optional): either `=` or `!=`; defaults to `=`
- `destination_col`: the join column in the destination table

`options`: an object with various options to customize the join:
- `destination_alias`: the alias by which the destination is known. if none is provided, this defaults to the destination's existing alias if there is one, or creates a new alias if not. This created alias can be found using `repo._aliasMap.get(destination)`.
- `join_type`: The type of join to be performed. Options are `INNER`, `LEFT`, `RIGHT`, or `FULL OUTER`. Defaults to `LEFT`.
- `origin`: The alias of the table you are joining from. If none is supplied, the join uses the default table's alias.

To clarify how the join function works, let's look at an example. Let's say we want to recreate the following query which gets some information from a given edge and its origin and destination nodes:

``` sql
SELECT n1.id AS n1_id, n1.properties AS n1_props,
    e.id AS edge_id, e.properties AS edge_props
    n2.id AS n2_id, n2.properties AS n2_props,
FROM current_nodes n1
INNER JOIN edges e
    ON n1.id = e.origin_id
INNER JOIN current_nodes n2
    ON e.destination_id = n2.id;
```

Using our repository layer, we could do this like so:

``` javascript
let repo = new EdgeRepository();

// override default SELECT * with only the desired fields
repo = repo.select(['id', 'properties'], 'n1')
    .from('current_nodes', 'n1') //override the default alias
    // join current_nodes n1 to current_edges e
    .join('current_edges',
        // origin(n1).id = destination(e).origin_id
        {origin_col: 'id', destination_col: 'origin_id'},
        {
            destination_alias: 'e', // alias current_edges as e
            join_type: 'INNER' // ensure this is an inner join
        })
    // join current_edges e to current_nodes n2
    .join('current_nodes',
        // origin(e).destination_id = destination(n2).id
        {origin_col: 'destination_id', destination_col: 'id'},
        {
            destination_alias: 'n2', // alias current_nodes as n2
            join_type: 'INNER', // ensure this is an inner join
            origin: 'current_edges' // join from current_edges
        })
```

### Advanced Concepts

There are certain cases where you may need to get into more complex, nested querying to get your data. This section will complex ("nested") filtering, as well as instructions and use cases for using subqueries in the repo layer. This section also covers the `groupBy` function for grouping fields.

#### Complex Filter Logic

Basic query chaining is useful when you need to apply several filters at once, but may not suit your needs when you need to apply OR logic with more than two filters. A simple query such as `SELECT * FROM people WHERE fname = 'bob' OR fname = 'tim'` can be executed easily with basic query chaining, but what if you wanted to find people named "bob" or "tim" whose last name is "smith"? Using basic query chaining, which doesn't add any parentheses to the where clause, would result in a query like this: 

`SELECT * FROM people WHERE fname = 'bob' OR fname = 'tim' AND lname = 'smith'`

This query may not look incorrect at first, until you realize that this will return all people named "bob" regardless of last name OR people named "tim smith". Instead, you need a query like this:

`SELECT * FROM people WHERE (fname = 'bob' OR fname = 'tim') AND lname = 'smith'`

Queries with nested filter logic (using parentheses) can be generated using the `and`, `or` and `where` functions. With no arguments, these functions are used to execute basic query chaining:

``` javascript
let repo = new NodeRepository();

repo = repo.where()
    .query('fname', 'eq', 'bob')
    .or()
    .query('fname', 'eq', 'tim')
    .and()
    .query('lname', 'eq', 'smith')
    .list()
// yields results of
// SELECT * FROM nodes
// WHERE fname = 'bob' OR fname = 'tim' AND lname = 'smith'
```

However, you can pass in the `repo` argument to these functions to generate a nested query. The `repo` argument is a new repository object and can be used like so:

``` javascript
let repo = new NodeRepository();

repo = repo.where(
        new NodeRepository()
        .query('fname', 'eq', 'bob')
        .or()
        .query('fname', 'eq', 'tim')
    ).and()
    .query('lname', 'eq', 'smith')
// yields results of
// SELECT * FROM nodes
// WHERE (fname = 'bob' OR fname = 'tim') AND lname = 'smith'
```

Here's another example using the `and()` function instead. This will generate a slightly different query that will yield the same results:

``` javascript
let repo = new NodeRepository();

repo = repo.where()
    .query('lname', 'eq', 'smith')
    .and(
        new NodeRepository()
        .query('fname', 'eq', 'bob')
        .or()
        .query('fname', 'eq', 'tim')
    )
// yields results of
// SELECT * FROM nodes
// WHERE lname = 'smith' AND (fname = 'bob' OR fname = 'tim')
```

#### Using Subqueries

Subqueries are used to return data to the main query in order to further narrow down your data. In the FROM clause, subqueries can be used as custom pseudo-tables from which to select or join. In the WHERE clause you can compare columns against the results of a subquery to refine your filters. The `subquery` function takes the same `repo` argument as the `and`, `or` and `where` functions mentioned above. This repo object is used to formulate the subquery. It is recommended that, when using subqueries, you store the results of the subquery in a variable. Below are examples of using subqueries in both the FROM and WHERE clauses.

Note that when using a subquery in the FROM clause, you must manually assign an alias using the `tableAlias` argument in the `from` function or the `destination_alias` option in the `join` function.

``` javascript
// Using a subquery in the FROM clause to add the 
// results of an aggregate function to the result-set.
let repo = new NodeRepository();

// generate the subquery
const sub = repo.subquery(
    new NodeRepository()
    .select('id', 'sub')
    .select('jsonb_agg(data) AS history', 'data_staging')
    .from('nodes', 'sub')
    .join('data_staging', 
        {origin_col: 'data_staging_id', destination_col: 'id'})
    .groupBy('id', 'sub'))
// returns this query:
// SELECT sub.id, data_staging.jsonb_agg(data) AS history
// FROM nodes sub
// JOIN data_staging
// ON sub.data_staging_id = data_staging.id
// GROUP BY sub.id

// join to the subquery
repo = repo
    .addFields('history', 'raw_data_history')
    .join(history, 
        {origin_col: 'id', destination_col: 'id'},
        {destination_alias: 'raw_data_history'})
    .where()
    .containerID('eq', 123)
    .list()
// yields results of
// SELECT node_alias.*, raw_data_history.history
// FROM current_nodes node_alias
// JOIN (<subquery>) raw_data_history
// ON node_alias.id = raw_data_history.id
// WHERE container_id = 123
```

``` javascript
// Using a subquery in the WHERE clause to narrow results
// to only those that fit the subquery condition.
let repo = new ImportRepository();

// generate the subquery
const sub = repo.subquery(
    // select from the data source repository
    new DataSourceRepository()
    .select('id').where().containerID('eq', 123))
// returns SELECT id FROM data_sources WHERE containerID = 123

// join to the subquery
repo = repo
    .select('data', 'ds')
    .addFields('status', 'i')
    .from('imports', 'i') // reassign default alias
    .join('data_staging',
        {origin_col: 'id', destination_col: 'import_id'},
        {destination_alias: 'ds'})
    .where()
    .query('data_source_id', 'in', sub, {tableAlias: 'ds'})
    .list()
// yields the results of
// SELECT ds.data, i.status
// FROM imports i JOIN data_staging ds
// ON ds.import_id = i.id
// WHERE ds.data_source_id IN (<subquery results>)
```

**Note**: subqueries are typically more expensive for the database to perform compared to a more basic query with joins and filters. If you can figure out how to meet your needs without using a subquery, you may see faster query results.

#### Group By Function

The `.groupBy` function is structured similarly to the `select` and `addFields` functions, and is used to generate a GROUP BY clause for aggregate functions. Its parameters are the following:

`fields`: a string or list of strings which will be the fields to group by.

`tableName`: the table alias with which the grouping fields will be qualified. If none specified, uses the default table name. Multiple `groupBy` functions may be called if using columns from multiple tables to group. If `null` is supplied as tableName, don't qualify these fields.

Here is an example, retrieving data source ID, data source name, import ID, and a count of data records per import:

``` javascript
let repo = new DataSourceRepository();

repo = repo
    .select(['id','name'], 'src')
    .addFields(['import_id', 'COUNT(id) AS records'], 'data')
    .from('data_sources', 'src')
    .join('data_staging',
        {origin_col: 'id', destination_col: 'data_source_id'},
        {destination_alias: 'data'})
    .groupBy(['id', 'name'], 'src')
    .groupBy('import_id', 'data')
    .list({
        sortBy: 'records',
        tableName: null, //don't qualify this field
        sortDesc: true
    })
// this returns the results of
// SELECT src.id, src.name, 
//     data.import_id, COUNT(data.id) AS records
// FROM data_sources src
// JOIN data_staging data
// ON src.id = data.data_source_id
// GROUP BY src.id, src.name, data.import_id
// ORDER BY records DESC
```

### Listing Options

The key component to retrieving your data after you have used the other functions to form your query is calling the `.list()` function. This function will assemble the query and return the results. There are several options that can be passed into the `.list()` function, as follows:

`limit`: Limit your results to the first *n* records. For example, `list({limit: 10})` adds `LIMIT 10` to the SQL statement.

`offset`: Skip the first *n* records. For example, `list({offset: 5})` adds `OFFSET 5` to the SQL statement. Offset is typically used together with limit for pagination of results. See the "Pagination" section below for an example.

`sortBy`: Sort the fields based on this column. For example `list({sortBy: 'name'})` adds `ORDER BY name` to the SQL statement and sorts results alphabetically by name. For more complex sorting (multiple fields, multiple orders) see the "Sorting" section below.

`sortDesc`: Sort ascending or descending. Building on the previous example, using `list({sortBy: 'name', sortDesc: true})` adds `ORDER BY name DESC` and sorts results by name in reverse alphabetical order.

`distinct`: Eliminate duplicate rows. See the ["Eliminating duplicate rows"](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#eliminating-duplicate-rows) section for more details.

`distinct_on`: Eliminate rows that duplicate a specific column's value. See the ["Selecting Distinct on Specific Fields"](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#selecting-distinct-on-specific-fields) section for more details.

`groupBy`: Column to group by. To group by more than one column, you may pass in a string with commas in it (eg `'id, name'`). However, it is encouraged to use the [`.groupBy` function](https://github.com/idaholab/Deep-Lynx/wiki/Forming-Dynamic-SQL-with-the-Repository#group-by-function) instead in this case.

`tableName`: Used to qualify field(s) from the `groupBy` or `sortBy` option if different from the default table. If `null` is used as tableName, don't qualify these fields.

#### Pagination

The LIMIT and OFFSET clauses can be used to break large amounts of data into smaller groups, or "pages" in order to make it more digestable (especially for the UI). If you had a result-set of 134 records and wanted to split it out into 25-item pages, you might do something like this:

``` typescript
async listPageItems(page: number, itemsPerPage: number) Promise<Result<Node[]>> {
    const pageNumber = page - 1;

    await new NodeRepository().list({
        limit: itemsPerPage,
        offset: itemsPerPage * pageNumber
    })
}

const pageLimit = 25
await this.listPageItems(1, pageLimit) // lists items 0-25
await this.listPageItems(2, pageLimit) // lists items 26-30
// ...
await this.listPageItems(6, pageLimit) // lists items 125-134
```

#### Sorting

If you find yourself needing to list more than one field, or have one field sorted ascending and another descending, or find some other sorting needs, it is suggested you use the `sortBy` function over the listing option of the same name. `sortBy` taks the following arguments:

`fields`: takes a string or list of strings. These will be the fields to order your results on.

`tableName` (optional): used to qualify columns. By default, columns will be qualified using the default table alias if no table name or alias is specified here. You may call `sortBy` several times if you need to order by columns from different tables. If `null` is supplied as tableName, don't qualify these fields.

`desc` (optional): A boolean used to specify to sort descending. If you need some columns ascending and others descending, use multiple `sortBy` functions.

This example using `distinctOn` and `sortBy` illustrates:

Here is an example of using `distinctOn`:

``` javascript
// get all edges and establish uniqueness with
// distinct on. sort on these fields, as well
// as created_at descending to get the most 
// recent edges.
let repo = new EdgeRepository();

repo = repo
    .distinctOn([
        'origin_id', 'destination_id', 
        'data_source_id', 'relationship_pair_id'
    ])
    .sortBy([
        'origin_id', 'destination_id',
        'data_source_id', 'realtionship_pair_id'
    ])
    .sortBy('created_at', tableName=undefined, desc=true)

results = await repo.list();
// returns results of
// SELECT DISTINCT ON (origin_id, destination_id, 
//      data_source_id, realtionship_pair_id)
// FROM edges
// ORDER BY origin_id, destination_id, data_source_id,
//      relationship_pair_id, created_at DESC;
```