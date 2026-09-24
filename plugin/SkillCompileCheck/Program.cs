using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SkillCompileCheck;

public static class Program
{
    public static int Main(string[] args)
    {
        // skills/ lives at mcp-server/skills relative to this project at
        // mcp-server/plugin/SkillCompileCheck
        string projectDir = AppContext.BaseDirectory;
        string skillsRoot = FindSkillsRoot(projectDir);
        if (skillsRoot == null)
        {
            Console.Error.WriteLine("Could not locate the skills/ directory by walking up from " + projectDir);
            return 1;
        }

        Console.WriteLine($"Skills root: {skillsRoot}");

        var skillFiles = Directory.GetFiles(skillsRoot, "*.skill.md", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"Found {skillFiles.Count} skill files.\n");

        var (references, usings) = BuildReferencesAndUsings();

        var indexEntries = new List<SkillIndexEntry>();
        var categoryCounts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int totalBlocks = 0;
        int failedBlocks = 0;
        var failures = new List<string>();

        foreach (var file in skillFiles)
        {
            string relPath = Path.GetRelativePath(skillsRoot, file).Replace('\\', '/');
            string text = File.ReadAllText(file);

            SkillFrontmatter fm;
            try
            {
                fm = ParseFrontmatter(text, relPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FRONTMATTER ERROR] {relPath}: {ex.Message}");
                failures.Add($"{relPath}: frontmatter parse error: {ex.Message}");
                continue;
            }

            categoryCounts.TryGetValue(fm.Category, out int c);
            categoryCounts[fm.Category] = c + 1;

            indexEntries.Add(new SkillIndexEntry
            {
                Name = fm.Name,
                Category = fm.Category,
                Description = fm.Description,
                RequiresWrite = fm.RequiresWrite,
                Parameters = fm.Parameters,
                File = relPath
            });

            var codeBlocks = ExtractCSharpBlocks(text);
            if (codeBlocks.Count == 0)
            {
                Console.WriteLine($"[NO CODE] {relPath}: no ```csharp fence found");
                failures.Add($"{relPath}: no csharp code fence found");
                continue;
            }

            for (int i = 0; i < codeBlocks.Count; i++)
            {
                totalBlocks++;
                string label = codeBlocks.Count > 1 ? $"{relPath} [block {i + 1}/{codeBlocks.Count}]" : relPath;
                var diagnostics = CompileBlock(codeBlocks[i], references, usings);
                var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

                if (errors.Count == 0)
                {
                    Console.WriteLine($"[OK]   {label}");
                }
                else
                {
                    failedBlocks++;
                    Console.WriteLine($"[FAIL] {label}");
                    foreach (var err in errors)
                    {
                        var lineSpan = err.Location.GetLineSpan();
                        Console.WriteLine($"       line {lineSpan.StartLinePosition.Line + 1}: {err.GetMessage()}");
                    }
                    failures.Add(label);
                }
            }
        }

        // Write index.json for the web app
        string indexPath = Path.Combine(skillsRoot, "index.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(indexPath, JsonSerializer.Serialize(indexEntries.OrderBy(e => e.Category).ThenBy(e => e.Name), jsonOptions));
        Console.WriteLine($"\nWrote {indexEntries.Count} entries to {Path.GetRelativePath(skillsRoot, indexPath)}");

        Console.WriteLine("\n=== Skills per category ===");
        foreach (var kv in categoryCounts)
            Console.WriteLine($"  {kv.Key,-20} {kv.Value}");
        Console.WriteLine($"  {"TOTAL",-20} {indexEntries.Count}");

        Console.WriteLine("\n=== Compile summary ===");
        Console.WriteLine($"  Code blocks checked: {totalBlocks}");
        Console.WriteLine($"  Failures: {failedBlocks}");

        if (failedBlocks > 0)
        {
            Console.WriteLine("\nFailing blocks:");
            foreach (var f in failures) Console.WriteLine($"  - {f}");
            return 1;
        }

        Console.WriteLine("\nAll skills compiled successfully.");
        return 0;
    }

    private static string? FindSkillsRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "skills");
            if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "*.skill.md", SearchOption.AllDirectories).Length > 0)
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    // Builds MetadataReferences (pure PE-metadata reads) rather than loading assemblies into the
    // CLR. This matters because the AutoCAD/Civil3D managed DLLs are Windows/x64-specific and
    // cannot be Assembly.LoadFrom'd on a non-Windows host (or even mismatched-arch Windows) — but
    // Roslyn only needs their metadata to type-check a script, never to actually run it, exactly
    // like `dotnet build` compiling Civil3dMcpPlugin.csproj itself works fine on this Mac.
    private static (List<MetadataReference> references, string[] usings) BuildReferencesAndUsings()
    {
        var packageDirs = new[]
        {
            FindNugetPackageDir("speckle.civil3d.api"),
            FindNugetPackageDir("speckle.autocad.api"),
        }.Where(d => d != null).Select(d => d!).ToList();

        var dllPaths = new List<string>();
        foreach (var dir in packageDirs)
        {
            var libDir = Directory.GetDirectories(dir, "lib").FirstOrDefault();
            if (libDir == null) continue;
            var tfmDir = Directory.GetDirectories(libDir).OrderByDescending(d => d).FirstOrDefault();
            if (tfmDir == null) continue;
            dllPaths.AddRange(Directory.GetFiles(tfmDir, "*.dll")
                .Where(p => !Path.GetFileNameWithoutExtension(p).Contains('.'))); // skip satellite resource dlls
        }

        var references = new List<MetadataReference>();
        foreach (var path in dllPaths)
        {
            try { references.Add(MetadataReference.CreateFromFile(path)); }
            catch (Exception ex) { Console.WriteLine($"[WARN] could not read metadata from {Path.GetFileName(path)}: {ex.Message}"); }
        }

        // Core BCL + Roslyn + this project's own already-loaded assemblies, also as metadata-only
        // references (never Assembly.LoadFrom) — these ARE loadable on this host, but we stay
        // consistent and avoid any accidental attempt to instantiate the script.
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
            try { references.Add(MetadataReference.CreateFromFile(asm.Location)); }
            catch { /* skip unreadable */ }
        }

        // Some AeccDbMgd/acdbmgd members (native C++/CLI, e.g. AlignmentEntityCollection.Count)
        // carry an `IsLong` custom modifier on their return type to mark them as native `long`
        // for interop. Roslyn needs that marker type resolvable even though the member itself is
        // just an Int32 — add the small framework assembly that defines it.
        string coreLibDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string visualCPath = Path.Combine(coreLibDir, "System.Runtime.CompilerServices.VisualC.dll");
        if (File.Exists(visualCPath))
        {
            try { references.Add(MetadataReference.CreateFromFile(visualCPath)); }
            catch { /* skip */ }
        }
        else
        {
            Console.WriteLine("[WARN] System.Runtime.CompilerServices.VisualC.dll not found next to the runtime — IsLong modopt errors may appear.");
        }

        var usings = new[]
        {
            "System",
            "System.Linq",
            "System.Collections.Generic",
            "System.Text",
            "Autodesk.AutoCAD.ApplicationServices",
            "Autodesk.AutoCAD.DatabaseServices",
            "Autodesk.AutoCAD.EditorInput",
            "Autodesk.AutoCAD.Geometry",
            "Autodesk.AutoCAD.Runtime",
            "Autodesk.Civil",
            "Autodesk.Civil.ApplicationServices",
            "Autodesk.Civil.DatabaseServices",
            "Autodesk.Civil.Settings"
        };

        return (references, usings);
    }

    private static string? FindNugetPackageDir(string packageIdLower)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string candidate = Path.Combine(home, ".nuget", "packages", packageIdLower);
        if (!Directory.Exists(candidate)) return null;
        var versionDir = Directory.GetDirectories(candidate).OrderByDescending(d => d).FirstOrDefault();
        return versionDir;
    }

    // Pure semantic-diagnostics compile: parses as a C# "Script" syntax tree and binds it against
    // a script-kind CSharpCompilation with the same globals type ScriptContext uses at runtime.
    // This never emits IL or loads an assembly, so it works regardless of host OS/architecture —
    // unlike Microsoft.CodeAnalysis.CSharp.Scripting's Script.Compile(), which (once a script has
    // zero diagnostics) proceeds to build a runnable executor and DOES require loading every
    // referenced assembly into the CLR, which fails immediately for these Windows/x64-only DLLs
    // on a non-Windows or mismatched-arch host.
    private static List<Diagnostic> CompileBlock(string code, List<MetadataReference> references, string[] usings)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            code,
            new CSharpParseOptions(kind: SourceCodeKind.Script, languageVersion: LanguageVersion.Latest));

        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithUsings(usings);

        var compilation = CSharpCompilation.CreateScriptCompilation(
            assemblyName: "SkillCheck_" + Guid.NewGuid().ToString("N"),
            syntaxTree: syntaxTree,
            references: references,
            options: compilationOptions,
            previousScriptCompilation: null,
            returnType: typeof(object),
            globalsType: typeof(ScriptGlobals));

        return compilation.GetDiagnostics().ToList();
    }

    private static List<string> ExtractCSharpBlocks(string markdown)
    {
        var blocks = new List<string>();
        const string fenceStart = "```csharp";
        int pos = 0;
        while (true)
        {
            int start = markdown.IndexOf(fenceStart, pos, StringComparison.Ordinal);
            if (start < 0) break;
            int codeStart = start + fenceStart.Length;
            int end = markdown.IndexOf("```", codeStart, StringComparison.Ordinal);
            if (end < 0) break;
            blocks.Add(markdown.Substring(codeStart, end - codeStart).Trim('\r', '\n'));
            pos = end + 3;
        }
        return blocks;
    }

    private static SkillFrontmatter ParseFrontmatter(string text, string relPath)
    {
        if (!text.StartsWith("---"))
            throw new Exception("missing frontmatter opening ---");

        int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
            throw new Exception("missing frontmatter closing ---");

        string fm = text.Substring(3, end - 3);
        var lines = fm.Replace("\r\n", "\n").Split('\n');

        string name = "", category = "", description = "";
        bool requiresWrite = false;
        var parameters = new List<Dictionary<string, object?>>();

        Dictionary<string, object?>? currentParam = null;
        bool inParameters = false;

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string line = raw;

            if (!line.StartsWith(" ") && !line.StartsWith("-"))
            {
                inParameters = line.TrimEnd().Equals("parameters:") ? true : false;
                if (inParameters) continue;

                var idx = line.IndexOf(':');
                if (idx < 0) continue;
                string key = line.Substring(0, idx).Trim();
                string value = line.Substring(idx + 1).Trim();

                switch (key)
                {
                    case "name": name = Unquote(value); break;
                    case "category": category = Unquote(value); break;
                    case "description": description = Unquote(value); break;
                    case "requires_write": requiresWrite = value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase); break;
                }
                continue;
            }

            if (inParameters)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("- name:"))
                {
                    currentParam = new Dictionary<string, object?> { ["name"] = Unquote(trimmed.Substring("- name:".Length).Trim()) };
                    parameters.Add(currentParam);
                }
                else if (currentParam != null && trimmed.Contains(':'))
                {
                    int idx = trimmed.IndexOf(':');
                    string key = trimmed.Substring(0, idx).Trim();
                    string value = Unquote(trimmed.Substring(idx + 1).Trim());
                    if (key == "required")
                        currentParam[key] = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    else
                        currentParam[key] = value;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(name)) throw new Exception("missing 'name' field");
        if (string.IsNullOrWhiteSpace(category)) throw new Exception("missing 'category' field");

        return new SkillFrontmatter
        {
            Name = name,
            Category = category,
            Description = description,
            RequiresWrite = requiresWrite,
            Parameters = parameters
        };
    }

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
            return s.Substring(1, s.Length - 2);
        return s;
    }
}

public class SkillFrontmatter
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public bool RequiresWrite { get; set; }
    public List<Dictionary<string, object?>> Parameters { get; set; } = new();
}

public class SkillIndexEntry
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public bool RequiresWrite { get; set; }
    public List<Dictionary<string, object?>> Parameters { get; set; } = new();
    public string File { get; set; } = "";
}
