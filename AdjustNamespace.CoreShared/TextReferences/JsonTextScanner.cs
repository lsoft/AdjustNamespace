using System;
using System.Collections.Generic;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Finds the string values of a json file (<c>appsettings*.json</c> and the json files the
    /// settings of the solution name) with the exact span of every one of them, and tells the
    /// ones that are known to hold a type name: the value of <c>$type</c>, of a key called
    /// <c>type</c> or <c>class</c> and the elements of the array <c>Using</c> (a Serilog or a
    /// dependency injection config), in any case. The other values are worth less.
    ///
    /// The keys are not searched, only the values. A comment (<c>//</c> and <c>/* */</c>, which
    /// the configuration of .NET allows) is skipped.
    ///
    /// <c>System.Text.Json</c> is not used on purpose: the core is compiled for .NET Framework 4.8
    /// as well, and a reader that reports the position of the value as it is written is simple
    /// enough to be written here.
    /// </summary>
    internal static class JsonTextScanner
    {
        /// <summary>
        /// The note to the report for a <c>$type</c>: data serialized with the old name cannot be
        /// fixed by an edit of the sources.
        /// </summary>
        public const string SerializedTypeNote =
            "A json file names a type with $type. Data that was serialized with the old name "
            + "(a database, a cache, a queue) may exist elsewhere and cannot be fixed by an edit of the sources.";

        private static readonly HashSet<string> TypeKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "$type",
            "type",
            "class",
            "using"
        };

        public static List<TextCandidate> Scan(string text)
        {
            var result = new List<TextCandidate>();
            var frames = new Stack<Frame>();

            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];

                switch (c)
                {
                    case '/':
                        i = SkipComment(text, i);
                        continue;

                    case '{':
                        frames.Push(new Frame(true, KeyOfEnclosing(frames)));
                        i++;
                        continue;

                    case '[':
                        frames.Push(new Frame(false, KeyOfEnclosing(frames)));
                        i++;
                        continue;

                    case '}':
                    case ']':
                        if (frames.Count > 0)
                        {
                            frames.Pop();
                        }

                        i++;
                        continue;

                    case ',':
                        if (frames.Count > 0 && frames.Peek().IsObject)
                        {
                            frames.Peek().ExpectsKey = true;
                        }

                        i++;
                        continue;

                    case ':':
                        if (frames.Count > 0 && frames.Peek().IsObject)
                        {
                            frames.Peek().ExpectsKey = false;
                        }

                        i++;
                        continue;

                    case '"':
                        i = ReadString(text, i, frames, result);
                        continue;

                    default:
                        i++;
                        continue;
                }
            }

            return result;
        }

        /// <summary>
        /// Read the string that starts at the position and add it as a candidate if it is a value.
        /// </summary>
        /// <returns>The position behind the string.</returns>
        private static int ReadString(
            string text,
            int quoteStart,
            Stack<Frame> frames,
            List<TextCandidate> result
            )
        {
            var contentStart = quoteStart + 1;
            var i = contentStart;

            while (i < text.Length && text[i] != '"')
            {
                //an escaped character: the quote after a backslash does not end the string
                i += text[i] == '\\' ? 2 : 1;
            }

            var contentEnd = Math.Min(i, text.Length);
            var end = Math.Min(contentEnd + 1, text.Length);

            if (frames.Count == 0)
            {
                return end;
            }

            var frame = frames.Peek();

            if (frame.IsObject && frame.ExpectsKey)
            {
                frame.Key = text.Substring(contentStart, contentEnd - contentStart);
                return end;
            }

            var isTypeValue = frame.Key != null && TypeKeys.Contains(frame.Key);

            result.Add(
                new TextCandidate(
                    isTypeValue ? TextReferencePlace.JsonTypeValue : TextReferencePlace.OtherValue,
                    contentStart,
                    contentEnd - contentStart,
                    note: string.Equals(frame.Key, "$type", StringComparison.OrdinalIgnoreCase) ? SerializedTypeNote : null
                    )
                );

            return end;
        }

        /// <summary>
        /// The key the value that is being opened belongs to: an object that is the value of
        /// <c>"Handler": { ... }</c> and an array that is the value of <c>"Using": [ ... ]</c>
        /// take the key with them to their elements.
        /// </summary>
        private static string? KeyOfEnclosing(Stack<Frame> frames)
        {
            if (frames.Count == 0)
            {
                return null;
            }

            return frames.Peek().Key;
        }

        /// <summary>
        /// Skip <c>//...</c> up to the end of the line and <c>/*...*/</c>.
        /// </summary>
        private static int SkipComment(string text, int start)
        {
            if (start + 1 < text.Length && text[start + 1] == '/')
            {
                var end = text.IndexOfAny(new[] { '\r', '\n' }, start);

                return end < 0 ? text.Length : end;
            }

            if (start + 1 < text.Length && text[start + 1] == '*')
            {
                var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);

                return end < 0 ? text.Length : end + 2;
            }

            return start + 1;
        }

        /// <summary>
        /// An object or an array that is being read.
        /// </summary>
        private sealed class Frame
        {
            public bool IsObject
            {
                get;
            }

            /// <summary>
            /// The key of the member being read (an object), or the key the array is the value of.
            /// </summary>
            public string? Key
            {
                get;
                set;
            }

            /// <summary>
            /// The next string of an object is a key.
            /// </summary>
            public bool ExpectsKey
            {
                get;
                set;
            }

            public Frame(bool isObject, string? key)
            {
                IsObject = isObject;
                Key = key;
                ExpectsKey = isObject;
            }
        }
    }
}
