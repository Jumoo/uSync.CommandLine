﻿using uSync.Commands.Core;

namespace uSync.Services;
public interface IRemoteRuntimeService
{
    Uri Uri { get; }

    Task<SyncCommandResponse?> ExecuteCommandAsync(string? command, IEnumerable<string>? parameters, TextWriter writer);
    Task<IReadOnlyCollection<SyncCommandInfo>> ListCommandsAsync(string? commandName);
}