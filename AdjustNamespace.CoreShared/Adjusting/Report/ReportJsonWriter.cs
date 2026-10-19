using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// A minimal indenting JSON writer. The core is compiled for .NET Framework 4.8 and for
    /// .NET 10, and a JSON library would be one more dependency of the extension for a text this
    /// simple.
    /// </summary>
    internal sealed class ReportJsonWriter
    {
        private readonly StringBuilder _text = new();

        /// <summary>
        /// One entry per open object / array: whether it has a member already.
        /// </summary>
        private readonly Stack<bool> _hasMembers = new();

        private bool _afterName;

        public void BeginObject()
        {
            BeforeValue();
            _text.Append('{');
            _hasMembers.Push(false);
        }

        public void EndObject()
        {
            End('}');
        }

        public void BeginArray()
        {
            BeforeValue();
            _text.Append('[');
            _hasMembers.Push(false);
        }

        public void EndArray()
        {
            End(']');
        }

        /// <summary>
        /// The name of the next member of the current object.
        /// </summary>
        public void Name(string name)
        {
            BeforeMember();
            WriteString(name);
            _text.Append(": ");
            _afterName = true;
        }

        public void Value(string? value)
        {
            BeforeValue();

            if (value is null)
            {
                _text.Append("null");
                return;
            }

            WriteString(value);
        }

        public void Value(int value)
        {
            BeforeValue();
            _text.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Value(bool value)
        {
            BeforeValue();
            _text.Append(value ? "true" : "false");
        }

        public void Property(string name, string? value)
        {
            Name(name);
            Value(value);
        }

        public void Property(string name, int value)
        {
            Name(name);
            Value(value);
        }

        public void Property(string name, bool value)
        {
            Name(name);
            Value(value);
        }

        public override string ToString()
        {
            return _text.ToString();
        }

        private void End(char bracket)
        {
            var hadMembers = _hasMembers.Pop();

            if (hadMembers)
            {
                NewLine();
            }

            _text.Append(bracket);
        }

        private void BeforeValue()
        {
            if (_afterName)
            {
                _afterName = false;
                return;
            }

            BeforeMember();
        }

        private void BeforeMember()
        {
            if (_hasMembers.Count == 0)
            {
                return;
            }

            if (_hasMembers.Pop())
            {
                _text.Append(',');
            }

            _hasMembers.Push(true);

            NewLine();
        }

        private void NewLine()
        {
            _text.Append('\n').Append(' ', _hasMembers.Count * 2);
        }

        private void WriteString(string value)
        {
            _text.Append('"');

            foreach (var c in value)
            {
                switch (c)
                {
                    case '"':
                        _text.Append("\\\"");
                        break;

                    case '\\':
                        _text.Append("\\\\");
                        break;

                    case '\n':
                        _text.Append("\\n");
                        break;

                    case '\r':
                        _text.Append("\\r");
                        break;

                    case '\t':
                        _text.Append("\\t");
                        break;

                    default:
                        if (c < ' ')
                        {
                            _text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _text.Append(c);
                        }

                        break;
                }
            }

            _text.Append('"');
        }
    }
}
