
# How to Contribute

## Coding Guidelines
* Follow the .NET Core coding conventions.
* For backend API, dependency injection, error handling, and solution structure conventions, see [DeepLynx Nexus Backend Development Style Guide](documentation/development-code-style-guide.md).
* Write clear and concise commit messages.
* For backend architecture, see [backend architecture](documentation/architecture.md).
* Ensure your code is well-documented and includes necessary comments.
* Maintain consistency with the existing codebase.
### Testing
#### Prerequisites
1. [Docker](https://docs.docker.com/get-started/) - our unit testing is using [Testcontainers](https://testcontainers.com/) which will utilize a docker container 

* Write unit tests for any new functionality within the deeplynx.tests folder.
* Ensure all existing and new tests pass before submitting your pull request.
* Run the tests using the following command from the root folder:


```
dotnet test
```

#### Sql Procedures
* If using Entity Framework, when the code runs an sql procedure, you need to make sure to clear the change tracker after 
the procedure runs. That way, the context will be forced to sync with the changes the procedure made in the database. 
To do this, add this in your test after a procedure is called:
```
Context.ChangeTracker.Clear();
```
Use the cascade delete tests in Project as a reference.

#### Test Fixture
* Make sure to include the testing fixture in your unit test suite. This makes sure that only one container is spun up 
for each test suite and makes the tests much more efficient. 
```csharp
public ClassIntegrationTests(TestSuiteFixture fixture) : base(fixture) {}
```

* You will also need to add this annotation to your test class:

```
[Collection("Test Suite Collection")] 
```

#### Environment Variables
When a change to environment variables is made, be sure to reflect those changes in both the `.env_sample` file on the backend as well as in GitHub actions. If you do not have access to GitHub actions to add the environment variable, please contact one of the codeowners.

## Submitting Pull Requests
When you're ready to submit your changes, follow these steps:

1. Create a new branch associated with a jira ticket: `git checkout -b DL-100`
    - If there are multiple jira tickets you're covering, do your best to split them into separate PRs to make them digestable.
    - If you must tackle multiple tickets in a single PR, just choose one ticket number for your branch name.

2. Make your changes: Ensure your changes include appropriate documentation and tests.

3. Build the app: Ensure the app builds properly with your changes.
    - Using Rider, this can be performed with the play button in the top right: ![alt text](markdown-assets/buildApp.png)
    - if the resulting browser window doesn't show API routes, there is something wrong with the build. Please address any build issues before submitting your PR.

4. If you added API endpoints, test them in scalar.
    - Following the instructions in step 3 should bring you to a page like this. You can also find it by navigating to `localhost:5095`.
    ![alt text](markdown-assets/scalar.png)
    - Test each endpoint that you created. 
        - Doing so may require you to insert some dummy data in other domains.
        - For example, to create a record, you need to create a project and a datasource first.
        - Data created via scalar, just like via Postman or other tools, will live on in your database. This means that if you have previously created additional objects for testing, you will likely not need to re-create new ones.
    - To do your testing, you can use scalar, Postman, or any other API client of your choice. Scalar is just highlighted here because it is conveniently built in to the project.

4. Create a pull request: Provide a clear description of your changes and any related issues.

### Communication
If you need any help or have questions, feel free to reach out. 

# The Meat and Potatoes

Below is the home for several technical "gotchas" that may be useful for other project developers. If you learn something new as you develop, feel free to add a section here.

## Coding Standards for Controllers and Business Classes

* Methods for Controllers and Business Classes should be prefaced by a comment similar to this:
    ```
    /// <summary>
    /// Summary of what your code does
    /// </summary>
    /// <param name="paramName"></param>
    /// <returns></returns>
    ```
* Controllers should include error handling on each route in try-catch format
* Routes names and the corresponding Controller and Business method names should be descriptive, such as "CreateDataSource"
* Two `Dto` object should be used within each domain: A `RequestDto` object, containing the fields which a user submits upon POST or PUT, and a `ResponseDto` object, containing the fields which should be exposed to the user upon return.
* To ensure all `Dtos` are included in the open api auto-generated document, they should be explicitly called in the return type of controller methods: 
```
 public async Task<ActionResult<ClassResponseDto>> CreateClass()
```
* Please write unit tests for your business classes and perform postman/scalar testing for your controllers.

## Making Database Changes

Making changes to the data layer and underlying database can feel quite involved, but when done right it will facilitate easy transfer of changes from developer to developer. Here are some tips:

### Adding a column

In the table that you are adjusting, you can add a column like so:

```
[Column("new_column")]
public dataType NewColumn { get; set; }
```

If the column is nullable, be sure to add a ? after the dataType. So for example, `long` becomes `long?`

### Adding an index

Indexes can be added at the top of a table file like so:

```
[Index("ColumnName", Name = "idx_table_name_column_name")]
[Index("OtherColumn", Name = "idx_table_name_other_column")]
public partial class TableName
```

### Adding a foreign key

Foreign keys need to be referenced in both the table where the key resides, and the table being referenced. Changes to DeepLynx context will also need to be made. For example, if I was making a foreign key to represent a one-to-many relationship between Books and Authors:

Book.cs:
```
[Table("books", Schema = "deeplynx")]
[Index("AuthorId", Name = "idx_books_author_id")]
public partial class Book
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("author_id")]
    public long AuthorId { get; set; }

    [ForeignKey("AuthorId")]
    [InverseProperty("Books")]
    public virtual Author Author { get; set; } = null!;
}
```

Author.cs:
```
[Table("authors", Schema = "deeplynx")]
public partial class Author
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [InverseProperty("Author")]
    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
}
```

DeeplynxContext.cs:
```
public partial class DeeplynxContext : DbContext
{
    public DeeplynxContext()
    {
    }

    public DeeplynxContext(DbContextOptions<DeeplynxContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Author> Authors { get; set; }

    public virtual DbSet<Book> Books { get; set; }

    modelBuilder.Entity<Author>(entity =>
    {
        entity.HasKey(e => e.Id).HasName("authors_pkey");
    });

    modelBuilder.Entity<Book>(entity =>
    {
        entity.HasKey(e => e.Id).HasName("books_pkey");

        entity.HasOne(b => b.Author).WithMany(a => a.Books).HasConstraintName("books_author_id_fkey");
    });

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
```

### Making a migration

To create a migration reflecting your changes, run the following command (replacing <MIGRATION_NAME>):

```
dotnet ef migrations add <MIGRATION_NAME> -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
```

### Updating the database

This can be done after you've made your migration to verify that the changes are accurately reflected in the DB. Use this command:

```
dotnet ef database update -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
```

### Removing a migration
If you have already applied the migration, you need to run an update pointing to the migration before the one you want
to remove (skip to step 3 if you have not applied it).

1. To easily find the full migration name, run:
```
dotnet ef migrations list --project deeplynx.datalayer --startup-project deeplynx.api
```

2. Use the full name to run this command (replacing <Full_Migration_Name>):
```
dotnet ef database update <Full_Migration_Name> -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
```

3. Run the remove command. This will delete the migration file and update the snapshot accordingly. This 
will remove the most recent migration, so the number of remove commands should be equal to the number of migrations 
you want to remove.
```
dotnet ef migrations remove -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
```
