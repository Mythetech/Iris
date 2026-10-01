namespace Iris.Desktop.Infrastructure.ShellEnvironment;

public static class AwsEnvironmentDefaults
{
    public const string Ec2MetadataDisabled = "AWS_EC2_METADATA_DISABLED";

    /// <summary>
    /// The AWS SDK's default credential chain ends by asking the EC2 instance metadata
    /// service, which on a desktop is an address nothing answers. With no credentials
    /// configured that wait is what the user sees: 5.8 seconds measured, against 3 ms with the
    /// lookup switched off. Anyone who has set the variable either way keeps their choice.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Select(IReadOnlyDictionary<string, string> current)
    {
        var selected = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!current.TryGetValue(Ec2MetadataDisabled, out var existing) || string.IsNullOrEmpty(existing))
            selected[Ec2MetadataDisabled] = "true";

        return selected;
    }
}
