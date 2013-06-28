// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED.
//
// Copyright (C) Inverted Software(TM). All rights reserved.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace InvertedSoftware.DataBlock
{
    /// <summary>
    /// This class helps mapping stored procedures input and output to objects.
    /// </summary>
    internal class ObjectHelper
    {
        /// <summary>
        /// Keeps metadata information about a POCO object
        /// </summary>
        internal struct DataObjectInfo
        {
            public PropertyInfo[] Properties { get; set; }
            public Dictionary<string, CrudField> AllAttributes { get; set; }
            public Dictionary<string, Action<object, object>> SetMethods { get; set; }
            public Dictionary<string, Func<object, object>> GetMethods { get; set; }
        }

        /// <summary>
        /// local cache for metadata.
        /// </summary>
        private static ConcurrentDictionary<string, DataObjectInfo> ObjectInfoCache = new ConcurrentDictionary<string, DataObjectInfo>();

        /// <summary>
        /// Local cache for field names in queries.
        /// </summary>
        private static ConcurrentDictionary<string, List<string>> QueryColumnNamesCache = new ConcurrentDictionary<string, List<string>>();

        /// <summary>
        /// Gets object metadata information.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <returns>DataObjectInfo for the specific type.</returns>
        public static DataObjectInfo GetDataObjectInfo<T>()
        {
            string name = typeof(T).FullName;
            return ObjectInfoCache.GetOrAdd(name, (key) =>
            {
                Type type = typeof(T);
                // Fill in properties
                DataObjectInfo dataObjectInfo = new DataObjectInfo()
                {
                    Properties = type.GetProperties().Where(p => p.PropertyType.Namespace != "System.Collections.Generic").ToArray(),
                    AllAttributes = new Dictionary<string, CrudField>(),
                    SetMethods = new Dictionary<string, Action<object, object>>(),
                    GetMethods = new Dictionary<string, Func<object, object>>()
                };

                foreach (var property in dataObjectInfo.Properties)
                {
                    // Fill in attributes
                    var dataAtt = property.GetCustomAttributes(typeof(CrudField), true).Cast<CrudField>().FirstOrDefault();
                    if (dataAtt != null)
                        dataObjectInfo.AllAttributes.Add(property.Name, dataAtt);

                    // Add dynamic set methods
                    var setMethod = property.GetSetMethod();
                    if (setMethod != null)
                    {
                        var arguments = new Type[2] { typeof(object), typeof(object) };
                        var setter = new DynamicMethod(string.Concat("EmitSet", property.Name), typeof(void), arguments, property.DeclaringType);
                        var generator = setter.GetILGenerator();
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Castclass, property.DeclaringType);
                        generator.Emit(OpCodes.Ldarg_1);
                        generator.Emit((property.PropertyType.IsClass) ? OpCodes.Castclass : OpCodes.Unbox_Any, property.PropertyType);
                        generator.EmitCall(OpCodes.Callvirt, setMethod, null);
                        generator.Emit(OpCodes.Ret);
                        dataObjectInfo.SetMethods.Add(property.Name, (Action<object, object>)setter.CreateDelegate(typeof(Action<object, object>)));
                    }
                    // Add dynamic get methods
                    var getMethod = property.GetGetMethod();

                    if (getMethod != null)
                    {
                        var arguments = new Type[1] { typeof(object) };
                        var getter = new DynamicMethod(string.Concat("EmitGet", property.Name), typeof(object), arguments, property.DeclaringType);
                        var generator = getter.GetILGenerator();
                        generator.DeclareLocal(typeof(object));
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Castclass, property.DeclaringType);
                        generator.EmitCall(OpCodes.Callvirt, getMethod, null);
                        if (!property.PropertyType.IsClass)
                        {
                            generator.Emit(OpCodes.Box, property.PropertyType);
                        }
                        generator.Emit(OpCodes.Ret);
                        dataObjectInfo.GetMethods.Add(property.Name, (Func<object, object>)getter.CreateDelegate(typeof(Func<object, object>)));
                    }
                }
                return dataObjectInfo;
            });
        }

        /// <summary>
        /// Gets a list of columns for a query (Please make sure columns returned match the output of the stored procedure).
        /// </summary>
        /// <param name="reader">A data reader containing the stored procedure's result.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <returns>A list of column names.</returns>
        public static List<string> GetColumnNames(SqlDataReader reader, string sprocName)
        {
            return QueryColumnNamesCache.GetOrAdd(sprocName, (key) =>
            {
                List<string> columnNames = new List<string>();
                System.Data.DataTable readerSchema = reader.GetSchemaTable();
                for (int i = 0; i < readerSchema.Rows.Count; i++)
                    columnNames.Add(readerSchema.Rows[i]["ColumnName"].ToString());
                return columnNames;
            });
        }

        /// <summary>
        /// Load the current row in a DataReader into an object.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="reader">A SqlDataReader containing the result and pointing to the next row.</param>
        /// <param name="objectToLoad">The live empty object.</param>
        /// <param name="props">Properties to use when loading the object.</param>
        /// <param name="columnList">The list of columns in the data reader / object.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        public static void LoadAs<T>(SqlDataReader reader, T objectToLoad, PropertyInfo[] props, List<string> columnList, string sprocName)
        {
            DataObjectInfo dataObjectInfo = GetDataObjectInfo<T>();

            if (objectToLoad == null)
                objectToLoad = Activator.CreateInstance<T>();
            if (props == null)
                props = dataObjectInfo.Properties;
            if (columnList == null)
                columnList = GetColumnNames(reader, sprocName);

            for (int i = 0; i < props.Length; i++)
            {
                if (columnList.Contains(props[i].Name) && reader[props[i].Name] != DBNull.Value)
                    dataObjectInfo.SetMethods[props[i].Name](objectToLoad, reader[props[i].Name]);
            }
        }

        /// <summary>
        /// Gets an array of SqlParameter based on a data object.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="dataObject">The live object.</param>
        /// <param name="usedFor">The CRUD operation to be performed with the SqlParameter array.</param>
        /// <returns>SqlParameter array for the object.</returns>
        public static SqlParameter[] GetSQLParametersFromPublicProperties<T>(object dataObject, CrudFieldType usedFor)
        {
            Type type = typeof(T);
            CrudField usedForAttr;
            DataObjectInfo dataObjectInfo = GetDataObjectInfo<T>();
            List<SqlParameter> paramList = new List<SqlParameter>();
            foreach (var prop in dataObjectInfo.Properties)
            {
                usedForAttr = null;
                if ((dataObjectInfo.AllAttributes.TryGetValue(prop.Name, out usedForAttr) &&
                    ((usedForAttr.UsedFor & usedFor) == usedFor ||
                    usedForAttr.UsedFor == CrudFieldType.All)) ||
                    usedForAttr == null)
                {
                    object parameterValue = dataObjectInfo.GetMethods[prop.Name](dataObject);
                    if (parameterValue == null)
                        parameterValue = DBNull.Value;

                    SqlParameter sqlParameter = new SqlParameter(String.Format("@{0}", prop.Name), parameterValue);
                    paramList.Add(sqlParameter);
                }
            }
            return paramList.ToArray();
        }
    }
}