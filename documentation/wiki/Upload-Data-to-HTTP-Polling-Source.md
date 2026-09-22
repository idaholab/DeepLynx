A HTTP Data Source, you need only wait for the system to automatically poll the configured endpoint and load the data. 

Once data has been uploaded it is stored as an Import in the “imports” table. 

Each Import contains the data, status, and data source that data belongs to. Data is stored in a column prefixed by the data type – e.g. json_data or csv_data. 

Once an Import has been created and the data stored, automated processes will parse the original data payload into individual data chunks. Each chunk is stored in the “data_staging” table as a JSON object. In order to process non-JSON data, a processor should be created that is called when an Import is converted from single record to data chunks and inserted into “data_staging”. 

Currently, only the JSON processor is active. It will automatically, using database triggers and cron jobs, take and chunk the data from an Import into data_staging.