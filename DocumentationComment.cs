using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace XmlMd
{
    public readonly struct DocumentationComment
    {
        public readonly XElement Content { get; private init; }

        public XElement? Summary => Content.Element("summary");

        public static DocumentationComment? Parse(string? node)
        {
            if (string.IsNullOrWhiteSpace(node))
            {
                return null;
            }

            static IEnumerable<XNode> GetXmlElements(string xml)
            {
                var settings = new XmlReaderSettings
                {
                    ConformanceLevel = ConformanceLevel.Fragment,
                    IgnoreWhitespace = true
                };

                using var stringReader = new StringReader(xml);
                using var xmlReader = XmlReader.Create(stringReader, settings);

                xmlReader.MoveToContent();
                while (xmlReader.ReadState != ReadState.EndOfFile)
                {
                    yield return XNode.ReadFrom(xmlReader);
                }
            }

            return new() { Content = new XElement("Root", GetXmlElements(node)) };
        }
    }
}