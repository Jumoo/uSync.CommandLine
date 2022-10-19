using Umbraco.Extensions;

namespace uSync.Commands.Core.Extensions;
public static class SyncCommandParameterExtensions
{
    public static SyncCommandParameter? FindParameter(this IReadOnlyCollection<SyncCommandParameter> parameters, string name)
       => parameters.FirstOrDefault(x => x.Id != null && x.Id.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static SyncCommandParameter? FindParameter(this SyncCommandRequest request, string name)
        => request.Parameters?.FindParameter(name);

    public static TResult GetValue<TResult>(this SyncCommandParameter? parameter, TResult defaultValue)
    {
        if (parameter == null) return defaultValue;

        var attempt = parameter.Value.TryConvertTo<TResult>();
        return attempt.ResultOr(defaultValue);
    }

    public static TResult GetParameterValue<TResult>(this SyncCommandRequest request, string name, TResult defaultValue)
        => request.FindParameter(name).GetValue(defaultValue);

    public static TResult GetParameterValue<TResult>(this IReadOnlyCollection<SyncCommandParameter> parameters, string name, TResult defaultValue)
        => parameters.FindParameter(name).GetValue(defaultValue);
}
