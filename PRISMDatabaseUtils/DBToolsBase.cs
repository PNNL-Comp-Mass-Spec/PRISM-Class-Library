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


        /// <summary>
        /// Get the .NET DbType for the given data type name
        /// </summary>
        /// <param name="dataTypeName">Data type name, supporting various synonyms for each data type</param>
        /// <param name="dataType">Output: SQL data type</param>
        /// <param name="supportsSize">Output: True if the data type supports a value for size</param>
        /// <returns>True if a recognized data type, otherwise false</returns>
        public static bool GetDbTypeByDataTypeName(string dataTypeName, out DbType dataType, out bool supportsSize)
        {
            supportsSize = false;

            switch (dataTypeName.ToLower())
            {
                case "bit":
                case "bool":
                case "boolean":
                    dataType = DbType.Boolean;
                    return true;

                case "tinyint":
                case "byte":
                    dataType = DbType.Byte;
                    return true;

                case "smallint":
                case "int16":
                case "int2":
                    dataType = DbType.Int16;
                    return true;

                case "int":
                case "int32":
                case "integer":
                case "int4":
                    dataType = DbType.Int32;
                    return true;

                case "bigint":
                case "int64":
                case "long":
                case "int8":
                    dataType = DbType.Int64;
                    return true;

                case "real":
                case "single":
                    dataType = DbType.Single;
                    return true;

                case "float":
                case "double":
                    dataType = DbType.Double;
                    return true;

                case "numeric":
                case "decimal":
                case "money":
                    dataType = DbType.Double;
                    return true;

                case "char":
                case "character":
                case "nchar":
                    dataType = DbType.String;
                    return true;

                // ReSharper disable StringLiteralTypo
                case "varchar":
                case "nvarchar":
                case "text":
                case "citext":
                case "ntext":
                case "name":
                case "string":
                    dataType = DbType.String;
                    return true;

                // ReSharper restore StringLiteralTypo

                case "date":
                case "time":
                case "datetime":
                case "timestamp":
                    dataType = DbType.DateTime;
                    return true;

                // ReSharper disable StringLiteralTypo
                case "datetimeoffset":
                case "timestamptz":
                    dataType = DbType.DateTimeOffset;
                    return true;

                // ReSharper restore StringLiteralTypo

                case "blob":
                case "binary":
                    dataType = DbType.Binary;
                    return true;

                // ReSharper disable once StringLiteralTypo
                case "uuid":
                case "uniqueidentifier":
                    dataType = DbType.String;
                    return true;

                case "xml":
                    dataType = DbType.String;
                    return true;

                case "sql_variant":
                    dataType = DbType.Object;
                    return true;

            }

            dataType = DbType.Int32;
            return false;
        }

        /// <summary>
        /// Get the SqlType enum for the given data type name
        /// </summary>
        /// <param name="dataTypeName">Data type name, supporting various synonyms for each data type</param>
        /// <param name="dataType">Output: SQL data type</param>
        /// <param name="supportsSize">Output: True if the data type supports a value for size</param>
        /// <returns>True if a recognized data type, otherwise false</returns>
        public static bool GetSqlTypeByDataTypeName(string dataTypeName, out SqlType dataType, out bool supportsSize)
        {
            var success = GetDbTypeByDataTypeName(dataTypeName, out var dbType, out supportsSize);
            if (!success)
            {
                dataType = SqlType.Integer;
                return false;
            }

            switch (dbType)
            {
                case DbType.Boolean:
                case DbType.Byte:

                    switch (dataTypeName.ToLower())
                    {
                        case "bit":
                            dataType = SqlType.Bit;
                            return true;

                        case "tinyint":
                            dataType = SqlType.TinyInt;
                            return true;

                        default:
                            // Includes "bool" and "boolean"
                            dataType = SqlType.Boolean;
                            return true;
                    }

                case DbType.Int16:
                    dataType = SqlType.SmallInt;
                    return true;

                case DbType.Int32:
                    dataType = SqlType.Int;
                    return true;

                case DbType.Int64:
                    dataType = SqlType.BigInt;
                    return true;

                case DbType.Single:
                    dataType = SqlType.Real;
                    return true;

                case DbType.Double:
                    switch (dataTypeName.ToLower())
                    {
                        case "numeric":
                        case "decimal":
                            dataType = SqlType.Decimal;
                            return true;

                        case "money":
                            dataType = SqlType.Money;
                            return true;

                        default:
                            // Includes "float" and "double"
                            dataType = SqlType.Float;
                            return true;
                    }

                case DbType.String:
                    switch (dataTypeName.ToLower())
                    {
                        case "char":
                        case "character":
                        case "nchar":
                            dataType = SqlType.Char;
                            supportsSize = true;
                            return true;

                        // ReSharper disable StringLiteralTypo
                        case "text":
                        case "ntext":
                            dataType = SqlType.Text;
                            return true;

                        case "citext":
                            dataType = SqlType.Citext;
                            return true;

                        case "name":
                            dataType = SqlType.Name;
                            supportsSize = true;
                            return true;

                        case "uuid":
                        case "uniqueidentifier":
                            dataType = SqlType.UUID;
                            return true;

                        // ReSharper restore StringLiteralTypo

                        case "xml":
                            dataType = SqlType.XML;
                            return true;

                        case "json":
                            dataType = SqlType.JSON;
                            return true;

                        default:
                            // Includes "varchar" and "nvarchar"
                            dataType = SqlType.VarChar;
                            return true;
                    }

                case DbType.DateTime:
                    switch (dataTypeName.ToLower())
                    {
                        case "date":
                            dataType = SqlType.Date;
                            supportsSize = true;
                            return true;

                        case "time":
                            dataType = SqlType.Time;
                            supportsSize = true;
                            return true;

                        default:
                            // Includes "datetime" and "timestamp"
                            dataType = SqlType.DateTime;
                            supportsSize = true;
                            return true;
                    }

                case DbType.DateTimeOffset:
                    dataType = SqlType.TimestampTz;
                    supportsSize = true;
                    return true;

                default:

                    if (dataTypeName.Equals("interval", StringComparison.OrdinalIgnoreCase))
                    {
                        dataType = SqlType.Interval;
                        return true;
                    }

                    // Unsupported type
                    // Includes DbType.Binary and DbType.Object

                    dataType = SqlType.Integer;
                    return false;
            }
        }

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

        /// <summary>
        /// Set the default precision for a Decimal (aka Numeric) parameter
        /// </summary>
        /// <param name="param"></param>
        protected void SetDefaultPrecision(DbParameter param)
        {
            if (param.DbType != DbType.Decimal)
                return;

            // Assign a default precision and scale
            param.Precision = 9;
            param.Scale = 5;
        }

    }
}
