using System;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace AdjustNamespace.Xaml
{
    public class CustomXmlWriter : XmlWriter
    {
        private readonly XmlWriter _xmlWriter;
        private readonly StringBuilder _sb;

        private int _prefix = 0;
        private int _currentLength = 0;
        private int _before = 0;

        public CustomXmlWriter(
            StringBuilder sb
            )
        {
            if (sb is null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            _sb = sb;

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = true;
            settings.NewLineOnAttributes = false;
            settings.NewLineChars = Environment.NewLine;

            _xmlWriter = XmlWriter.Create(sb, settings);
        }

        public override WriteState WriteState => _xmlWriter.WriteState;

        public override void Flush()
        {
            _xmlWriter.Flush();
        }

        public override string LookupPrefix(string ns)
        {
            return _xmlWriter.LookupPrefix(ns);
        }

        public override void WriteBase64(byte[] buffer, int index, int count)
        {
            _xmlWriter.WriteBase64(buffer, index, count);
        }

        public override void WriteCData(string text)
        {
            _xmlWriter.WriteCData(text);
        }

        public override void WriteCharEntity(char ch)
        {
            _xmlWriter.WriteCharEntity(ch);
        }

        public override void WriteChars(char[] buffer, int index, int count)
        {
            _xmlWriter.WriteChars(buffer, index, count);
        }

        public override void WriteComment(string text)
        {
            _xmlWriter.WriteComment(text);
        }

        public override void WriteDocType(string name, string pubid, string sysid, string subset)
        {
            _xmlWriter.WriteDocType(name, pubid, sysid, subset);
        }

        public override void WriteEndAttribute()
        {
            _xmlWriter.WriteEndAttribute();

            _xmlWriter.Flush();
            var after = _sb.ToString().Length;
            _currentLength += (after - _before);
            _before = 0;

        }

        public override void WriteEndDocument()
        {
            _xmlWriter.WriteEndDocument();
        }

        public override void WriteEndElement()
        {
            _prefix -= 2;
            _currentLength = _prefix;
            _xmlWriter.WriteEndElement();
        }

        public override void WriteEntityRef(string name)
        {
            _xmlWriter.WriteEntityRef(name);
        }

        public override void WriteFullEndElement()
        {
            _xmlWriter.WriteFullEndElement();
        }

        public override void WriteProcessingInstruction(string name, string text)
        {
            _xmlWriter.WriteProcessingInstruction(name, text);
        }

        public override void WriteRaw(char[] buffer, int index, int count)
        {
            _xmlWriter.WriteRaw(buffer, index, count);
        }

        public override void WriteRaw(string data)
        {
            _xmlWriter.WriteRaw(data);
        }

        public override void WriteStartAttribute(string prefix, string localName, string ns)
        {
            if (_currentLength > 60)
            {
                _xmlWriter.Flush();
                _sb.AppendLine();
                _sb.Append(GetPrefix());
                _currentLength = _prefix;
            }

            _before = _sb.ToString().Length;

            _xmlWriter.WriteStartAttribute(prefix, localName, ns);
        }

        public override void WriteStartDocument()
        {
            _prefix = 0;
            _xmlWriter.WriteStartDocument();
        }

        public override void WriteStartDocument(bool standalone)
        {
            _prefix = 0;
            _xmlWriter.WriteStartDocument(standalone);
        }

        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            _prefix += 2;
            _xmlWriter.WriteStartElement(prefix, localName, ns);
        }

        public override void WriteString(string text)
        {
            _xmlWriter.WriteString(text);
        }

        public override void WriteSurrogateCharEntity(char lowChar, char highChar)
        {
            _xmlWriter.WriteSurrogateCharEntity(lowChar, highChar);
        }

        public override void WriteWhitespace(string ws)
        {
            _xmlWriter.WriteWhitespace(ws);
        }

        private string GetPrefix() => new string(' ', _prefix);
    }
}
