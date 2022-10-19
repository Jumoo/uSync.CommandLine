using System.Runtime.Serialization;

namespace uSync.Handlers;
[Serializable]
internal class uSyncCommandException : Exception
{
    public int Id { get; private set; }

    public uSyncCommandException()
    { }

    public uSyncCommandException(string? message) : base(message)
    { }

    public uSyncCommandException(int id, string message)
        : base(message)
    {
        Id = id;
    }

    public uSyncCommandException(string? message, Exception? innerException)
        : base(message, innerException)
    { }

    protected uSyncCommandException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    { }
}