namespace Iris.Contracts.Brokers.Models.Amazon
{

    public class RegionEndpoint
    {
        public string SystemName { get; private set; }
        public string DisplayName { get; private set; }

        public RegionEndpoint()
        {
            SystemName = ""; DisplayName = "";
        }

        private RegionEndpoint(string systemName, string displayName)
        {
            SystemName = systemName;
            DisplayName = displayName;
        }

        public static readonly RegionEndpoint USEast1 = new("us-east-1", "US East 1 (N. Virginia)");
        public static readonly RegionEndpoint USEast2 = new("us-east-2", "US East 2 (Ohio)");
        public static readonly RegionEndpoint USWest1 = new("us-west-1", "US West 1 (N. California)");
        public static readonly RegionEndpoint USWest2 = new("us-west-2", "US West 2 (Oregon)");
        public static readonly RegionEndpoint AFSouth1 = new("af-south-1", "Africa 1 (Cape Town)");
        public static readonly RegionEndpoint APEast1 = new("ap-east-1", "Asia Pacific 1 (Hong Kong)");
        public static readonly RegionEndpoint APNortheast1 = new("ap-northeast-1", "Asia Pacific 1 (Tokyo)");
        public static readonly RegionEndpoint APNortheast2 = new("ap-northeast-2", "Asia Pacific 2 (Seoul)");
        public static readonly RegionEndpoint APSouth1 = new("ap-south-1", "Asia Pacific 1 (Mumbai)");
        public static readonly RegionEndpoint APSoutheast1 = new("ap-southeast-1", "Asia Pacific 1 (Singapore)");
        public static readonly RegionEndpoint APSoutheast2 = new("ap-southeast-2", "Asia Pacific 2 (Sydney)");
        public static readonly RegionEndpoint CACentral1 = new("ca-central-1", "Canada 1 (Central)");
        public static readonly RegionEndpoint EUCentral1 = new("eu-central-1", "Europe 1 (Frankfurt)");
        public static readonly RegionEndpoint EUNorth1 = new("eu-north-1", "Europe 1 (Stockholm)");
        public static readonly RegionEndpoint EUWest1 = new("eu-west-1", "Europe 1 (Ireland)");
        public static readonly RegionEndpoint EUWest2 = new("eu-west-2", "Europe 2 (London)");
        public static readonly RegionEndpoint SAEast1 = new("sa-east-1", "South America 1 (Sao Paulo)");
        public static readonly RegionEndpoint CNNorth1 = new("cn-north-1", "China 1 (Beijing)");
        public static readonly RegionEndpoint CNNorthWest1 = new("cn-northwest-1", "China 1 (Ningxia)");
        public static readonly RegionEndpoint USGovCloudEast1 = new("us-gov-east-1", "AWS GovCloud 1 (US-East)");
        public static readonly RegionEndpoint USGovCloudWest1 = new("us-gov-west-1", "AWS GovCloud 1 (US-West)");
        public static readonly RegionEndpoint USIsoEast1 = new("us-iso-east-1", "US ISO East 1");
        public static readonly RegionEndpoint USIsoWest1 = new("us-iso-west-1", "US ISO West 1");
        public static readonly RegionEndpoint USIsobEast1 = new("us-isob-east-1", "US ISOB East 1 (Ohio)");
        public static readonly RegionEndpoint EUIsoeWest1 = new("eu-isoe-west-1", "EU ISOE West 1");


        public static readonly IReadOnlyDictionary<string, RegionEndpoint> Regions = new Dictionary<string, RegionEndpoint>
    {
        { USEast1.SystemName, USEast1 },
        { USEast2.SystemName, USEast2 },
        { USWest1.SystemName, USWest1 },
        { USWest2.SystemName, USWest2 },
        { AFSouth1.SystemName, AFSouth1 },
        { APEast1.SystemName, APEast1 },
        { APNortheast1.SystemName, APNortheast1 },
        { APNortheast2.SystemName, APNortheast2 },
        { APSouth1.SystemName, APSouth1 },
        { APSoutheast1.SystemName, APSoutheast1 },
        { APSoutheast2.SystemName, APSoutheast2 },
        { CACentral1.SystemName, CACentral1 },
        { EUCentral1.SystemName, EUCentral1 },
        { EUNorth1.SystemName, EUNorth1 },
        { EUWest1.SystemName, EUWest1 },
        { EUWest2.SystemName, EUWest2 },
        { SAEast1.SystemName, SAEast1 },
        { CNNorth1.SystemName, CNNorth1 },
        { CNNorthWest1.SystemName, CNNorthWest1 },
            { USGovCloudEast1.SystemName, USGovCloudEast1 },
    { USGovCloudWest1.SystemName, USGovCloudWest1 },
    { USIsoEast1.SystemName, USIsoEast1 },
    { USIsoWest1.SystemName, USIsoWest1 },
    { USIsobEast1.SystemName, USIsobEast1 },
    { EUIsoeWest1.SystemName, EUIsoeWest1 },
    };
    }
}