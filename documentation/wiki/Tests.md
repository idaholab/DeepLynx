DeepLynx uses [Mocha.js](https://mochajs.org/) for its test framework and [Chai.js](https://www.chaijs.com/) for its assertion library. The DeepLynx test library can be found under `src->tests`. The tests folder reflects the structure of the data warehouse, containing tests in the following areas:

- `access_management`: tests regarding container invites, authorization, users and API keys
- `data_warehouse`: tests pertaining to the actual data warehouse, with the subcategories `data`, `etl`, `export`, `import`, and `ontology`. 
- `event_system`: tests for the [event system](Creating-and-Listening-to-Events).
- `graphql`: tests regarding schema generation and querying in graphql. For more information about the DeepLynx implementation of GraphQL, check out the article [here](Querying-Data-with-GraphQL).
- `services`: tests regarding various [services](Services) which ship with DeepLynx

The `tests` folder also comes with its own `.env` environment file to configure things such as a container ID specifically for testing, as well as skipping certain tests.

## Writing Tests

There are a few elements of writing tests for DeepLynx that are very important to note. Tests are written in the following format:
```typescript
describe('The thing being tested', async() => {
    before(async function () {
        await PostgresAdapter.Instance.init();
        //... do these things to set up tests
    });
    after(async function () {
        //... do these things to clean up
        return PostgresAdapter.Instance.close();
    });
    it('can perform this task', async() => {
        //... do the task
        expect(the_task).to.equal('done');

        return Promise.resolve();
    });
})
```
The `before` and `after` blocks are an important part of setting up the testing environment while allowing the individual `it` functions to remain simple and readable.

In the `before` function, it is good practice to open a new database connection and then set up the data structures and users that are needed to perform your test. You can initialize a database connection with the line `await PostgresAdapter.Instance.init();`.

When a database connection is made in the before function, it is **super important** that you close the connection in the after function once the tests have completed. The after function can also be used to delete any data structures or users that were created in the before function. Be sure that all of your data structures and users are deleted, and then execute the command `return PostgresAdapter.Instance.close();` to close the database connection. Again, this is **very important** as leaving database connections open could quickly crowd out and slow down the database.

DeepLynx uses the [Expect style of assertion](https://www.chaijs.com/guide/styles/#expect) for its testing. For more information on assertion language chaining, click [here](https://www.chaijs.com/api/bdd/).

## Running Tests

In order to run the tests, open up your command line and type the following command: `npm run test`. This will run all the tests and return a report in the terminal regarding which tests failed and which tests failed, why tests failed if they did, and all the files that were involved in the testing process. The result of the example code block above would look something like this if the test passes.

```
The thing being tested
    ✔ can perform this task
```