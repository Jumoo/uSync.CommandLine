using System.Text.Json;

namespace Umbraco.Management.Api
{

    public partial class UmbracoClient
    {
        static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
            => settings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        // this class is missing from the generated code ... just add an empty class, we won't need it
        public class FileResponse { }
    }
}


namespace uSync.Management.Api {

    public partial class uSyncClient
    {
        static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
            => settings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        // this class is missing from the generated code ... just add an empty class, we won't need it
        public class FileResponse { }
    }
}