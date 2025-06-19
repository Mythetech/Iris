using System;
namespace Iris.Contracts.Results
{
    public class Result<T>
    {
        public bool Error { get; set; }
        public string Message { get; set; }
        public T Value { get; set; }

        public Result(bool error, string message, T value)
        {
            Error = error;
            Message = message;
            Value = value;
        }
    }
}

