namespace Iris.Contracts.Brokers.Models.Amazon;

/// <summary>
/// A named AWS profile as the connection dialog needs it: what to show, and which region to
/// prefill. Nothing about how the profile gets its credentials crosses this boundary.
/// </summary>
public record AwsProfile(string Name, string? Region);
