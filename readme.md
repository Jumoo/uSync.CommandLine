# uSync Command Line library

### uSync command line for Umbraco v15+

This is the v15+ version of the uSync command line util, it uses the ManagementAPI (and the uSync Management API) to do the funky stuff without
you having to install anything on the server.

```
dotnet tool install uSync.Cli
```

## Loads of useful commands for Umbraco !

In this beta, we have plugged in the 'useful' things you might need from the command line.

rebuild indexes, models or caches, or run usync imports or exports.

```
uSync CommandLine: 17.0.0

Description:

Usage:
  uSync [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  test                     Test command
  user-current             Fetch the user executing the commands
  user-list                List all users
  cache-rebuild            Rebuild the cache on the server
  cache-reload             Reload the cache on the server
  models-rebuild           Rebuild the models on the server
  models-status            Get the status of the models on the server
  indexer-rebuild          Rebuild the index on the server
  indexer-list             List the indexes on the server
  healthcheck-list         List the health checks on the server
  healthcheck-group-list   List the health check groups on the server
  healthcheck-group-check  Run a health check on the server
  usync-settings           List all settings
  usync-import             Import all items
  usync-export             Export all items
  usync-ping               Ping the Umbraco server until it responds
```

e.g

run an import

```
uSyncCli usync-import -s https://myserver.com/ --force
```

## Create an API user

In the users section of Umbraco

![Add Api User](./img/add-api-user.png)

create the api user and add them to the relevant group

![Create API user](./img/create-api-user.png)

## Add a Client Secret and Key

Once you have created an API user , you will need to give them a client id and secret,

![client secret](./img/client-secret.png)

you will need both the client id and secret to connect via the command line.

## either add an appsetting.json or use the `-k` `-i` settings

you can add an appsettings.json to the root of the folder where
you are running the uSync command line:

```json
{
  "uSync": {
    "Command": {
      "Secret": "[CLIENT_SECRET]",
      "ClientId": "[CLIENT_ID]"
    }
  }
}
```

or you can pass these on the command line. eg.

```
uSyncCli usync-ping -s https://localhost:44359 -s [client_secret] -k [client_id]
```

## Automagic Server discovery. 
if you place the uSync:Command settings in the appsettings.json file of a site. then if you run the uSync command 
from within the site directory it will attempt to workout the server Url, 

if will check : 
  1. the command line `-s` parameter
  2. for a `uSync:Command:ServerUrl` value in appsettings
  3. for the UmbracoApplicationUrl value `Umbraco:CMS:WebRouting:UmbracoApplicationUrl`
  4. for the applicationUrl value in the `Properties\launchSettings.json` file.

if it finds one, it will use it as the server address. 

## Automagic Api User / ClientID & Secret Generation.

if the settings are stored in the sites appsettings.json (or the enviroment or a key vault). then you can
use the `uSync.Command.Setup` package to automatically setup the API user for you. 

```json
"uSync": {
    "Command": {
      "AddIfMissing": true,
      "ClientId": "[CLIENT_ID]",
      "Secret": "[CLIENT_SECRET]"
    }
  }
```

on boot the `uSync.Command.Setup` package will check to see if the clientId has been setup on your site.
if it hasn't it creates an API user with the clientId / secret. 

you can also specify `Email`, `Username` and `Name` for the API account, but if you don't some will
be generated for you. 

> [!WARNING]
> This isn't compuslary, and it does open you up to potential issues if someone gets access to your appsettings.json
> (although you might already be in trouble if this happens). 



