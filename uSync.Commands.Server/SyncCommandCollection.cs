using Umbraco.Cms.Core.Composing;

using uSync.Commands.Core;

namespace uSync.Commands.Server;
public class SyncCommandCollection
    : BuilderCollectionBase<ISyncCommand>
{
    public SyncCommandCollection(Func<IEnumerable<ISyncCommand>> items)
        : base(items)
    { }

    public ISyncCommand? FindCommand(string id)
        => this.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}

public class SyncCommandCollectionBuilder
    : WeightedCollectionBuilderBase<SyncCommandCollectionBuilder,
        SyncCommandCollection, ISyncCommand>
{
    protected override SyncCommandCollectionBuilder This => this;
}
