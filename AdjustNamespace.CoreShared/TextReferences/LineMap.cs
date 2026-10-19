using System.Collections.Generic;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The lines of a text: which line a position is in and what the line says.
    /// </summary>
    internal sealed class LineMap
    {
        private readonly string _text;
        private readonly List<int> _starts = new() { 0 };

        public LineMap(
            string text
            )
        {
            _text = text;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    _starts.Add(i + 1);
                }
                else if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    _starts.Add(i + 1);
                }
            }
        }

        /// <summary>
        /// The line (starting with 0) the position is in.
        /// </summary>
        public int LineOf(int position)
        {
            var low = 0;
            var high = _starts.Count - 1;

            while (low < high)
            {
                var middle = (low + high + 1) / 2;

                if (_starts[middle] <= position)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low;
        }

        /// <summary>
        /// The position the line starts at.
        /// </summary>
        public int StartOf(int line)
        {
            return _starts[line];
        }

        /// <summary>
        /// The text of the line, without the line break.
        /// </summary>
        public string TextOf(int line)
        {
            var start = _starts[line];
            var end = line + 1 < _starts.Count ? _starts[line + 1] : _text.Length;

            while (end > start && (_text[end - 1] == '\n' || _text[end - 1] == '\r'))
            {
                end--;
            }

            return _text.Substring(start, end - start);
        }
    }
}
