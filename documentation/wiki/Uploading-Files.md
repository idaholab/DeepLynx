Users can now upload files into the DeepLynx data warehouse. At time of writing (August 2020) files will either be stored on the local filesystem, or using Azure's Blob Storage service.

### Configuration
Users must configure the `FILE_STORAGE_METHOD` environment variable and set it to be either `azure_blob` or `filesystem`. Users must set additional environment variables with each of the methods, those are described inside the `.env-sample` file at the project's root.


### How-to
1. User POSTS a multipart form to `/containers/:id/datasources/:id/files`. That multipart form can have numerous files and normal field/values attached.
2. DeepLynx separates the files from the field values.
3. DeepLynx creates a record in the `files` table with the files location on the configured storage method and some metadata about the file along with the user provided metadata (the previous field values)
4. The endpoint returns an array of File objects, which contains the newly assigned DeepLynx ID.
5. Users can query files directly from the `files` table using the GraphQL query layer.

### Including Metadata for Normal Processing
DeepLynx has the capability to attach files it's received to metadata. In order for this to function correctly, and have your files attached to the resulting nodes/edges of your metadata successfully you must follow these instructions exactly.

1. User POSTS a multipart form to `/containers/:id/datasources/:id/files`. That multipart form can have numerous files and normal field/values attached.
2. Users must include a field named "metadata". This field should contain a json file (file must be an **array of json objects**), a csv file, or xml file.
3. DeepLynx processes the "metadata" field as normal data, and attaches any files included in that same multipart form to the parsed data.
4. Once DeepLynx processes the new metadata into nodes or edges, the previously attached files to the original data are then attached to the resulting nodes/edges.
