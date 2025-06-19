namespace Iris.Contracts.Results
{
    public class Success<T> : Result<T>
    {
        public Success(T value, string message = "") : base(false, message, value)
        {
        }

        public static Success<bool> Create()
        {
            return new Success<bool>(true, "");
        }
    }
}

