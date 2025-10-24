using NJsonSchema.CodeGeneration.CSharp;

using NSwag;
using NSwag.CodeGeneration.CSharp;

using System.Xml.Linq;

// the Umbraco host (change this to fit your custom setup)
const string host = "https://localhost:44359";

const string mangementSwagger = $"{host}/umbraco/swagger/management/swagger.json";
const string uSyncSwagger = $"{host}/umbraco/swagger/uSync/swagger.json";

await GenerateClient(mangementSwagger, "Umbraco.Management.Api", "UmbracoClient");
await GenerateClient(uSyncSwagger, "uSync.Management.Api", "uSyncClient");
async Task GenerateClient(string url, string nameSpace, string className)
{
    // fetch the OpenAPI spec for the Management API
    using var client = new HttpClient();
    var json = await client.GetStringAsync(url);

    // parse the OpenAPI spec
    var document = await OpenApiDocument.FromJsonAsync(json);

    // settings for the generated API client code
    var settings = new CSharpClientGeneratorSettings
    {
        ClassName = className,
        CSharpGeneratorSettings =
        {
            Namespace = nameSpace,
            JsonLibrary = CSharpJsonLibrary.SystemTextJson,
        }
    };

    // generate the API client and write the code to disk
    var generator = new CSharpClientGenerator(document, settings);
    var code = generator.GenerateFile();



    Directory.CreateDirectory("../uSync.Commands/Client/");
    File.WriteAllText($"../uSync.Commands/Client/{className}.Generated.cs", code);
}
