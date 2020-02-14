using System;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using PRISM;

namespace PRISMDatabaseUtils
{
    internal abstract class DBToolsBase : EventNotifier
    {
        private static readonly Regex mIntegerMatcher = new Regex(@"\d+", RegexOptions.Compiled);

        protected int GetReturnCode(DbParameterCollection cmdParameters)
        {
            foreach (DbParameter parameter in cmdParameters)
            {
                if (parameter.ParameterName.Equals("_returnCode", StringComparison.OrdinalIgnoreCase))
                {
                    var returnCodeValue = parameter.Value.CastDBVal<string>();

                    if (string.IsNullOrWhiteSpace(returnCodeValue) || returnCodeValue.Equals("0"))
                    {
                        parameter.Value = 0;
                        return 0;
                    }

                    // Find the longest integer in returnCodeValue
                    var match = mIntegerMatcher.Match(returnCodeValue);
                    if (match.Success)
                    {
                        var matchValue = int.Parse(match.Value);
                        if (matchValue != 0)
                            return matchValue;

                    }

                    return DbUtilsConstants.RET_VAL_UNDEFINED_ERROR;
                }

                if (parameter.Direction == ParameterDirection.ReturnValue)
                {
                    var returnCodeValue = parameter.Value.CastDBVal(0);
                    return returnCodeValue;
                }
            }

            // The procedure does not have a standard return or return code parameter
            return DbUtilsConstants.RET_VAL_OK;
        }

        protected bool IsFatalException(Exception ex)
        {
            return
                ex.Message.IndexOf("Login failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ex.Message.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ex.Message.IndexOf("Invalid column name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ex.Message.IndexOf("permission was denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ex.Message.IndexOf("permission denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ex.Message.IndexOf("No password has been provided but the backend requires one", StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }
}
