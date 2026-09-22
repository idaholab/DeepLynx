DeepLynx uses [Casbin](https://casbin.org/) to handle its user authorization needs. DeepLynx uses a domain based role based access control (DRBAC) method. 

The followings roles exist per container.

### Admin
This role allows a user to perform any and all operations inside a container. This can be viewing or editing data as well as managing other users assigned to that particular container. This will also allow the user to access container, data source, and data export settings.

### Editor
Editors can both see and manipulate data and the ontology. An editor can not, however, edit any settings or manage data sources.

### User
A user can only see data and the ontology.

