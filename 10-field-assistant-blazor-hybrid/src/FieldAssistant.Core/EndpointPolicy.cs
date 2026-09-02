namespace FieldAssistant.Core;

public static class EndpointPolicy
{
    public static Uri BuildAgentRunUri(string baseAddress)
    {
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Enter an absolute HTTP or HTTPS API address.", nameof(baseAddress));
        }

        if (endpoint.Scheme == Uri.UriSchemeHttp && !endpoint.IsLoopback)
        {
            throw new ArgumentException("Remote API addresses must use HTTPS.", nameof(baseAddress));
        }

        var normalizedBase = new Uri(endpoint.GetLeftPart(UriPartial.Authority) + "/");
        return new Uri(normalizedBase, "api/agents/run");
    }
}
