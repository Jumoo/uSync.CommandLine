# uSync Command Line. 

This is the Command line element of the uSync command line.

To install globally on your computer :

```
Dotnet tool install uSync -g 
```

or just localls 

```
dotnet tool install uSync.Cli
```

## NOTE :
> You will need to install and configure the uSync.Commands package on your umbraco installation for the command line to work!

## Running commands

### To see the command help 

```
uSync -? 
```

## List commands on a remote server 
You can use the command line to connect to a server and list the avalible commands

```
uSync list https://myserver.com/umbraco -user <username> -pass <password>
```

depending on what is installed - you will see a list of commands like this : 

```
  *** uSync Command Line ***

Remote commands available for https://localhost:44382/umbraco :

  Info                 : Information about the Umbraco installation
  Ping                 : Ping a server, returns true when server is ready
  Rebuild-DbCache      : Rebuilds the database cache (Expensive)
  Rebuild-Index        : Rebuilds an examine index
  Reload-MemCache      : Reloads the in-memory cache
  Test                 : A test command, to check things work
  uSync-Export         : Run an uSync export
  uSync-Import         : Run an uSync import
  uSync-Pull           : Pull updates via uSync.Publisher
  uSync-Push           : Push updates via uSync.Publisher
  uSync-Report         : Run a uSync report

For specific information on a single command :
   > uSync list <command-name> ...
To run a command :
   > uSync run <command-name> ...
```

### Run a command on a remote server 
to run a command issue the uSync run with the relavant parameters

```
uSync run [command] -p [parameters] https://my-server/umbraco -user <username> -pass <password>
```

### Add new commands 
See the uSync.Commands and uSync.Commands.Core code for examples of how to write addtional commands for the command line.


