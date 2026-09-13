using System.Reflection;
using System.Reflection.Emit;

namespace Iris.Assemblies.Test;

/// <summary>
/// Writes a pair of real DLLs to a throwaway directory: a dependency assembly, and a
/// "contracts" assembly with a property typed from it. That shape is the whole of SVC-2, and
/// emitting it is the only way to get it without shipping checked-in binaries or fixture
/// projects whose output the test host would already have loaded.
/// </summary>
public sealed class EmittedAssemblyFixture : IDisposable
{
    public const string DependencyName = "Iris.Test.Emitted.Dependency";
    public const string ContractsName = "Iris.Test.Emitted.Contracts";
    public const string MessageTypeName = "Iris.Test.Emitted.Contracts.OrderPlaced";

    public string Directory { get; }

    public string ContractsPath => Path.Combine(Directory, ContractsName + ".dll");
    public string DependencyPath => Path.Combine(Directory, DependencyName + ".dll");

    public EmittedAssemblyFixture()
    {
        Directory = Path.Combine(Path.GetTempPath(), "iris-asm-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);

        var dependency = new PersistedAssemblyBuilder(new AssemblyName(DependencyName), typeof(object).Assembly);
        var addressType = DefineTypeWithStringProperty(
            dependency.DefineDynamicModule(DependencyName),
            DependencyName + ".Address",
            "City");
        addressType.CreateType();
        dependency.Save(DependencyPath);

        var contracts = new PersistedAssemblyBuilder(new AssemblyName(ContractsName), typeof(object).Assembly);
        var orderType = DefineTypeWithStringProperty(
            contracts.DefineDynamicModule(ContractsName),
            MessageTypeName,
            "Reference");
        DefineProperty(orderType, "ShipTo", addressType);
        orderType.CreateType();
        contracts.Save(ContractsPath);
    }

    /// <summary>
    /// Copies the contracts assembly, and optionally its dependency, into a directory of its
    /// own so a test can load the same assembly with and without the sibling present.
    /// </summary>
    public string StageContracts(bool withDependency)
    {
        var staged = Path.Combine(Directory, withDependency ? "with-dependency" : "without-dependency");
        System.IO.Directory.CreateDirectory(staged);

        File.Copy(ContractsPath, Path.Combine(staged, ContractsName + ".dll"), overwrite: true);

        if (withDependency)
            File.Copy(DependencyPath, Path.Combine(staged, DependencyName + ".dll"), overwrite: true);

        return Path.Combine(staged, ContractsName + ".dll");
    }

    private static TypeBuilder DefineTypeWithStringProperty(ModuleBuilder module, string typeName, string propertyName)
    {
        var type = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
        DefineProperty(type, propertyName, typeof(string));
        return type;
    }

    private static void DefineProperty(TypeBuilder type, string name, Type propertyType)
    {
        var field = type.DefineField("_" + name, propertyType, FieldAttributes.Private);
        var property = type.DefineProperty(name, PropertyAttributes.None, propertyType, null);

        var getter = type.DefineMethod("get_" + name,
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            propertyType, Type.EmptyTypes);
        var getterIl = getter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);
        getterIl.Emit(OpCodes.Ldfld, field);
        getterIl.Emit(OpCodes.Ret);
        property.SetGetMethod(getter);

        var setter = type.DefineMethod("set_" + name,
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null, [propertyType]);
        var setterIl = setter.GetILGenerator();
        setterIl.Emit(OpCodes.Ldarg_0);
        setterIl.Emit(OpCodes.Ldarg_1);
        setterIl.Emit(OpCodes.Stfld, field);
        setterIl.Emit(OpCodes.Ret);
        property.SetSetMethod(setter);
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a green test over.
        }
    }
}
