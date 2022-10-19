using System.Diagnostics;

using CSharpTest.Net.IO;

using Lucene.Net.Util;

using Newtonsoft.Json;

using Org.BouncyCastle.X509;

using Umbraco.Cms.Core.Hosting;
using Umbraco.Extensions;

using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Controllers;
using uSync.BackOffice.SyncHandlers;
using uSync.Commands.Core;
using uSync.Commands.Core.Extensions;

namespace uSync.Commands;

/// <summary>
///  base for all uSync Commands 
/// </summary>
/// <remarks>
///  these are command that actually call uSync - not commands in uSync command line.
/// </remarks>
public abstract class uSyncCommandBase : SyncCommandBase
{
    protected readonly SyncHandlerFactory syncHandlerFactory;
    protected readonly uSyncConfigService configService;
    protected readonly uSyncService uSyncService;
    private readonly string _tempPath;

    protected uSyncCommandBase(
        SyncHandlerFactory syncHandlerFactory,
        uSyncConfigService configService,
        uSyncService uSyncService,
        IHostingEnvironment hostingEnvironment)
    {
        this.syncHandlerFactory = syncHandlerFactory;
        this.configService = configService;
        this.uSyncService = uSyncService;

        _tempPath = Path.Combine(hostingEnvironment.LocalTempPath, "uSync", "Remote");
    }

    public override SyncCommandInfo GetCommand()
    {
        var command = base.GetCommand();

        command.Parameters = new List<SyncCommandParameterInfo>
        {
            new SyncCommandParameterInfo
            {
                Id = "set",
                Description = $"Handler set to use (default = '{configService.Settings.DefaultSet}')",
                ParameterType = SyncCommandObjectType.String
            },
            new SyncCommandParameterInfo
            {
                Id = "groups",
                Description = $"Handler groups to use (default = '{configService.Settings.UIEnabledGroups}')",
                ParameterType = SyncCommandObjectType.String
            },
            new SyncCommandParameterInfo
            {
                Id = "folder",
                Description = $"Folder to use for uSync files (default: '{configService.GetRootFolder()}'",
                ParameterType = SyncCommandObjectType.String
            },
            new SyncCommandParameterInfo
            {
                Id = "Verbose",
                Description = $"Return all the details of the action",
                ParameterType = SyncCommandObjectType.Bool
            }
        };

        return command;
    }

    public override async Task<SyncCommandResponse> Execute(SyncCommandRequest request)
    {
        var set = request.GetParameterValue("set", configService.Settings.DefaultSet);
        var groups = request.GetParameterValue("groups", configService.Settings.UIEnabledGroups);
        var force = request.GetParameterValue("force", false);
        var folder = request.GetParameterValue("folder", configService.GetRootFolder());
        var verbose = request.GetParameterValue("verbose", false);

        // the work
        var handlers = GetHandlerAliases(set, groups, action).ToList();

        if (handlers.Count > request.Page)
        {
            var currentHandler = handlers[request.Page];

            var sw = Stopwatch.StartNew();
            var result = await RunHandlerCommand(currentHandler, set, folder, force);
            sw.Stop();

            SaveActions(request.Id, result.Actions);


            object? resultObject = null;

            var complete = request.Page + 1 >= handlers.Count;
            if (complete)
            {
                var actions = LoadActions(request.Id);
                RemoveTempFile(request.Id);

                if (verbose)
                {
                    resultObject = actions;
                }
                else
                {
                    resultObject = new
                    {
                        Type = action.ToString(),
                        Changes = actions.CountChanges(),
                        Total = actions.Count()
                    };
                }
            }

            return new SyncCommandResponse(request.Id, true)
            {
                Complete = request.Page + 1 >= handlers.Count,
                Message = $"Ran {currentHandler,-30}. {action} {result.Actions.CountChanges(),3} / {result.Actions.Count(),3} ({sw.ElapsedMilliseconds}ms)",
                Result = resultObject
            };
        }

        return new SyncCommandResponse(request.Id, false)
        {
            Message = $"Page {request.Page} out of range for handlers {handlers.Count}"
        };
    }

    private IEnumerable<string> GetHandlerAliases(string set, string group, HandlerActions actions)
    {
        return syncHandlerFactory.GetValidHandlers(new SyncHandlerOptions
        {
            Set = set,
            Group = group,
            Action = actions
        }).Select(x => x.Handler.Alias);
    }

    private void SaveActions(Guid id, IEnumerable<uSyncAction> actions)
    {
        var existing = LoadActions(id);
        existing.AddRange(actions);
        var json = JsonConvert.SerializeObject(existing);

        var tempFileName = GetTempFileName(id);
        Directory.CreateDirectory(_tempPath);

        File.WriteAllText(tempFileName, json);
    }

    private IList<uSyncAction> LoadActions(Guid id)
    {
        var tempFileName = GetTempFileName(id);
        if (File.Exists(tempFileName))
        {
            try
            {
                var json = File.ReadAllText(GetTempFileName(id));
                if (json != null)
                {
                    return JsonConvert.DeserializeObject<List<uSyncAction>>(json) ?? new List<uSyncAction>();
                }
            }
            catch
            {
                // error reading file 
            }
        }

        return new List<uSyncAction>();
    }

    private void RemoveTempFile(Guid id)
    {
        try
        {
            var tempFile = GetTempFileName(id);
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
        catch { }
    }

    private string GetTempFileName(Guid id)
        => Path.Combine(_tempPath, id.ToString() + ".json");

    protected abstract HandlerActions action { get; }
    protected abstract Task<SyncActionResult> RunHandlerCommand(string handler, string set, string folder, bool force);

}
