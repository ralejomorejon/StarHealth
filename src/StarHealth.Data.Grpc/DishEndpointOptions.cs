namespace StarHealth.Data.Grpc;

/// <summary>Dish gRPC endpoint. Defaults match a stock Starlink LAN.</summary>
public sealed record DishEndpointOptions(
    string Host = "192.168.100.1",
    int DishPort = 9200)
{
    public string Address => $"http://{Host}:{DishPort}";
}

