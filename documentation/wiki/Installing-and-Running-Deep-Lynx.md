The easiest way to get DeepLynx up and running is to use Docker. Docker allows for easily reproducible builds and the majority of configuration tasks and database migrations are handled for you.

1. Install the latest version of [Docker](https://www.docker.com/)
2. Clone the DeepLynx repository
3. Navigate to the DeepLynx repository in your terminal/command line/Powershell
4. Type `docker compose pull` and wait for the process to finish
5. Type `docker compose up --attach deep-lynx` and hit enter
6. To terminate hit Cntrl-C or Cntrl-D

The initial startup might take a while as the operation must first fetch the pre-built containers from the internet. If you need to change any configuration values edit the `.docker-env` file included in your repository.

**Note**: As an alternative option for developers, you can build DeepLynx manually by following the instructions found [here](Building-DeepLynx).