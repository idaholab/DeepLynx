# Authentication Methods and Configuration
DeepLynx secures its HTTP endpoints in a few different ways. Below is a brief overview of DeepLynx's security measures. These measures are configured using environment variables - see the `.env-sample` file for more information.

## Basic Authentication (`basic`)

It is highly recommended that you only enable Basic Authentication for local development purposes. There is no way to track individual users or to implement access control using this method. All methods and routes will be accessible to anyone possessing the proper username and password. Basic authentication is not considered a secure production method.

1. Set `AUTH_STRATEGY` environment variable to `basic` (either using the `.env` file or the deployment target's own environment)
2. Set `BASIC_USER` and `BASIC_PASSWORD` to desired values

## Token (`token`)
Token authentication relies on the user submitting a JSON Web Token with each request. Said JWT is retrieved by using one of the authentication methods below. Once a user is authenticated and a JWT has been retrieved, subsequent requests against DeepLynx must be sent with an `Authorization` header with the JWT as a bearer token.

1. Set `AUTH_STRATEGY` environment variable to `token` (either using the `.env` file or the deployment target's own environment)
2. Set all required environment variables for `token` - namely the environment variables for the encryption key path or secret and SAML variables (if using SAML).

### SAML for Active Directory Federation Services (`saml-adfs`) Configuration

In order to enable SAML authentication for active directory federation services, the application will need access to a self signed certificate private key (certificate must be previously registered), and the x509 certificate for SAML authentication located in the ADFS's metadata document. Instructions for acquiring each are below.

1. **Self-signed certificate private key**: There are a few methods by which you can accomplish this. First, you could use [OpenSSL](https://www.openssl.org/) to generate a certificate and its private key at the same time - this is probably the easiest way and I've included a bash script (`src/authentication/generate-cert.sh`) which will do this for you on *nix based systems. If you are on Windows, you could also use [OpenSSL](https://www.openssl.org/) or you can use the PowerShell script `CertsDeLynx.ps1`'s `Get-Self-Cert-Key` function. *Note: you must still [export the private key](https://www.mysysadmintips.com/windows/servers/62-export-private-key-and-certificate-from-iis-pfx-file) using Windows certificate management tools yourself.*
2. **x509 Certificate from ADFS Metadata**: In order to verify your identity provider's responses you must have access to their public x509 certificate. Most ADFS services provide a HTTP endpoint serving a `metadata.xml` document which will contain the x509 certificate for SAML 2.0 authentication services. In order to facilitate the retrieval of this key I've included both a bash script (`get-adfs-cert`) and a PowerShell function (`CertDeLynx.ps1`'s `Get-Cert-From-Idp`) to help you retrieve it. The certificate will be saved to a `.crt` file.


Quick example of the PowerShell script
```shell script
$FederationMetatdataUrl = "adfs metadata url"
.\CertsDeLynx.ps1; Get-Cert-From-IdP ((new-object System.Net.WebClient).DownloadString($FederationMetadataUrl))
```

Once you have those two pieces you're ready to configure the application. 

1. Set `AUTH_STRATEGY` environment variable to `token` (either using the `.env` file or the deployment targets own environment, applies to all environment variables listed after this step)
2. Set `SAML_ADFS_ISSUER` to the application id assigned when you registered this application with the ADFS service 
3. Set `SAML_ADFS_CALLBACK` to the URL the Identity Provider will send the user to after a successful authentication (this is generally registered with the Identity Provider beforehand)
4. Set `SAML_ADFS_PRIVATE_CERT_PATH` to the absolute path for your self-signed certificate's private key
5. Set `SAML_ADFS_PUBLIC_CERT_PATH` to the downloaded x509 certificate from the ADFS metadata document


*Note: This document assumes that you have configured the Identity Provider service correctly. It is out of scope to provide information on how to do that here. Contact your Active Directory systems administrator if you need help or information on this subject.*

### RSA SecurID

*Note: This form of authentication **does not** currently integrate with any of the internal authentication methods related to users or containers.*

SecurID is an identity and access management provider. Users provide a memorized pin and temporary 6 digit token to authenticate with the RSA server. **These two tokens combined** (pin + temporary token with no spaces or other characters between) creates the **securID**. This form of authentication is currently intended for use when integrating DeepLynx with certain environments and processes, such as a High Performance Computing center that shares the same RSA SecurID system and can validate the provided credentials. 

To get set up for integration with RSA, please set the following config parameters:
- `RSA_URL`: The full url (including port) to the RSA server
- `RSA_CLIENT_KEY`: A key generated by the RSA server owner and provided to the client (this DeepLynx application) for authenticating the client
- `RSA_CLIENT_ID`: A client ID for the DeepLynx application provided by the RSA server owner

There are two possible authentication flows:  
1. Call the `initialize` route with the user's id (`subjectName`)
2. Call the `verify` route with the user's `securID`, `inResponseTo` (the message ID generated by the RSA server and included in the previous `initialize` request response), and `authnAttemptId` (an ID generated by the RSA server and included in the previous `initialize` request response).

Or,

1. Call the `initialize` route with both the user's id (`subjectName`) and `securID`. This will perform the authentication request and return the result, making `verify` unnecessary.

Additionally, the `status` route may be used to check the status of the authentication request and the `cancel` route will cancel an authentication attempt. 

For full details, see the official [RSA Guide](https://community.rsa.com/t5/securid-access-cloud/securid-authentication-api-developer-s-guide/ta-p/568534)

### OAuth Flow
See [Authenticating with DeepLynx Apps](Authenticating-with-DeepLynx-Apps)

### API Key/Secret Flow
See [Generating and Using API Keys](Generating-and-Using-API-Keys-Token)
