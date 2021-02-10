using Microsoft.CodeAnalysis;

using System.Collections.Immutable;

namespace XmlMd
{
    public record DocumentedSymbol<TSymbol>(TSymbol Symbol, DocumentationComment? Documentation) where TSymbol : ISymbol;

    public sealed record DocumentedType(INamedTypeSymbol Symbol, DocumentationComment? Documentation) : DocumentedSymbol<INamedTypeSymbol>(Symbol, Documentation)
    {
        public ImmutableArray<DocumentedSymbol<IFieldSymbol>> Fields { get; init; }
        public ImmutableArray<DocumentedSymbol<IMethodSymbol>> Methods { get; init; }
        public ImmutableArray<DocumentedSymbol<IPropertySymbol>> Properties { get; init; }
        public ImmutableArray<DocumentedSymbol<IEventSymbol>> Events { get; init; }

        // TODO: Extension methods
    }

    internal sealed class DocumentationCoallator
    {
        public Compilation Compilation { get; }
        public IAssemblySymbol Assembly { get; }

        public DocumentationCoallator(Compilation compilation, IAssemblySymbol assembly)
        {
            Compilation = compilation;
            Assembly = assembly;
        }

        public ImmutableArray<DocumentedType> GetDocumentedTypes()
        {
            var builder = ImmutableArray.CreateBuilder<DocumentedType>();

            ProcessMembers(Assembly.GlobalNamespace, builder);

            return builder.ToImmutable();
        }

        private void ProcessMembers(INamespaceOrTypeSymbol nsOrType, ImmutableArray<DocumentedType>.Builder builder)
        {
            if (nsOrType is INamespaceSymbol ns)
            {
                foreach (var symbol in ns.GetMembers())
                {
                    ProcessMembers(symbol, builder);
                }
            }
            else if (nsOrType is INamedTypeSymbol { DeclaredAccessibility: Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal } type)
            {
                builder.Add(CoallateTypeWithDocumentation(type));

                foreach (var nestedType in type.GetTypeMembers())
                {
                    ProcessMembers(nestedType, builder);
                }
            }
        }

        private static DocumentedType CoallateTypeWithDocumentation(INamedTypeSymbol type)
        {
            var fields = ImmutableArray.CreateBuilder<DocumentedSymbol<IFieldSymbol>>();
            var methods = ImmutableArray.CreateBuilder<DocumentedSymbol<IMethodSymbol>>();
            var properties = ImmutableArray.CreateBuilder<DocumentedSymbol<IPropertySymbol>>();
            var events = ImmutableArray.CreateBuilder<DocumentedSymbol<IEventSymbol>>();

            foreach (var member in type.GetMembers())
            {
                var xml = DocumentationComment.Parse(member.GetDocumentationCommentXml());

                switch (member)
                {
                    case IFieldSymbol field:
                        fields.Add(new DocumentedSymbol<IFieldSymbol>(field, xml));
                        break;
                    case IMethodSymbol method:
                        methods.Add(new DocumentedSymbol<IMethodSymbol>(method, xml));
                        break;
                    case IPropertySymbol prop:
                        properties.Add(new DocumentedSymbol<IPropertySymbol>(prop, xml));
                        break;
                    case IEventSymbol @event:
                        events.Add(new DocumentedSymbol<IEventSymbol>(@event, xml));
                        break;
                }
            }

            return new DocumentedType(type, DocumentationComment.Parse(type.GetDocumentationCommentXml()))
            {
                Fields = fields.ToImmutable(),
                Methods = methods.ToImmutable(),
                Properties = properties.ToImmutable(),
                Events = events.ToImmutable()
            };
        }
    }
}
