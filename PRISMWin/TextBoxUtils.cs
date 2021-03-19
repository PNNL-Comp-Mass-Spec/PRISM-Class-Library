using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PRISMWin
{
    /// <summary>
    /// Methods for reading or validating data in a TextBox
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static class TextBoxUtils
    {
        /// <summary>
        /// Tries to convert the string to a double
        /// </summary>
        /// <param name="value"></param>
        /// <returns>True if successful, otherwise false</returns>
        private static bool IsNumber(string value)
        {
            try
            {
                return double.TryParse(value, out _);
            }
            catch (Exception)
            {
                return false;
            }
        }

#if !NETSTANDARD2_0

        /// <summary>
        /// Look for a integer value in a TextBox
        /// </summary>
        /// <param name="thisTextBox">TextBox</param>
        /// <param name="messageIfError">Message to show the user if an error and informOnError is true</param>
        /// <param name="isError">Output: true if an error</param>
        /// <param name="valueIfError">Value to return if not an integer</param>
        /// <param name="informOnError">When true, show a MessageBox if an error</param>
        public static int ParseTextBoxValueInt(
            System.Windows.Forms.TextBox thisTextBox,
            string messageIfError,
            out bool isError,
            int valueIfError = 0,
            bool informOnError = false)
        {
            isError = false;

            try
            {
                return int.Parse(thisTextBox.Text);
            }
            catch (Exception)
            {
                if (informOnError)
                {
                    System.Windows.Forms.MessageBox.Show(messageIfError, "Error", System.Windows.Forms.MessageBoxButtons.OK,
                                                         System.Windows.Forms.MessageBoxIcon.Exclamation);
                }

                isError = true;
                return valueIfError;
            }
        }

        /// <summary>
        /// Look for a float value in a TextBox
        /// </summary>
        /// <param name="thisTextBox">TextBox</param>
        /// <param name="messageIfError">Message to show the user if an error and informOnError is true</param>
        /// <param name="isError">Output: true if an error</param>
        /// <param name="valueIfError">Value to return if not a float</param>
        /// <param name="informOnError">When true, show a MessageBox if an error</param>
        public static float ParseTextBoxValueFloat(
            System.Windows.Forms.TextBox thisTextBox,
            string messageIfError,
            out bool isError,
            float valueIfError = 0,
            bool informOnError = false)
        {
            isError = false;

            try
            {
                return float.Parse(thisTextBox.Text);
            }
            catch (Exception)
            {
                if (informOnError)
                {
                    System.Windows.Forms.MessageBox.Show(messageIfError, "Error", System.Windows.Forms.MessageBoxButtons.OK,
                                                         System.Windows.Forms.MessageBoxIcon.Exclamation);
                }

                isError = true;
                return valueIfError;
            }
        }

        /// <summary>
        /// Look for a double value in a TextBox
        /// </summary>
        /// <param name="thisTextBox">TextBox</param>
        /// <param name="messageIfError">Message to show the user if an error and informOnError is true</param>
        /// <param name="isError">Output: true if an error</param>
        /// <param name="valueIfError">Value to return if not a double</param>
        /// <param name="informOnError">When true, show a MessageBox if an error</param>
        public static double ParseTextBoxValueDbl(
            System.Windows.Forms.TextBox thisTextBox,
            string messageIfError,
            out bool isError,
            double valueIfError = 0,
            bool informOnError = false)
        {
            isError = false;

            try
            {
                return double.Parse(thisTextBox.Text);
            }
            catch (Exception)
            {
                if (informOnError)
                {
                    System.Windows.Forms.MessageBox.Show(messageIfError, "Error", System.Windows.Forms.MessageBoxButtons.OK,
                                                         System.Windows.Forms.MessageBoxIcon.Exclamation);
                }

                isError = true;
                return valueIfError;
            }
        }

#endif

        /// <summary>
        /// Detects when the user uses the control key while typing in a TextBox
        /// Ctrl+A will highlight the entire text box
        /// Ctrl+X, Ctrl+C, and Ctrl+V are blocked if allowCutCopyPaste is False
        /// Ctrl+Z and Ctrl+backspace are allowed
        /// All other key combos are blocked
        /// </summary>
        /// <param name="thisTextBox">TextBox</param>
        /// <param name="e">Keypress event arg</param>
        /// <param name="allowCutCopyPaste">True to allow Ctrl+X, Ctrl+C, and Ctrl+V</param>
        public static void TextBoxKeyPressHandlerCheckControlChars(
            System.Windows.Forms.TextBox thisTextBox,
            System.Windows.Forms.KeyPressEventArgs e,
            bool allowCutCopyPaste = true)
        {
            if (!char.IsControl(e.KeyChar)) return;

            switch (Convert.ToInt32(e.KeyChar))
            {
                case 1:
                    //  Ctrl+A -- Highlight entire text box
                    thisTextBox.SelectionStart = 0;
                    thisTextBox.SelectionLength = thisTextBox.TextLength;
                    e.Handled = true;
                    break;

                case 24:
                case 3:
                case 22:
                    //  Ctrl+X, Ctrl+C, Ctrl+V
                    //  Cut, copy, or paste, was pressed; possibly suppress
                    if (!allowCutCopyPaste)
                    {
                        e.Handled = true;
                    }
                    break;

                case 26:
                    //  Ctrl+Z = Undo; allow .NET to handle this
                    break;

                case 8:
                    //  Backspace is allowed
                    break;

                default:
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Used to examine every keystroke entered into a TextBox and optionally block certain character classes
        /// </summary>
        /// <param name="thisTextBox">TextBox</param>
        /// <param name="e">Keypress event arg</param>
        /// <param name="allowNumbers">True to allow 0-9, false to block them</param>
        /// <param name="allowDecimalPoint">True to allow .</param>
        /// <param name="allowNegativeSign">True to allow -</param>
        /// <param name="allowCharacters">True to allow A-Z and a-z</param>
        /// <param name="allowPlusSign">True to allow +</param>
        /// <param name="allowUnderscore">True to allow _</param>
        /// <param name="allowDollarSign">True to allow $</param>
        /// <param name="allowEmailChars">True to allow @</param>
        /// <param name="allowSpaces">True to allow a space</param>
        /// <param name="allowECharacter">True to allow E (used for numbers like 1.83E-4</param>
        /// <param name="allowCutCopyPaste">True to allow Ctrl+X, Ctrl+C, and Ctrl+V</param>
        /// <param name="allowDateSeparatorChars">True to allow - or /</param>
        public static void TextBoxKeyPressHandler(
            System.Windows.Forms.TextBox thisTextBox,
            System.Windows.Forms.KeyPressEventArgs e,
            bool allowNumbers = true,
            bool allowDecimalPoint = false,
            bool allowNegativeSign = false,
            bool allowCharacters = false,
            bool allowPlusSign = false,
            bool allowUnderscore = false,
            bool allowDollarSign = false,
            bool allowEmailChars = false,
            bool allowSpaces = false,
            bool allowECharacter = false,
            bool allowCutCopyPaste = true,
            bool allowDateSeparatorChars = false
        )
        {
            //  Checks e.KeyChar to see if it's valid
            //  If it isn't, e.Handled is set to True to ignore it
            if (char.IsDigit(e.KeyChar))
            {
                if (!allowNumbers)
                {
                    e.Handled = true;
                }
                return;
            }

            if (char.IsLetter(e.KeyChar))
            {
                if (allowCharacters) return;

                if (allowECharacter && char.ToLower(e.KeyChar) == 'e')
                {
                    //  allow character
                }
                else
                {
                    //  Ignore character
                    e.Handled = true;
                }
                return;
            }

            if (char.IsControl(e.KeyChar))
            {
                TextBoxKeyPressHandlerCheckControlChars(thisTextBox, e, allowCutCopyPaste);
                return;
            }

            switch (e.KeyChar)
            {
                case ' ':
                {
                    if (!allowSpaces)
                    {
                        e.Handled = true;
                    }
                    return;
                }
                case '_':
                {
                    if (!allowUnderscore)
                    {
                        e.Handled = true;
                    }
                    return;
                }
                case '$':
                {
                    if (!allowDollarSign)
                    {
                        e.Handled = true;
                    }
                    return;
                }
                case '+':
                {
                    if (!allowPlusSign)
                    {
                        e.Handled = true;
                    }
                    return;
                }
                case '-':
                {
                    if (!allowNegativeSign || allowDateSeparatorChars)
                    {
                        e.Handled = true;
                    }
                    return;
                }
                case '@':
                {
                    if (!allowEmailChars)
                    {
                        e.Handled = true;
                    }
                    return;
                }
                case '.':
                {
                    if (!allowDecimalPoint)
                    {
                        e.Handled = true;
                    }
                    return;
                }
                case '/':
                {
                    if (!allowDateSeparatorChars)
                    {
                        e.Handled = true;
                    }
                    return;
                }
                default:
                    //  Ignore the key
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Examines the value in a TextBox to assure that it is an integer and is within the specified range
        /// </summary>
        /// <param name="thisTextBox">TextBox</param>
        /// <param name="minimum">Minimum allowed integer</param>
        /// <param name="maximum">Maximum allowed integer</param>
        /// <param name="defaultValue"></param>
        public static void ValidateTextBoxInt(System.Windows.Forms.TextBox thisTextBox, int minimum, int maximum, int defaultValue)
        {
            if (IsNumber(thisTextBox.Text))
            {
                try
                {
                    var value = int.Parse(thisTextBox.Text);
                    if (value < minimum || value > maximum)
                    {
                        thisTextBox.Text = defaultValue.ToString();
                    }
                }
                catch (Exception)
                {
                    thisTextBox.Text = defaultValue.ToString();
                }
            }
            else
            {
                thisTextBox.Text = defaultValue.ToString();
            }
        }

        /// <summary>
        /// Examines the value in a TextBox to assure that it is a float and is within the specified range
        /// </summary>
        /// <param name="thisTextBox">TextBox</param>
        /// <param name="minimum">Minimum allowed float</param>
        /// <param name="maximum">Maximum allowed float</param>
        /// <param name="defaultValue"></param>
        public static void ValidateTextBoxFloat(System.Windows.Forms.TextBox thisTextBox, float minimum, float maximum, float defaultValue)
        {
            if (IsNumber(thisTextBox.Text))
            {
                try
                {
                    var value = float.Parse(thisTextBox.Text);
                    if (value < minimum || value > maximum)
                    {
                        thisTextBox.Text = defaultValue.ToString(CultureInfo.InvariantCulture);
                    }
                }
                catch (Exception)
                {
                    thisTextBox.Text = defaultValue.ToString(CultureInfo.InvariantCulture);
                }
            }
            else
            {
                thisTextBox.Text = defaultValue.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
