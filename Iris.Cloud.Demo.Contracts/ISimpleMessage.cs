namespace Iris.Cloud.Demo.Contracts;

public interface ISimpleMessage
{
    public string Name { get; set; }

    public int Count { get; set; }

    public string Value { get; set; }

    public DateTime When { get; set; }
}

