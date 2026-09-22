The DeepLynx HTTP Server provides users with front-facing API routes and middleware in order to perform various tasks as they pertain to the data warehouse. Everything related to the HTTP server can be found in `src->http_server`. The sections below describe the code resources available within the HTTP server.

## `assets` and `views`
These sub-folders are used to build the UI for the frontend user's interactions with the server

## `authentication`
Contains the code to perform authentication tasks, such as generating tokens and certs.

## `server.ts`
Contains the code to run the server. The class `Server` found in this file is a singleton and wraps the express.js library and application.

## Middleware (`middleware.ts` and `routes/router.ts`)
The `middleware.ts` file contains functions which handle authentication, as well as various context functions which attempt to fetch class instances by id. The `router.ts` file from the `routes` sub-folder conatins the proper mounting configuration for each grouping of routes, allowing the routes to call upon functions from the `middleware.ts` file. Middleware is then loaded into each endpoint call using the spread operator `...middleware`. For more information on routes and endpoints, see the section below.

## `routes`

The `routes` folder contains API routes and their resolvers for performing various functions, organized in a manner that reflects the rest of the application as follows:

- `access_management`: routes regarding authentication, users, api keys and interactions with the rest of the system
- `data_warehouse`: these routes reflect the majority of the system, and are further organized into `data`, `etl`, `export`, `import`, and `ontology`. 
- `event_system`: routes representing the internal and external facing event system of DeepLynx
- `task_runner`: routes associated with creating, retrieving, and updating tasks in DeepLynx

Each file in this structure contains the route groupings pertaining to the data structure at hand. For example, `metatype_routes.ts` (under `ontology`) contains the routes necessary for performing CRUD (create, read, update, and delete) operations on classes (sometimes referred to as metatypes), as well as their validation. These routes contain the necessary middleware to execute these functions, and each is associated with one or more repository functions which help validate and translate the data. You can learn more about repositories [here](Repository-Pattern).