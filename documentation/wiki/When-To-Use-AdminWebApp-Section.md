DeepLynx ships with a Vue single page application which serves as the primary UI for the DeepLynx system. While you can run this [separately](https://gitlab.software.inl.gov/b650/Deep-Lynx/-/wikis/Administration-Web-App-Installation) (and it's recommended to do so if you're developing it) we suggest you use `npm run build:web` and `npm run start` to build and deploy the included Vue app alongside DeepLynx. This process may take a few minutes each time.

If for whatever reason this is not working correctly for you, or you are doing development work on the Vue single page application itself, then this section will walk you through how to launch the bundled web app separately and register it with DeepLynx correctly.

 * [Requirements](Requirements)
 * [Installation](Administration-Web-App-Installation)