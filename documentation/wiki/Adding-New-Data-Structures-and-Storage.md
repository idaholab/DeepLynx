DeepLynx is constantly growing and changing, and as a user of DeepLynx, you may find yourself in need of adding a new data structure to suit your needs. This page will briefly go over adding a new data structure and the necessary elements involved. These elements include the necessary data Migrations, as well as creating a Domain Object, Mapper, Repository associated with your new data structure. For more information on each of these design patterns, click the link in the heading to see the associated wiki article.

**NOTE**: any example code in this article is incomplete but instead provides a general pattern for developers to follow. Looking to the existing codebase for more specific examples of this code in action may prove helpful alongside this guide.
## [Data Migration](Migrations)

Once you have decided on a data structure to add, the next step is creating the appropriate underlying SQL structure. This can be done by adding a new migration file. Migration files are raw SQL files used to create, update and delete SQL table structure or SQL functions. These files are executed upon application startup and they run in alphanumeric order. If the most recent file in the migrations folder was `075_some_structure.sql`, best practice is to name your migration file `076_new_structure.sql` to ensure that all table dependencies are properly created prior to executing your new SQL commands. A few things to note when creating a new table in the database:

- There is a possibility that this migration may be run multiple times. As such, in order to avoid creation errors, it is best practice to execute a `DROP TABLE IF EXISTS` statement before creating a table, and to use `CREATE TABLE IF NOT EXISTS` when creating the table.
- DeepLynx is built on Postgres. For guidance on Postgres table creation syntax, visit [this link](https://www.postgresql.org/docs/current/sql-createtable.html).
- Some migrations may be used to alter existing data structures. If you create your data structure and find that there is something missing, instead of altering the original migration, create a new one. In the example case from above, you could name this new migration `077_new_structure_fix.sql`.
- Migrations are executed one statement at time. Make sure that each SQL statement ends with a semicolon to avoid execution errors.
- It is common practice to include created_at/by and modified_at/by columns in your database table. See the example below.

Here is an example of what these migration may look like:

``` sql
-- 076_new_structure.sql
DROP TABLE IF EXISTS new_structure;

CREATE TABLE IF NOT EXISTS new_structure (
    -- column name followed by datatype and any constraints
    id bigserial,
    container_id bigint REFERENCES containers(id) ON UPDATE CASCADE ON DELETE CASCADE,
    name character varying(255) NOT NULL,
    created_at timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_at timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by varchar NOT NULL,
    modified_by varchar NOT NULL,
    PRIMARY KEY (id)
);

-- 077_new_structure_fix.sql
ALTER TABLE new_structure ADD COLUMN status character varying(255);
```
## [Domain Object](Domain-Object-Pattern)

Once the data structure exists in the database, we need to create a way for the data to be represented in the codebase as well. This can be accomplished by creating a domain object. This is a class representing the domain (or data structure) you are using. Once created, this domain object can be used in the Mapper and Repository layers to represent your data. Domain objects should contain validation rules for each field in the data structure. Continuing on with our example, here is some code that could represent our `NewStructure` object:

```typescript
// base domain class already includes creation and modification details,
// so those fields are not included in this class creation
export default class NewStructure extends BaseDomainClass{
    // these validation rules ensure that user input matches
    // with what the database expects to get on insert
    @IsOptional()
    @IsString()
    id?: string;

    @IsString()
    container_id?: string;

    @IsString()
    name: string

    @IsString()
    // only allow for one of these four statuses, set default to 'ready'
    status: 'ready' | 'processing' | 'error' | 'completed' = 'ready'

    constructor(input: {
        // id is auto-generated on insert, and status shouldn't be declared by the user
        container_id?: Container | string;
        name: string;
    }) {
        // initializes base domain class first
        super();
        // set any user-specified fields based on input
        if (input) {
            // check if container is a domain object or just the id
            if (input.container_id instanceof Container) {
                this.container_id = input.container_id.id
            } else {
                this.container_id = input.container_id
            }
            if (input.name) {this.name = input.name}
        }
    }
}
```
## [Data Mapper](Data-Mapper-Pattern)

Now that the data structure exists in both the database and the codebase, the Mapper and Repository layers will translate the data back and forth. The Mapper contains methods to create, read, update, and delete data. These methods call raw SQL queries against the existing data structure in the database. The mapper also contains an `Instance` method for instantiating it in other files. Below is a stripped-down example of what a mapper could look like, with just the `Instance` and `Create` methods:

``` typescript
// mapper class creation
export default class NewStructureMapper extends Mapper {
    // resultClass reflects the domain object in which data is stored outside the database
    public resultClass = NewStructure

    public static get Instance(): NewStructureMapper {
        // create an instance if there isn't one already
        if (!NewStructureMapper.instance){
            NewStructureMapper.instance = new NewStructureMapper();
        }

        return NewStructureMapper.instance;
    }
    
    public async Create(
        userID: string, 
        new_struc: NewStructure
    ): Promise<Result<NewStructure>>{
        // call the private create statement
        const n = await super.run(this.createStatement(userID, new_struc), {
            resultClass: this.resultClass
        });

        // return the errant structure if creation failed
        if (n.isError) {return Promise.resolve(Result.Pass(n))};

        // return successful promise if creation succeeded
        return Promise.resolve(Result.Success(n.value[0]));
    }

    // create statement could be used to insert one to many of these structures
    private createStatement(userID: string, ...new_strucs: NewStructure[]): string {
        // %L below represents the values being passed in
        const text = `INSERT INTO new_structure(
                        container_id,
                        name,
                        status,
                        created_by) VALUES %L RETURNING *`;
        const values = new_strucs.map((n) => [
            n.container_id, n.name, n.status, userID
        ]);

        // the postgres format function combines the query with the 
        // mapped values passed in by the user
        return format(text, values);
    }
}
```

## [Repository](Repository-Pattern)

The data mapper can be used to talk directly with the database, but it contains very basic logic in its query interface. This is where the Repository comes in. The repository builds on the mapper and adds query and validation logic. The repository methods can then be chained together to list filtered results. Repositories typically contain the `save` method for creation and updating, `delete` for deletion, and `findByID`, `list`, and `count` for querying. The code below will demonstrate `save` and `list`.

**NOTE**: In a typical repository implementation, the repository is created using the following syntax:

`export refault class xRepository extends Repository implements RepositoryInterface<x>`
 
The Repository Interface enforces that the new repository must contain the methods `findByID`, `save` and `delete`. In our case, since we are only implementing `save`, we will not use this interface.

``` typescript
export default class NewStructureRepository extends Repository {
    #mapper: NewStructureMapper = NewStructureMapper.Instance;

    async save(n: NewStructure, user: User): Promise<Result<boolean>> {
        // check domain object for validation errors
        const errors = await n.validationErrors();
        if (errors) {
            return Promise.resolve(
                Result.Failure(`structure does not pass validation ${errors.join(',')}`)
            )
        }

        // update if object exists in DB, create if not
        if (n.id) {
            // pseudocode since these methods aren't in our example mapper
            // ... findByID to fetch object
            // ... this.#mapper.Update(fetchedValue);
        } else {
            const created = await this.#mapper.Create(user.id!, n);
            if (created.isError) { return Promise.resolve(Result.Pass(created)) }
            Object.assign(r, created.value);
        }

        return Promise.resolve(Result.Success(true));
    }

    // fields for chaining queries
    id(operator: string, value: any) {
        super.query('id', operator, value);
        return this;
    }

    containerID(operator: string, value: any) {
        super.query('container_id', operator, value);
        return this;
    }

    name(operator: string, value: any) {
        super.query('name', operator, value);
        return this;
    }

    status(operator: string, value: any) {
        super.query('status', operator, value);
        return this;
    }

    // query options such as limit and sortby are found in the base repository class
    async list(queryOptions?: QueryOptions): Promise<Result<NewStructure[]>> {
        const results = await super.findAll<NewStructure>(queryOptions, {
            resultClass: NewStructure
        });

        if (results.isError) { return Promise.resolve(Result.Pass(results)) }

        return Promise.resolve(Result.Success(results.value));
    }
}
```

The methods found in the repository can be chained together in order to list all objects of your structure type. For example, to list all objects of the type newStructure in a particular container which have the status of `ready`, methods could be chained together as follows:

``` typescript
const repo = new NewStructureRepository();
// chaining methods such as WHERE and AND are found in the base repository class
const readyIn1 = await repo.where()
                            .containerID('eq', 1)
                            .and()
                            .status('eq', 'ready')
                            .list();
```

More commonly, these methods can be chained with dynamic values based off of inputs from an API call instead of the static ones we saw above. For example, if an endpoint called the method `listStructureByStatus`:

``` typescript
private static listStructureByStatus(req: Request, res: Response, next: NextFunction) {
    const repo = new NewStructureRepository();

    repo.where()
        .conatinerID('eq', req.container.id!)
        .and()
        .status('eq', req.query.depth)
        .list()
        .then((result) => {
            if (result.isError && result.error) {
                result.asResponse(res);
                return;
            }
            res.status(200).json(result);
        })
        .catch((err) => Result.Failure(err, 404).asResponse(res))
        .finally(() => next());
}
```

For more information on DeepLynx's HTTP Server and API routes, click [here](HTTP-Server).