namespace Iris.Cloud.Demo.Contracts
{
    public record SimpleRecordMessage
    {
        public string Name { get; set; }

        public int Count { get; set; }

        public string Value { get; set; }

        public DateTime When { get; set; }
    }
}

