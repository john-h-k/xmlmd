using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XmlMd
{
    public sealed class MarkdownDocument
    {
        private StringWriter _output;

        public MarkdownDocument()
        {
            _output = new();
        }

        public void AddHeader(int level, MarkdownText text)
        {
            _output.Write(new string('#', level));
            _output.Write(' ');
            _output.WriteLine(text);
        }

        public void AddNewline(int count = 1) => _output.Write(new string('\n', count));

        public void AddList(ListKind kind, IEnumerable<MarkdownText> elements)
        {
            int i = 0;
            foreach (var element in elements)
            {
                _output.Write(kind == ListKind.Ordered ? i++ + "." : "*");
                _output.Write(' ');
                _output.WriteLine(element);
            }
        }

        public void AddTable(ICollection<MarkdownText> header, IList<IList<MarkdownText>> table)
        {
            int i = 0;
            var lengths = new int[table.Count];

            foreach (var row in table[0])
            {
                lengths[i++] = table.Max(row => row[i].Text.Length);
            }

            i = 0;
            _output.Write('|');
            _output.Write(' ');
            foreach (var headerElement in header)
            {
                _output.Write(header);
                _output.Write(new string(' ', header.Count - lengths[i++]));
                _output.Write(' ');
                _output.Write('|');
            }

            _output.Write("| " + string.Join(" | ", lengths.Select(length => new string('-', length - 2))) + " |");

            _output.Write('|');
            _output.Write(' ');
            foreach (var row in table)
            {
                foreach (var element in row)
                {
                    AddText(element);
                    _output.Write(' ');
                    _output.Write('|');
                }
            }
        }

        public void AddText(IEnumerable<MarkdownText> texts)
        {
            foreach (var text in texts)
            {
                AddText(text);
            }
        }
        public void AddText(MarkdownText text) => _output.Write(text);

        public void AddImage(MarkdownText altText, string href)
        {
            _output.Write('!');
            _output.Write('[');
            _output.Write(altText);
            _output.Write(']');
            _output.Write('(');
            _output.Write(href);
            _output.Write(')');
        }

        public override string ToString() => _output.ToString();
    }

    public static class LanguageID
    {
        public const string CSharp = "cs";
        public const string CPlusPlus = "cpp";
        public const string C = "c";
    }

    public class MarkdownText
    {
        internal MarkdownText(string text)
        {
            Text = text;
        }

        public string Text { get; private set; }

        public override string ToString() => Text;

        public static implicit operator MarkdownText(string text) => new MarkdownText(text);
    }

    public class MarkdownFactory
    {
        public static MarkdownText NewLine { get;} = "\n";
        public static MarkdownText NewLines(int count) => new string('\n', count);
        public static MarkdownText Plain(string text) => text;
        public static MarkdownText Header(int level, MarkdownText text) => new(new string('#', level) + " " + text.Text);
        public static MarkdownText Bold(MarkdownText text) => new("**" + text.Text + "**");
        public static MarkdownText Italic(MarkdownText text) => Bold(text.Text);
        public static MarkdownText InlineCode(MarkdownText text) => new("`" + text.Text + "`");

        public static MarkdownText CodeBlock(string id, MarkdownText text) => new("```" + id + "\n" + text.Text + "\n" + "```" + "\n");
        public static MarkdownText Link(string href, MarkdownText text) => new('[' + text.Text + ']' + '(' + href + ')');
    }

    public enum ListKind
    {
        Unordered,
        Ordered
    }
}