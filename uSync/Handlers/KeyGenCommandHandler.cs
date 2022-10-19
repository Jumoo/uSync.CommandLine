using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace uSync.Handlers;
internal class KeyGenCommandHandler : ISyncCommandHandler
{
    public Command? Command { get; private set; }
    
    public KeyGenCommandHandler(TextWriter stdOut)
    {
        Command = new Command("key-gen", "Generate a key to use for hmac based authentication");

        Command.SetHandler(async (context) =>
        {
            var key = GenertateAppKey();

            await stdOut.WriteLineAsync($"Key: {key}");
            await stdOut.WriteLineAsync("");

            await stdOut.WriteLineAsync("You will need to set this key in your servers appsetting.json file\n");

            await stdOut.WriteLineAsync("\"uSync\": {\n\r" +
                "  \"Commands\": {\n\r" +
                "    \"Enabled\": \"hmac\",\n\r" +
                "    \"key\": \"" + key + "\"\n\r" +
                "  }\n\r" +
                "}");

            await stdOut.WriteLineAsync();
            await stdOut.WriteLineAsync("When running commands this value should make up the -key part of the command e.g");
            await stdOut.WriteLineAsync(" > uSync run info -s servername -k " + key);
            await stdOut.WriteLineAsync();
        });
    }

    private string GenertateAppKey()
    {
        using (var random = RandomNumberGenerator.Create())
        {
            byte[] secretKeyByteArray = new byte[32]; //256 bit
            random.GetBytes(secretKeyByteArray);
            var ApiKey = Convert.ToBase64String(secretKeyByteArray);

            return ApiKey;
        }
    }
}
