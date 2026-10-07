namespace StarHealth.Data.Grpc;

/// <summary>
/// One dish on the network. The factory default matches a stock Starlink LAN;
/// installations with several terminals (each on its own segment) add one
/// entry per reachable address. <see cref="Name"/> is only a local label.
/// </summary>
public sealed record DishEndpointOptions(
    string Host = "192.168.100.1",
    int DishPort = 9200,
    string Name = "Dish")
{
    public string Address => $"http://{Host}:{DishPort}";

    public string EndpointText => $"{Name} · {Host}:{DishPort}";

    public bool IsValid(out string? error) => DishEndpointValidation.Validate(Host, DishPort, out error);
}

/// <summary>Parse/validate UI input before it becomes an endpoint.</summary>
public static class DishEndpointValidation
{
    public static bool Validate(string host, int port, out string? error)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            error = "host";
            return false;
        }
        if (port is < 1 or > 65535)
        {
            error = "port";
            return false;
        }
        error = null;
        return true;
    }

    public static bool TryParsePort(string text, out int port)
        => int.TryParse(text.Trim(), out port) && port is >= 1 and <= 65535;
}

