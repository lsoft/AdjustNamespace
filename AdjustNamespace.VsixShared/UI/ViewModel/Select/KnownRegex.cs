using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace AdjustNamespace.UI.ViewModel.Select
{
    public sealed class KnownRegex
    {
        private ICommand? _applyRegexCommand;
        private readonly Action<KnownRegex> _applyAction;

        public string RegexName
        {
            get;
        }
        public string ReplaceRegex
        {
            get;
        }
        public string ReplacedString
        {
            get;
        }

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