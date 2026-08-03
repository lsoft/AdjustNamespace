using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace AdjustNamespace.UI.ViewModel.Select
{
    /// <summary>
    /// A built in sample of the target namespace regex, shown on the second wizard step.
    /// One click applies it to the regex fields of that step.
    /// </summary>
    public sealed class KnownRegex
    {
        private ICommand? _applyRegexCommand;
        private readonly Action<KnownRegex> _applyAction;

        /// <summary>
        /// Human readable description of what this regex does.
        /// </summary>
        public string RegexName
        {
            get;
        }

        /// <summary>
        /// The regex to search for.
        /// </summary>
        public string ReplaceRegex
        {
            get;
        }

        /// <summary>
        /// The replacement for the found fragment.
        /// </summary>
        public string ReplacedString
        {
            get;
        }

        /// <summary>
        /// Apply this sample to the regex fields of the wizard step.
        /// </summary>
        public ICommand ApplyRegexCommand
        {
            get
            {
                if (_applyRegexCommand == null)
                {
                    _applyRegexCommand = new RelayCommand(
                        a =>
                        {
                            _applyAction(this);
                        }
                        );
                }

                return _applyRegexCommand;
            }
        }


        /// <param name="regexName">Human readable description of the sample.</param>
        /// <param name="replaceRegex">The regex to search for.</param>
        /// <param name="replacedString">The replacement for the found fragment.</param>
        /// <param name="applyAction">Callback which applies this sample to the wizard step.</param>
        public KnownRegex(
            string regexName,
            string replaceRegex,
            string replacedString,
            Action<KnownRegex> applyAction
            )
        {
            if (regexName is null)
            {
                throw new ArgumentNullException(nameof(regexName));
            }

            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            if (replacedString is null)
            {
                throw new ArgumentNullException(nameof(replacedString));
            }

            RegexName = regexName;
            ReplaceRegex = replaceRegex;
            ReplacedString = replacedString;
            _applyAction = applyAction;
        }

    }
}