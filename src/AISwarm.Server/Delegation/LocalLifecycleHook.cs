using System.Net;
using System.Net.Http.Json;

namespace AISwarm.Server.Delegation;

public class LocalLifecycleHook
{
    private readonly HttpClient _client;
    private readonly Uri? _endpoint;

    public LocalLifecycleHook(
        HttpClient client,
        string? endpoint = null)
    {
        _client = client;
        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address)
            || !IPAddress.IsLoopback(address)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Lifecycle hook must use an HTTP(S) loopback IP without credentials.", nameof(endpoint));
        _endpoint = uri;
    }

    public async Task NotifyAsync(
        DelegatedTask snapshot,
        CancellationToken cancellationToken)
    {
        if (_endpoint == null)
            return;
        using var response = await _client.PostAsJsonAsync(_endpoint, new
        {
            snapshot.TaskId,
            snapshot.State,
            snapshot.ResultUri,
            snapshot.ExitCode
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
