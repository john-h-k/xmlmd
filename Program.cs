using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using System.IO;
using System.Linq;

namespace XmlMd
{
    public class Program
    {
        public static void Main(string assembly, string output, string? xml = null)
        {
#if DEBUG
            assembly = @"C:\Users\reflectronic\source\repos\reflectronic\sympl\Sympl.Compiler\bin\Debug\net5.0\Sympl.Compiler";
            output = @"C:\Users\reflectronic\Documents\Test";
#endif
            if (Path.GetExtension(assembly) is not (".exe" or ".dll"))
            {
                assembly = File.Exists(assembly + ".dll") ? assembly + ".dll" : assembly + ".exe";
            }

            if (xml is null)
            {
                xml = Path.ChangeExtension(assembly, ".xml");
            }

            var reference = MetadataReference.CreateFromFile(assembly, documentation: XmlDocumentationProvider.CreateFromFile(xml));

            var compilation = CSharpCompilation.Create("a", references: new[] { reference });

            var assemblySymbol = (IAssemblySymbol) compilation.GetAssemblyOrModuleSymbol(reference)!;

            new MarkdownGenerator(new(compilation, assemblySymbol)).GenerateMarkdown(output);        
        }
    }

    internal sealed class MarkdownGenerator
    {
        public DocumentationCoallator Coallator { get; }

        public MarkdownGenerator(DocumentationCoallator coallator)
        {
            Coallator = coallator;
        }

        static readonly SymbolDisplayFormat FullType = new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
            kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword | SymbolDisplayKindOptions.IncludeTypeKeyword,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        static readonly SymbolDisplayFormat BaseType = new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions:  SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);


        public void GenerateMarkdown(string rootOutputDirectory)
        {
            var documentedTypes = Coallator.GetDocumentedTypes();
            foreach (var (symbol, doc) in documentedTypes)
            {
                var parts = symbol.ContainingNamespace.ToDisplayParts().Where(p => p.Kind is SymbolDisplayPartKind.NamespaceName).Select(e => e.ToString());
                var path = string.Join(Path.DirectorySeparatorChar, parts.Prepend(rootOutputDirectory).Append(symbol.MetadataName));
                path = Path.ChangeExtension(path, ".md");

                var toc = new MarkdownDocument();

                toc.AddHeader(1, $"{symbol.Name} {symbol.TypeKind}");
                toc.AddText(doc?.Summary?.Value);

                // TODO: https://github.com/dotnet/roslyn/issues/28297
                // Roslyn should probably support more here

                var str = symbol.ToDisplayString(FullType);
                if (symbol.BaseType is { SpecialType: not SpecialType.System_Object } || symbol.Interfaces.Length > 0)
                {   
                    str += " : ";
                    str += string.Join(", ", symbol.Interfaces.Prepend(symbol.BaseType).Select(t => t?.ToDisplayString(BaseType)));
                }

                toc.AddText(MarkdownFactory.CodeBlock(LanguageId.CSharp, str));

                var file = new FileInfo(path);
                file.Directory?.Create();
                File.WriteAllText(file.FullName, toc.ToString());
            }    
        }
    }
}
