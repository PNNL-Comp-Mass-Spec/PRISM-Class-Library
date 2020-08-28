using System;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using PRISM;

namespace PRISMDatabaseUtils
{
    /// <summary>
    /// Base class for SQLServerDBTools and PostgresDBTools, which are used to retrieve data from a database or run stored procedures
    /// </summary>
    public abstract class DBToolsBase : EventNotifier
    {
        // ReSharper disable CommentTypo
        // Ignore Spelling: smallint, tinyint, bigint, bool, nchar, nvarchar, citext, ntext, datetime
        // Ignore Spelling: datetimeoffset, timestamptz, uuid, uniqueidentifier, sql, json
        // ReSharper restore CommentTypo

        private static readonly Regex mIntegerMatcher = new Regex(@"\d+", RegexOptions.Compiled);

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <param name="command"></param>
        /// <param name="name">Parameter name</param>
        /// <param name="dbType">Database data type</param>
        /// <param name="direction">Parameter direction</param>
        /// <returns>The newly added parameter</returns>
        /// <remarks>
        /// If dbType is Text or VarChar, sets the parameter's value to string.Empty
        /// </remarks>
        public abstract DbParameter AddParameter(
            DbCommand command,
            string name,
            SqlType dbType,
            ParameterDirection direction = ParameterDirection.Input);

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <param name="command"></param>
        /// <param name="name">Parameter name</param>
        /// <param name="dbType">Database data type</param>
        /// <param name="size">Size (typically for varchar, but sometimes for date and time)</param>
        /// <param name="value"></param>
        /// <param name="direction">Parameter direction</param>
        /// <returns>The newly added parameter</returns>
        public abstract DbParameter AddParameter(
            DbCommand command,
            string name,
            SqlType dbType,
            int size,
            object value,
            ParameterDirection direction = ParameterDirection.Input);

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <param name="command"></param>
        /// <param name="name">Parameter name</param>
        /// <param name="dataTypeName">Database data type name</param>
        /// <param name="size">Size (typically for varchar, but sometimes for date and time)</param>
        /// <param name="direction">Parameter direction</param>
        /// <returns>The newly added parameter</returns>
        protected DbParameter AddParameterByDataTypeName(
            DbCommand command,
            string name,
            string dataTypeName,
            int size,
            ParameterDirection direction)
        {
            var success = GetSqlTypeByDataTypeName(dataTypeName, out var dataType, out var supportsSize);

            if (!success)
            {
                OnWarningEvent(string.Format("AddParameterByDataTypeName: Data type {0} not recognized for parameter {1}", dataTypeName, name));
                return null;
            }

            if (!supportsSize)
            {
                var parameter = AddParameter(command, name, dataType, direction);
                return parameter;
            }

            switch (dataType)
            {
                case SqlType.Char:
                    var charParameter = AddParameter(command, name, dataType, size, string.Empty, direction);
                    return charParameter;

                default:
                    // Includes:
                    //   SqlType.Name
                    //   SqlType.Date
                    //   SqlType.Time
                    //   SqlType.DateTime
                    //   SqlType.TimestampTz

                    var parameterWithNull = AddParameter(command, name, dataType, size, null, direction);
                    return parameterWithNull;
            }
        }

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

        /// <summary>
        /// Determine the return code to report after calling a stored procedure or function
        /// </summary>
        /// <param name="cmdParameters"></param>
        /// <returns>Numeric return code</returns>
        /// <remarks>Looks for a parameter named _returnCode or with Direction == ParameterDirection.ReturnValue</remarks>
        protected int GetReturnCode(DbParameterCollection cmdParameters)
        {
            foreach (DbParameter parameter in cmdParameters)
            {
                if (parameter.ParameterName.Equals("_returnCode", StringComparison.OrdinalIgnoreCase))
                {
                    return GetReturnCode(parameter);
                }

                if (parameter.Direction == ParameterDirection.ReturnValue)
                {
                    return parameter.Value.CastDBVal(0);
                }
            }

            // The procedure does not have a standard return or return code parameter
            return DbUtilsConstants.RET_VAL_OK;
        }

        /// <summary>
        /// Parse a parameter (of type string or integer) to determine the return code
        /// Supports Postgres error codes that might contain a letter, e.g. 22P06 or 2200L
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns>Parsed integer, or -1 if does not start with an integer</returns>
        public static int GetReturnCode(IDataParameter parameter)
        {
            var returnCodeValue = parameter.Value.CastDBVal<string>();

            if (string.IsNullOrWhiteSpace(returnCodeValue) || returnCodeValue.Equals("0"))
            {
                parameter.Value = 0;
                return 0;
            }

            // Find the first integer in returnCodeValue
            var match = mIntegerMatcher.Match(returnCodeValue);
            if (match.Success)
            {
                var matchValue = int.Parse(match.Value);
                if (matchValue != 0)
                    return matchValue;
            }

            return DbUtilsConstants.RET_VAL_UNDEFINED_ERROR;
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
