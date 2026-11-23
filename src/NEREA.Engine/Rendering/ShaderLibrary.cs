using System.Reflection;
using System.Text;
using Vortice.D3DCompiler;

namespace NEREA.Engine.Rendering;

/// <summary>
/// Loads embedded HLSL sources, resolves #include directives and compiles
/// them at startup through D3DCompile (d3dcompiler_47, ships with Windows).
/// Compile failures surface as exceptions with the full compiler log.
/// </summary>
public static class ShaderLibrary
{
    private static readonly Dictionary<string, string> SourceCache = new();

    public static ReadOnlyMemory<byte> Compile(string fileName, string entryPoint, string target)
    {
        var source = ResolveSource(fileName);
        try
        {
            return Compiler.Compile(source, entryPoint, fileName, target, ShaderFlags.None, EffectFlags.None);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Shader compile failed ({fileName}:{entryPoint}): {ex.Message}", ex);
        }
    }

    private static string ResolveSource(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = $"NEREA.Engine.Shaders.{fileName}";
        string source;
        lock (SourceCache)
        {
            if (SourceCache.TryGetValue(resource, out var cached)) return ExpandIncludes(cached, asm);
            using var stream = asm.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded shader not found: {resource}");
            using var reader = new StreamReader(stream);
            source = reader.ReadToEnd();
            SourceCache[resource] = source;
        }
        return ExpandIncludes(source, asm);
    }

    private static string ExpandIncludes(string source, Assembly asm)
    {
        var sb = new StringBuilder(source.Length * 2);
        foreach (var line in source.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#include") && trimmed.Contains('"'))
            {
                int a = trimmed.IndexOf('"') + 1;
                int b = trimmed.LastIndexOf('"');
                var include = trimmed[a..b];
                sb.AppendLine(ResolveSource(include).TrimEnd());
            }
            else
            {
                sb.Append(line.TrimEnd('\r'));
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }
}
