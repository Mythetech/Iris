using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.Logging;

namespace Iris.Assemblies.CodeGeneration
{
    public class CodeGenerator : ICodeGenerator
    {
        private readonly ILogger<CodeGenerator> _logger;

        public CodeGenerator(ILogger<CodeGenerator> logger)
        {
            _logger = logger;
        }

        public Type Create(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            _logger.LogInformation("Creating type {Type}", type.Name);

            var typeBuilder = CreateTypeBuilder(type.Name + "Dynamic");

            foreach (var property in type.GetProperties())
            {
                CreateProperty(typeBuilder, property.Name, property.PropertyType);
            }

            var createdType = typeBuilder.CreateType();
            
            _logger.LogInformation("Created type {Type}", createdType.Name);
            
            return createdType;
        }

        public Type Create<T>()
        {
            return Create(typeof(T));
        }

        private static TypeBuilder CreateTypeBuilder(string typeName)
        {
            var assemblyName = new AssemblyName(typeName);
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("Iris.Api.Dynamic");
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout, null);
            return typeBuilder;
        }

        private void CreateProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
        {
            _logger.LogDebug("Creating property {PropertyName} of type {PropertyType}", propertyName, propertyType.Name);
            
            var fieldBuilder = typeBuilder.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
            var propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            var methodAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

            var getterBuilder = typeBuilder.DefineMethod("get_" + propertyName, methodAttributes, propertyType, Type.EmptyTypes);
            var getterIL = getterBuilder.GetILGenerator();
            getterIL.Emit(OpCodes.Ldarg_0);
            getterIL.Emit(OpCodes.Ldfld, fieldBuilder);
            getterIL.Emit(OpCodes.Ret);

            var setterBuilder = typeBuilder.DefineMethod("set_" + propertyName, methodAttributes, null, new[] { propertyType });
            var setterIL = setterBuilder.GetILGenerator();
            setterIL.Emit(OpCodes.Ldarg_0);
            setterIL.Emit(OpCodes.Ldarg_1);
            setterIL.Emit(OpCodes.Stfld, fieldBuilder);
            setterIL.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getterBuilder);
            propertyBuilder.SetSetMethod(setterBuilder);
        }
    }
}