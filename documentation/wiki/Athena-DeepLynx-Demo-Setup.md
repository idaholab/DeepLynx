### Setup

To set up a dockerized DeepLynx instance:

1. Install the latest version of Docker
2. Clone the DeepLynx [repository](https://github.com/idaholab/Deep-Lynx/tree/development)
3. Navigate to the DeepLynx repository in your terminal/command line/Powershell
4. Type `docker compose pull` and wait for the process to finish
5. Type `docker compose up --attach deep-lynx` and hit enter
6. To terminate hit Cntrl-C or Cntrl-D

To set up the Athena DeepLynx container:

1. Download the files found [here](https://github.com/user-attachments/files/17192043/AthenaDemoFiles.zip). The files are as follows:
  - `AthenaDT.owl`: ontology file
  - `Athena DT_Container_Export.json`: data source/type mapping file
  - `bt_equipment 1.csv`: graph data file
  - `AMUSE_scenario_0.xlsx`, `AMUSE_scenario_1.xlsx`, and `AMUSE_scenario_2.xlsx`: amuse output files
  - `tag_athena.py`: a python file to properly tag the nodes and files for the demo
2. Create a new DL container with the `AthenaDT.owl` ontology
3. Under "Container Administration"/"Import Container", check the "Data Sources" and "Type Mapping and Transformations" box and import the `Athena DT_Container_Export.json` file
4. Enable the Data Source ("Data Management"/"Data Sources") and all the mapping checkboxes ("Data Management"/"Type Mappings")
5. Under the "Data Management"/"Import Data" page, upload `bt_equipment 1.csv`
6. Navigate to "Data"/"Data Viewer". You should see a graph like this:
  - <img width="1380" alt="image" src="https://github.com/user-attachments/assets/d04cc06d-c3be-439e-a8f1-da0f315e64a2">
7. Under "Data"/"File Viewer", upload each of the `AMUSE_scenarion_#` files.
8. In order for the Python script to operate correctly, you will need two pieces of information- an API auth token and a container ID.
    1. To get the API auth token, view this [wiki article](https://github.com/idaholab/Deep-Lynx/wiki/Generating-and-Using-API-Keys-Token). 
        1. Use [method 1](https://github.com/idaholab/Deep-Lynx/wiki/Generating-and-Using-API-Keys-Token#method-1) to generate your key/secret
        2. Use steps 1-3 of [this section](https://github.com/idaholab/Deep-Lynx/wiki/Generating-and-Using-API-Keys-Token#using-an-api-keysecret-pair) to generate your auth token.
    2. Container ID is visible in the bottom left corner of the UI. For example, the container from the screenshot above is container 2.
9. Once you have your auth token and container ID, put them into the TODO lines from the Python code:
  - <img width="355" alt="image" src="https://github.com/user-attachments/assets/162747df-56c6-45f1-8c55-4765cca74f18">
10. Run the Python code. You should see an output like this (there may be some SSL warnings before this output; you can safely ignore those):
  - <img width="273" alt="image" src="https://github.com/user-attachments/assets/5d96f88e-b73e-4d0c-ae31-0f61cccc6cf0">

### Relevant DeepLynx endpoints (for querying from Unity)

- List Tags: GET `localhost:8090/containers/:container_id/graphs/tags`
  - you want to retrieve `id` (tag_id) for future queries
- List Files for a tag: GET `localhost:8090/containers/:container_id/graphs/tags/:tag_id/files`
  - you want to retrieve `id` (file_id) for future queries
- Download file (used for AMUSE data): GET `localhost:8090/containers/:container_id/files`
- List Nodes for a tag (used for flashing instrumentation): GET `localhost:8090/containers/:container_id/graphs/tags/:tag_id/nodes`
