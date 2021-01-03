
using System.Collections.Generic;
using System.Xml;

namespace XmlMd
{
    public enum MemberDocKind
    {
        Namespace = 'N',
        Type = 'T',
        Field = 'F',
        Property = 'P',
        Method = 'M',
        Event = 'E',
        ErrorString = '!'
    }

    public sealed class MemberDocType
    {
        public MemberDoc Type { get; set; }

        public List<MemberDoc> Fields { get; set; } = new();
        public List<MemberDoc> Properties { get; set; } = new();
        public List<MemberDoc> Methods { get; set; } = new();
        public List<MemberDoc> Events { get; set; } = new();
        public List<MemberDoc> ErrorStrings { get; set; } = new();
    }

    public readonly struct MemberDoc    
    {
        public readonly MemberDocKind Kind;
        public readonly string Name;
        public readonly string Content;
        public readonly XmlNode ContentNode;

        public static MemberDoc Parse(XmlNode node)
        {
            var name = node.Attributes?["name"]!.Value!;

            var innerText = node.InnerXml.Replace(new string(' ', 12), "\n");

            return new MemberDoc((MemberDocKind)name[0], /* skip ID and colon */ name[2..], innerText, node);
        }

        public MemberDoc(MemberDocKind kind, string name, string content, XmlNode contentNode)
        {
            Kind = kind;
            Name = name;
            Content = content;
            ContentNode = contentNode;
        }
    }
}