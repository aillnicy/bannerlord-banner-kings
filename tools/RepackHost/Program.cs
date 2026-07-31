using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

static class Program
{
    public static int Main(string[] args)
    {
        var root = AppContext.BaseDirectory;
        var payload = Path.Combine(root, "toolpayload");
        if (!Directory.Exists(payload))
        {
            Console.Error.WriteLine("toolpayload directory is missing");
            return 2;
        }

        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var candidate = Path.Combine(payload, name.Name + ".dll");
            return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
        };

        foreach (var path in Directory.GetFiles(payload, "*.dll").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                if (asm.EntryPoint == null) continue;
                Console.WriteLine("ILRepack host: " + Path.GetFileName(path));
                var parameters = asm.EntryPoint.GetParameters().Length == 0 ? null : new object[] { args };
                var result = asm.EntryPoint.Invoke(null, parameters);
                return result is int code ? code : 0;
            }
            catch (BadImageFormatException) { }
            catch (TargetInvocationException ex)
            {
                Console.Error.WriteLine(ex.InnerException ?? ex);
                return 1;
            }
        }

        Console.Error.WriteLine("No executable ILRepack assembly found.");
        return 3;
    }
}
