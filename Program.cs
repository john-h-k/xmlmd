using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;

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

            DocumentationCoallator coallator = new(compilation, assemblySymbol);
            coallator.GetDocumentedTypes();

            /*
            var generator = new MdGenerator(compilation, assemblySymbol);
            generator.GenerateMarkdown(output);
            */
        }
    }
}

    /*
    internal sealed class MdGenerator
    {
        Compilation Compilation;
        IAssemblySymbol Assembly;

        public MdGenerator(Compilation compilation, IAssemblySymbol assembly)
        {
            Assembly = assembly;
            Compilation = compilation;
        }

        public void GenerateMarkdown(string outputPath)
        {

            var doc = new XmlDocument();
            doc.Load(XmlName);

            foreach (XmlNode node in doc.ChildNodes[1]!)
            {
                if (node.Name == "assembly")
                {
                    var nameNode = node.ChildNodes[0];
                    AssemblyName = nameNode!.InnerText;
                    continue;
                }

                Debug.Assert(node.Name == "members");
                GenerateMembers(node, outputPath);
            }
        }

        private void GenerateMembers(INamespaceOrTypeSymbol nsOrType)
        {
            if (nsOrType is ITypeSymbol type)
            {
                GenerateMembers(type);
            }
            else
            {
                foreach (var member in nsOrType.GetMembers())
                {
                    if (member is INamespaceOrTypeSymbol nsOrTypeMember)
                    {
                        GenerateMembers(nsOrTypeMember);
                    }
                }
            }
        }

        private DocumentedType<ITypeSymbol> CreateDocumentedType(ITypeSymbol type)
        {
            foreach (var member in type.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol method:

                        break;
                }
            }
        }


        private void GenerateMembers(XmlNode members, string output)
        {
            var memberDocs = new List<DocumentationComment>();

            foreach (XmlNode node in members.ChildNodes)
            {
                Console.WriteLine(node.ToString());
                memberDocs.Add(DocumentationComment.Parse(node));
            }

            var types = memberDocs.Where(doc => doc.Kind == MemberDocKind.Type).ToDictionary(key => key.Name, key => new MemberDocType { Type = key });

            foreach (var member in memberDocs.Where(doc => doc.Kind != MemberDocKind.Type))
            {
                string name = member.Name;
                var paren = name.LastIndexOf('(');

                if (paren != -1)
                {
                    name = name[0..paren];
                }

                var dot = name.LastIndexOf('.');

                var typeName = name[0..dot];
                if (!types.TryGetValue(typeName, out var type))
                {
                    type = new MemberDocType { Type = new DocumentationComment(MemberDocKind.Type, typeName, string.Empty, null!) };
                    types[typeName] = type;
                }


                var target = member.Kind switch
                {
                    MemberDocKind.Field => type.Fields,
                    MemberDocKind.Property => type.Properties,
                    MemberDocKind.Method => type.Methods,
                    MemberDocKind.Event => type.Events,
                    MemberDocKind.ErrorString => type.ErrorStrings,
                    _ => null!
                };

                target.Add(member);
            }

            var demangler = new Demangler(Assembly);
            var writer = new DefaultMarkdownTypeWriter(new DirectoryInfo(output));
            foreach (var (_, type) in types)
            {
                writer.Write(demangler.DemangleEntireType(type));
            }
        }

        private static readonly Dictionary<Type, string> Aliases =
        new Dictionary<Type, string>()
        {
            { typeof(byte), "byte" },
            { typeof(sbyte), "sbyte" },
            { typeof(short), "short" },
            { typeof(ushort), "ushort" },
            { typeof(int), "int" },
            { typeof(uint), "uint" },
            { typeof(long), "long" },
            { typeof(ulong), "ulong" },
            { typeof(float), "float" },
            { typeof(double), "double" },
            { typeof(decimal), "decimal" },
            { typeof(object), "object" },
            { typeof(bool), "bool" },
            { typeof(char), "char" },
            { typeof(string), "string" },
            { typeof(void), "void" }
        };

        private void WriteType(MemberDocType type, string outputFile)
        {
            var directory = Path.Join(outputFile, type.Type.Name.Replace(".", "/"));
            var path = Path.Join(outputFile, type.Type.Name.Replace(".", "/") + ".md");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var output = File.CreateText(path);

            var toc = new MarkdownDocument();

            var asmType = Assembly.DefinedTypes.Where(info => info.FullName?.Replace('+', '.') == type.Type.Name).First();

            var isStatic = asmType is System.Reflection.TypeInfo { IsAbstract: true, IsSealed: true };
            var children = asmType.IsEnum ? Aliases[Enum.GetUnderlyingType(asmType)] : string.Join(", ", Enumerable.Concat(new[] { (asmType.IsValueType ? string.Empty : asmType.BaseType?.Name) }, asmType.ImplementedInterfaces.Select(ii => ii.Name)));


            var demangler = new Demangler(Assembly);

            var methods = type.Methods.Select(m => demangler.DemangleMethodName(asmType, m));
            var properties = type.Properties.Select(m => demangler.DemanglePropertyName(asmType, m));
            var fields = type.Fields.Select(m => demangler.DemangleFieldName(asmType, m));
            var events = type.Events.Select(m => demangler.DemangleEventName(asmType, m));
            var props = type.Properties.Select(m => demangler.DemanglePropertyName(asmType, m));

            toc.AddHeader(1, MarkdownFactory.Bold(type.Type.Name));
            toc.AddNewline();

            var attributes = string.Join("]\n[", asmType.CustomAttributes.Select(attr => attr.AttributeType.Name[..(^"Attribute".Length)]));

            toc.AddText(
                MarkdownFactory.CodeBlock(
                    id: LanguageID.CSharp,

                    (attributes.Length > 0 ? "[" + attributes + "]\n" : string.Empty) +
                    "public " +
                    // static types are sealed and abstract, so we don't print those modifiers if both are present
                    (isStatic ? "static " : string.Empty) +
                    (asmType.IsAbstract && !asmType.IsInterface && !isStatic ? "abstract " : "") +
                    (asmType.IsSealed && !asmType.IsValueType && !isStatic ? "sealed " : "") +
                    (asmType.IsEnum ? "enum " : asmType.IsValueType ? "struct " : asmType.IsInterface ? "interface " : "class ") +
                    (asmType.Name) +
                    (children == string.Empty ? "" : " : ") +
                    children +
                    (asmType.IsEnum ? "\n{\n\t" + string.Join(",\n\t", fields.Select(f => f.Name + " = " + f.GetRawConstantValue()!.ToString())) + "\n}" : "")
                )
            );

            if (fields.Count() != 0)
            {
                toc.AddNewline();
                toc.AddHeader(2, "Fields:");
                toc.AddList(ListKind.Unordered, fields.Select(m => MarkdownFactory.InlineCode(m.Name)));
            }

            toc.AddText(type.Type.Content);
            toc.AddNewline();

            if (methods.Count() != 0)
            {
                toc.AddHeader(2, "Methods:");
                toc.AddList(ListKind.Unordered, methods.Select(m => FormatMethodLink(m)));
            }

            if (properties.Count() != 0)
            {
                toc.AddNewline();
                toc.AddHeader(2, "Properties:");
                toc.AddList(ListKind.Unordered, properties.Select(m => MarkdownFactory.InlineCode(m.Name)));
            }

            if (events.Count() != 0)
            {
                toc.AddNewline();
                toc.AddHeader(2, "Events:");
                toc.AddList(ListKind.Unordered, events.Select(m => MarkdownFactory.InlineCode(m.Name)));
            }

            if (methods.Count() != 0)
            {
                toc.AddText(Enumerable.Zip(methods, type.Methods).Select(((m) => FormatMethod(m.First, m.Second))).SelectMany(m => m));
            }


            MarkdownText FormatMethodLink(MethodBase method)
            {
                var name = MethodName(method);

                var code = MarkdownFactory.InlineCode(name);
                return MarkdownFactory.Link("##" + SanitiseLink(name), MarkdownFactory.InlineCode(name));
            }

            MarkdownText[] FormatMethod(MethodBase method, DocumentationComment doc)
            {
                var name = MethodName(method);
                return new MarkdownText[]
                {
                    MarkdownFactory.Header(2, MarkdownFactory.InlineCode(SanitiseLink(name))),
                    MarkdownFactory.Plain(doc.Content),
                    MarkdownFactory.NewLines(3)
                };
            }

            string MethodName(MethodBase method)
            {
                var @params = method.GetParameters();
                var uniqueName = method.Name;
                uniqueName += '(';
                uniqueName += string.Join(", ", @params.Select(p => p.ParameterType.Name));
                uniqueName += ')';

                return uniqueName;
            }

            string SanitiseLink(string str) => str.Replace(" ", "");
        }
    }

    public interface ITypeWriter
    {
        void Write(DocumentedType type);
    }

    public sealed class DefaultMarkdownTypeWriter : ITypeWriter
    {
        private DirectoryInfo OutputDirectory;

        private const BindingFlags BindAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly Dictionary<Type, string> Aliases =
            new Dictionary<Type, string>()
            {
                { typeof(byte), "byte" },
                { typeof(sbyte), "sbyte" },
                { typeof(short), "short" },
                { typeof(ushort), "ushort" },
                { typeof(int), "int" },
                { typeof(uint), "uint" },
                { typeof(long), "long" },
                { typeof(ulong), "ulong" },
                { typeof(float), "float" },
                { typeof(double), "double" },
                { typeof(decimal), "decimal" },
                { typeof(object), "object" },
                { typeof(bool), "bool" },
                { typeof(char), "char" },
                { typeof(string), "string" },
                { typeof(void), "void" }
            };

        private const string Extension = ".md";

        public DefaultMarkdownTypeWriter(DirectoryInfo outputDirectory)
        {
            OutputDirectory = outputDirectory;
        }

        public void Write(DocumentedType type)
        {
            var path = Path.Join(OutputDirectory.FullName, type.Type.Doc.Name.Replace(".", "/") + '/' + type.Type.Info.Name + Extension);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var output = File.CreateText(path);

            var md = new MarkdownDocument();
            md.AddText(MarkdownFactory.CodeBlock(LanguageID.CSharp, GetTypeDecl(type.Type.Info)));

            output.Write(md);
        }

        private string GetTypeDecl(Type type)
        {
            if (type.IsEnum)
            {
                return GetEnumDecl(type);
            }
            if (type.IsInterface)
            {
                return GetInterfaceDecl(type);
            }
            if (type.IsValueType)
            {
                return GetStructDecl(type);
            }
            return GetClassDecl(type);
        }

        private string GetClassDecl(Type type)
        {
            var modifiers = string.Empty;
            if (type.IsAbstract && type.IsSealed)
            {
                modifiers += "static ";
            }
            else if (type.IsAbstract)
            {
                modifiers += "abstract ";
            }
            else if (type.IsSealed)
            {
                modifiers += "sealed ";
            }
            return GetTypeWithImplementingInterfaces(type, modifiers + "class", true);
        }

        private string GetStructDecl(Type type)
        {
            var modifiers = type.GetCustomAttribute<IsReadOnlyAttribute>() is not null ? "readonly " : string.Empty;
            modifiers += type.IsByRefLike ? "ref " : string.Empty;
            return GetTypeWithImplementingInterfaces(type,  modifiers +  "struct", false);
        }

        private string GetInterfaceDecl(Type type)
        {
            return GetTypeWithImplementingInterfaces(type, "interface", false);
        }

        // logic for interface/struct/class is basically identical
        private string GetTypeWithImplementingInterfaces(Type type, string typeKeyword, bool baseTypes)
        {
            var bases = type.GetInterfaces().Select(i => i.Name);
            if (baseTypes && type.BaseType is not null)
            {
                bases = Enumerable.Prepend(bases, type.BaseType.Name);
            }

            var name = type.Name;
            if (type.GenericTypeArguments.Length > 0)
            {
                name = name + "<" + string.Join(", ", type.GenericTypeArguments.Select(t => t.Name)) + ">";
            }

            return GetAccessibiltyKeyword(type) + ' ' + typeKeyword + ' ' + name + (bases.Count() == 0 ? string.Empty : " : " + string.Join(", ", bases));
        }

        private string GetAttributes(Type type)
        {
            var attrs = Enumerable.Zip(type.CustomAttributes, type.GetCustomAttributesData());
            if (attrs.Count() == 0)
            {
                return string.Empty;
            }

            return '[' + string.Join("]\n[", attrs.Select(attr => attr.First.AttributeType.Name + attr.Second.ToString())) + ']';
        }

        private string GetEnumDecl(Type type, bool includeFields = true)
        {
            var decl = GetAccessibiltyKeyword(type) + ' ' + "enum" + ' ' + type.Name + " : " + Aliases[Enum.GetUnderlyingType(type)];
            var fields = includeFields ? GetEnumFields() : string.Empty;
            return decl + '\n' + '{' + '\n' + "    " + fields + '\n' + '}';

            string GetEnumFields()
                => string.Join(",\n    ",type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(field => field.Name + " = " + field.GetRawConstantValue()!.ToString()));
        }

        private static string GetAccessibiltyKeyword(Type type)
        {
            if (type.IsPublic || type.IsNestedPublic)
            {
                return "public";
            }
            if ((!type.IsNested && !type.IsPublic) || type.IsNestedAssembly)
            {
                return "internal";
            }
            if (type.IsNestedFamily)
            {
                return "protected";
            }
            if (type.IsNestedFamORAssem)
            {
                return "internal protected";
            }
            if (type.IsNestedFamANDAssem)
            {
                return "private protected";
            }
            if (type.IsNestedPrivate)
            {
                return "private";
            }

            throw new ArgumentException("Type is weird");
        }
    }
}
    */