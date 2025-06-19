namespace Iris.Contracts.Results
{
    public class Failure<T> : Result<T>
    {
        public Failure(string message) : base(true, message, default!)
        {
        }
    }
}

