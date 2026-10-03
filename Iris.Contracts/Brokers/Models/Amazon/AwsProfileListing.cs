namespace Iris.Contracts.Brokers.Models.Amazon;

/// <summary>
/// The profiles found, and whether they could be read at all. The two are kept apart because
/// "you have no profiles" and "your profile files have an error" need different words, and
/// only the first is true of an empty list.
/// </summary>
public record AwsProfileListing(IReadOnlyList<AwsProfile> Profiles, bool Readable)
{
    public static AwsProfileListing Unreadable { get; } = new([], Readable: false);
}
