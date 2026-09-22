**Installation Gotchas**

* If you have installed a version of PostgreSQL and are attempting to use the Docker version of PostgreSQL included in this repository, you will need to ensure that your local copy of Postgres is not currently running. It will prioritize itself over the Docker PostgreSQL.

* If you're planning on uploading actual files you **must** have the `FILE_STORAGE_METHOD` set in your `.env` file. If you're using the `filesystem` storage method, ensure that you have also set `FILESYSTEM_STORAGE_DIRECTORY` and that it points to a directory that the DL instance has read/write permissions to.

* If you are having difficulties authenticating in the Admin Web GUI (when using it in a non-bundled moded) - ensure that you've setup the OAuth Application correctly and that the Admin Web GUI is configured correctly. Find more information [here](Admin-UI).

* When an attempt to access the bundled admin web GUI at `{{your base URL}}` fails, ensure that you have run `npm run build:dev` prior to running `npm run start`.

* When using the Dockerfile you must ensure you update the environment variables inside the Dockerfile itself with the proper values.

* When developing the AdminWebApp, be sure to open the AdminWebApp folder in vscode as an individual project (instead of opening the DeepLynx folder) so that you can take advantage of intellisense. This also gets rid of many "errors" that the IDE detects. 

* In order to get Email working correctly, you must fill out at least some of the environment variables dictating to DeepLynx what SMTP server to use. Below find the environment variables you need to set.
```
# SMTP Mail server specific settings
SMTP_USERNAME=
SMTP_PASSWORD=
SMTP_HOST=
SMTP_PORT=25
SMTP_TLS=true

# SMTP OAuth2 settings
SMTP_CLIENT_ID=
SMTP_CLIENT_SECRET=
SMTP_REFRESH_TOKEN=
SMTP_ACCESS_TOKEN=
```

* If you are on some sort of encrypted network, you may encounter an error similar to the following when attempting to set up any rust libraries: `warning: spurious network error... SSL connect error... The revocation function was unable to check revocation for the certificate.` This can be solved by navigating to your root cargo config file (`~/.cargo/config.toml`) file and adding the following lines. If you do not have an existing config.toml file at your root `.cargo` directory, you will need to make one:

```
# in ~/.cargo/config.toml
[http]
check-revoke = false
```