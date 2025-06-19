namespace Iris.Assemblies.CodeGeneration
{
    public interface ICodeGenerator
    {
        public Type Create<T>();

        public Type Create(Type t);
    }
}

