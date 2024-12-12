# uSync Command Line library

### uSync command line for Umbraco v15+

This is the v15 version of the uSync command line util, it uses the ManagementAPI (and the uSync Maanagement API) to do the funky stuff without
you having to install anything on the server.

```
dotnet tool install uSync.Cli --version 15.0.0-beta1
```

## Loads of useful commands for umbrco !

In this beta, we have plugged in the 'useful' things you might need from the command line.

rebuild indexes, models or caches, or run usync imports or exports.

```
uSync CommandLine: 15.0.0-beta1

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
usync-ping -s https://localhost:44359 -s [client_secret] -k [client_id]
```
