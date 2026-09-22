# P6 Adapter Data Source

The P6 adapter is designed to facilitate interaction between DeepLynx and Oracle's P6 Primavera P6. Its purpose is to import P6 scheduling data into DeepLynx. To learn more about the P6 adapter, check out the readme [here](https://github.inl.gov/Digital-Engineering/p6_deeplynx_adapter#p6-deep-lynx-adapter). This article will cover setting up a P6 data source in DeepLynx.

### Configured P6 Sources

You may encounter scenarios in which you want several different connections to the same server but different projects on that server, or you may want to have different data sources connecting to different servers. For ease of access on the user's part, container administrators can create pre-configured sources which auto-fill the P6 endpoint and project ID. In order to create a preconfigured source, follow these steps:

1. Navigate to the "Settings" tab under "Container Administration" in the GUI sidebar
<!-- <img width="255" alt="image" src="https://user-images.githubusercontent.com/115493430/212105370-44cddda5-61da-440d-a40d-f8de446e94ce.png"> --> <!-- This is the dark mode screenshot -->
 ![image](uploads/image-updates/P6-Data-Source/p6ds1.png)

2. Select "P6" from the "Enabled Data Source Types" dropdown. You should see a this table pop up:
<!-- <img width="1366" alt="image" src="https://user-images.githubusercontent.com/115493430/212167047-7d740983-c43f-428e-ae30-abba90b57e8d.png"> --> <!-- This is the dark mode screenshot -->
 ![image](uploads/image-updates/P6-Data-Source/p6ds2.png)

3. Click "Add New Configured Source" and choose "P6" as the data source type. 
<!-- <img width="1351" alt="image" src="https://user-images.githubusercontent.com/115493430/212182548-4a40986c-0ffc-40b2-9637-5f69973cb1bb.png"> --> <!-- This is the dark mode screenshot -->
 ![image](uploads/image-updates/P6-Data-Source/p6ds3.png)

4. Enter an alias for the configured source. This is the name that will appear when you create a new data source using this config. Enter an endpoint and a project ID, then click "Create".

Once your configuration is created, you can use it to create a new P6 data source as explained in the next section.

### Creating a P6 data source

1. Navigate to Data -> Data Sources. In the upper left-hand corner select the button that says "New Data Source"
2. Name your source and select "P6" as the source type
3. **(optional)** Choose a configured source from the drop down. If at any time during source creation you change your mind, you can select "Default P6 adapter" and restore the default (blank) options.
<!-- <img width="1307" alt="image" src="https://user-images.githubusercontent.com/115493430/212183426-c7d29dc9-f2f4-4c62-818b-de41d4d3509d.png"> --> <!-- This is the dark mode screenshot -->
 ![image](uploads/image-updates/P6-Data-Source/p6ds5.png)

4. Fill in P6 endpoint, projectID, username and password. The username and password should be credentials that can access the P6 server and project specified.
<!-- <img width="1331" alt="image" src="https://user-images.githubusercontent.com/115493430/212187368-85d9f56b-8ae5-4aac-b8b5-d9fa90af1e17.png"> --> <!-- This is the dark mode screenshot -->
 ![image](uploads/image-updates/P6-Data-Source/p6ds6.png)

5. Enable the data source to start receiving data from the P6 adapter.
6. Click "Create"

### Handling P6 Imports

Once enabled, the P6 data source will _automatically_ begin receiving data from the P6 adapter. No user intervention is required apart from enabling and disabling the data source.

Each adapter cycle, data will be sent to the source. You can check for new imports on the "Imports" tab. Once you have at least one import, you can [create type mappings to map the data](https://github.com/idaholab/Deep-Lynx/wiki/Type-Mapping). Once your type mappings are all created and enabled, data flowing to the source will be translated into nodes and edges. You can see view this data from the "graph" tab.