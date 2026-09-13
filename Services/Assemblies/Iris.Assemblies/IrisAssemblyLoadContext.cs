using System.Reflection;
using System.Runtime.Loader;

namespace Iris.Assemblies;

/// <summary>
/// A collectible load context that can find the dependencies of the assembly it was created
/// for. Without this, a contracts DLL that references a sibling assembly loads without
/// complaint and then throws <see cref="FileNotFoundException"/> or
/// <see cref="TypeLoadException"/> the first time anything reflects over its types, which is
/// the moment Iris maps them.
///
/// Resolution is wired to <see cref="AssemblyLoadContext.Resolving"/> rather than an override
/// of <see cref="AssemblyLoadContext.Load"/> on purpose. Resolving runs only after the default
/// context has failed, so anything the host already has loaded stays a single type identity and
/// a user assembly can never pull a second copy of a runtime library in beside it.
/// </summary>
internal sealed class IrisAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver? _resolver;
    private readonly string? _probingDirectory;

    /// <param name="assemblyPath">
    /// Where the assembly came from, when Iris knows. A file picked from disk has one; a
    /// browser upload is a stream and nothing more, so it gets no dependency resolution and
    /// behaves exactly as it did before.
    /// </param>
    public IrisAssemblyLoadContext(string name, string? assemblyPath)
        : base(name, isCollectible: true)
    {
        if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
        {
            _probingDirectory = Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
            _resolver = TryCreateResolver(assemblyPath);
        }

        Resolving += Resolve;
    }

    /// <summary>
    /// The resolver reads the assembly's deps.json, and it throws rather than degrading when
    /// there is nothing there to read. Directory probing alone is still worth having, so a
    /// missing or unreadable deps file costs the caller nothing.
    /// </summary>
    private static AssemblyDependencyResolver? TryCreateResolver(string assemblyPath)
    {
        try
        {
            return new AssemblyDependencyResolver(assemblyPath);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        // The deps file is authoritative when the DLL was published with one.
        var resolved = _resolver?.ResolveAssemblyToPath(name);
        if (resolved is not null && File.Exists(resolved))
            return context.LoadFromAssemblyPath(resolved);

        // A DLL copied out of a bin folder usually arrives without its deps.json, so fall back
        // to the plain convention: the dependency sits next to the assembly that needs it.
        if (_probingDirectory is null || name.Name is null)
            return null;

        var candidate = Path.Combine(_probingDirectory, name.Name + ".dll");

        return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
    }
}
