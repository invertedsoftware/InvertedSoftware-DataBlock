// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED.
//
// Copyright (C) Inverted Software(TM). All rights reserved.
//

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace InvertedSoftware.DataBlock
{
    /// <summary>
    /// This class helps mapping stored procedures input and output to objects.
    /// </summary>
    public class ObjectHelper
    {
        /// <summary>
        /// Keeps metadata information about a POCO object
        /// </summary>
        internal struct DataObjectInfo
        {
            public PropertyInfo[] Properties { get; set; }
            public PropertyInfo[] InnerCollections { get; set; }
            public Dictionary<string, MapToColumn> MappingAttributes { get; set; }
            public Dictionary<string, CrudField> AllAttributes { get; set; }
            public Dictionary<string, ForeignKeyAttribute> ForeignKeyAttributes { get; set; }
            public Dictionary<string, DatabaseGeneratedAttribute> IdentityAttributes { get; set; }
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
        internal static DataObjectInfo GetDataObjectInfo<T>()
        {
            string name = typeof(T).FullName;
            return ObjectInfoCache.GetOrAdd(name, (key) =>
            {
                Type type = typeof(T);
                // Fill in properties
                DataObjectInfo dataObjectInfo = new DataObjectInfo()
                {
                    Properties = type.GetProperties().Where(p => !IsCollectionType(p.PropertyType)).ToArray(),
                    InnerCollections = type.GetProperties().Where(p => IsCollectionType(p.PropertyType)).ToArray(),
                    MappingAttributes = new Dictionary<string, MapToColumn>(),
                    AllAttributes = new Dictionary<string, CrudField>(),
                    ForeignKeyAttributes = new Dictionary<string, ForeignKeyAttribute>(),
                    IdentityAttributes = new Dictionary<string, DatabaseGeneratedAttribute>(),
                    SetMethods = new Dictionary<string, Action<object, object>>(),
                    GetMethods = new Dictionary<string, Func<object, object>>()
                };

                foreach (var property in dataObjectInfo.Properties)
                {
                    // Fill in mapping attributes
                    var mapAtt = property.GetCustomAttributes<MapToColumn>(true).FirstOrDefault();
                    if (mapAtt != null)
                        dataObjectInfo.MappingAttributes.Add(property.Name, mapAtt);

                    // Fill in CRUD attributes
                    var dataAtt = property.GetCustomAttributes<CrudField>(true).FirstOrDefault();
                    if (dataAtt != null)
                        dataObjectInfo.AllAttributes.Add(property.Name, dataAtt);

                    // Fill in foreign keys
                    var fkAttribute = property.GetCustomAttribute<ForeignKeyAttribute>();
                    if (fkAttribute != null)
                        dataObjectInfo.ForeignKeyAttributes.Add(fkAttribute.Name, fkAttribute);

                    // Fill in Database Identity
                    var IDAttribute = property.GetCustomAttribute<DatabaseGeneratedAttribute>();
                    if (IDAttribute != null && IDAttribute.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                        dataObjectInfo.IdentityAttributes.Add(property.Name, IDAttribute);

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
                        if (mapAtt == null)
                            dataObjectInfo.SetMethods.Add(property.Name, (Action<object, object>)setter.CreateDelegate(typeof(Action<object, object>)));
                        else
                            dataObjectInfo.SetMethods.Add(mapAtt.ColumnName, (Action<object, object>)setter.CreateDelegate(typeof(Action<object, object>)));
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
                        if (mapAtt == null)
                            dataObjectInfo.GetMethods.Add(property.Name, (Func<object, object>)getter.CreateDelegate(typeof(Func<object, object>)));
                        else
                            dataObjectInfo.GetMethods.Add(mapAtt.ColumnName, (Func<object, object>)getter.CreateDelegate(typeof(Func<object, object>)));
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
        /// <param name="useCache">Use the internal DataBlock's cache</param>
        /// <returns>A list of column names.</returns>
        internal static List<string> GetColumnNames(SqlDataReader reader, string sprocName, bool useCache = true)
        {
            if (useCache)
                return QueryColumnNamesCache.GetOrAdd(sprocName, (key) =>
                {
                    return GetColumnNames(reader, sprocName);
                });

            else
                return GetColumnNames(reader, sprocName);
        }

        /// <summary>
        /// Gets a list of columns for a query (Please make sure columns returned match the output of the stored procedure).
        /// </summary>
        /// <param name="reader">A data reader containing the stored procedure's result.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <returns>A list of column names.</returns>
        private static List<string> GetColumnNames(SqlDataReader reader, string sprocName)
        {
            List<string> columnNames = new List<string>();
            System.Data.DataTable readerSchema = reader.GetSchemaTable();
            for (int i = 0; i < readerSchema.Rows.Count; i++)
                columnNames.Add(readerSchema.Rows[i]["ColumnName"].ToString());
            return columnNames;
        }

        /// <summary>
        /// Load the current row in a SqlDataReader into an object.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="reader">A SqlDataReader containing the result and pointing to the next row.</param>
        /// <param name="objectToLoad">The live empty object.</param>
        /// <param name="props">Properties to use when loading the object.</param>
        /// <param name="columnList">The list of columns in the data reader / object.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        internal static void LoadAs<T>(SqlDataReader reader, T objectToLoad, PropertyInfo[] props, List<string> columnList, string sprocName)
        {
            DataObjectInfo dataObjectInfo = GetDataObjectInfo<T>();
            CrudField usedForAttr;
            MapToColumn column;
            string currentColumn = null;

            if (objectToLoad == null)
                objectToLoad = Activator.CreateInstance<T>();
            if (props == null)
                props = dataObjectInfo.Properties;
            if (columnList == null)
                columnList = GetColumnNames(reader, sprocName);

            for (int i = 0; i < props.Length; i++)
            {
                column = null;
                usedForAttr = null;

                if (dataObjectInfo.MappingAttributes.TryGetValue(props[i].Name, out column))
                    currentColumn = column.ColumnName;
                else
                    currentColumn = props[i].Name;

                if ((dataObjectInfo.AllAttributes.TryGetValue(props[i].Name, out usedForAttr) &&
                    ((usedForAttr.UsedFor & CrudFieldType.Read) == CrudFieldType.Read ||
                    usedForAttr.UsedFor == CrudFieldType.All)) ||
                    usedForAttr == null &&
                    columnList.Contains(currentColumn) &&
                    reader[currentColumn] != DBNull.Value)
                    dataObjectInfo.SetMethods[currentColumn](objectToLoad, reader[currentColumn]);
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
            for (int i = 0; i < dataObjectInfo.Properties.Length; i++)
            {
                usedForAttr = null;
                if ((dataObjectInfo.AllAttributes.TryGetValue(dataObjectInfo.Properties[i].Name, out usedForAttr) &&
                    ((usedForAttr.UsedFor & usedFor) == usedFor ||
                    usedForAttr.UsedFor == CrudFieldType.All)) ||
                    usedForAttr == null)
                {
                    object parameterValue = null;
                    SqlParameter sqlParameter = null;
                    MapToColumn column;
                    if (dataObjectInfo.MappingAttributes.TryGetValue(dataObjectInfo.Properties[i].Name, out column))
                    {
                        parameterValue = dataObjectInfo.GetMethods[column.ColumnName](dataObject);
                        sqlParameter = new SqlParameter(String.Format("@{0}", column.ColumnName), parameterValue);
                    }
                    else
                    {
                        parameterValue = dataObjectInfo.GetMethods[dataObjectInfo.Properties[i].Name](dataObject);
                        sqlParameter = new SqlParameter(String.Format("@{0}", dataObjectInfo.Properties[i].Name), parameterValue);
                    }

                    if (parameterValue == null)
                        parameterValue = DBNull.Value;
                    paramList.Add(sqlParameter);
                }
            }
            return paramList.ToArray();
        }

        /// <summary>
        /// Utility method to return DBNull.Value on null objects.
        /// </summary>
        /// <param name="unsafeValue">The original value</param>
        /// <param name="returnNullIf">If unsafeValue is this value, return DBNull.Value.</param>
        /// <returns>The original value or DBNull.Value</returns>
        public static object GetSqlParameterValue(object unsafeValue, object returnNullIf = null)
        {
            if (unsafeValue != null && unsafeValue != returnNullIf)
                return unsafeValue;
            return DBNull.Value;
        }

        /// <summary>
        /// Load an object into a list based on a column name that matches the object type and property.
        /// </summary>
        /// <typeparam name="T">The type of the object to load</typeparam>
        /// <param name="generator">Function to generate a new child. Example: () => new MyCustomObject()</param>
        /// <param name="objectList">A list to load the object to.</param>
        /// <param name="allColumns">All of the colums in the SqlDataReader</param>
        /// <param name="reader">A SqlDataReader pointed to the current row.</param>
        internal static void LoadObjectFromReaderWithColumnPrefix<T>(Func<T> generator, ref List<T> objectList, List<string> allColumns, SqlDataReader reader)
        {
            string prefix = typeof(T).Name;
            CrudField usedForAttr;
            MapToColumn column;
            string currentColumn = null;
            ObjectHelper.DataObjectInfo objectInfo = ObjectHelper.GetDataObjectInfo<T>();
            if (objectInfo.IdentityAttributes.Count == 0)
                throw new Exception(String.Format("Object {0} does not contain a DatabaseGeneratedAttribute with DatabaseGeneratedOption.Identity", prefix));
            // If this object already exists in the list, do not fill a duplicate object
            if (objectList.SingleOrDefault((p) => (int)objectInfo.GetMethods[objectInfo.IdentityAttributes.First().Key](p) == (int)reader[String.Format("{0}_{1}", prefix, objectInfo.IdentityAttributes.First().Key)]) != null)
                return;
            T newObject = generator();
            // Fill in a new parent and add it to the parent list
            for (int i = 0; i < objectInfo.Properties.Length; i++)
            {
                column = null;
                usedForAttr = null;

                if (objectInfo.MappingAttributes.TryGetValue(objectInfo.Properties[i].Name, out column))
                    currentColumn = column.ColumnName;
                else
                    currentColumn = objectInfo.Properties[i].Name;

                string parentColumnName = String.Format("{0}_{1}", prefix, currentColumn);
                usedForAttr = null;
                if ((objectInfo.AllAttributes.TryGetValue(objectInfo.Properties[i].Name, out usedForAttr) &&
                    ((usedForAttr.UsedFor & CrudFieldType.Read) == CrudFieldType.Read ||
                    usedForAttr.UsedFor == CrudFieldType.All)) ||
                    usedForAttr == null &&
                    allColumns.Contains(parentColumnName) &&
                    reader[parentColumnName] != DBNull.Value)
                    objectInfo.SetMethods[currentColumn](newObject, reader[parentColumnName]);
            }
            objectList.Add(newObject);
        }

        /// <summary>
        /// Map a child list to a parent list using a ForeignKeyAttribute
        /// </summary>
        /// <typeparam name="T1">The type of the parent object.</typeparam>
        /// <typeparam name="T2">The type of the child object.</typeparam>
        /// <param name="objectList1">The Parent List</param>
        /// <param name="objectList2">The Child List</param>
        internal static void MapRelatedObjects<T1, T2>(List<T1> objectList1, List<T2> objectList2)
        {
            DataObjectInfo parentDataObjectInfo = GetDataObjectInfo<T1>();
            DataObjectInfo childDataObjectInfo = GetDataObjectInfo<T2>();
            // To map the parent child objects we use a ForeignKeyAttribute in the child with the parent's key property: [ForeignKey("BlogId")]
            // Find the property in T1 that should contain a generic list of T2
            PropertyInfo childListProp = parentDataObjectInfo.InnerCollections.Where((p) => p.PropertyType.GenericTypeArguments[0].UnderlyingSystemType.FullName == typeof(T2).FullName).FirstOrDefault();
            if (childListProp == null) // If there is no generic list found in T1, Look for a single object
                childListProp = parentDataObjectInfo.Properties.Where((p) => p.PropertyType.FullName == typeof(T2).FullName).FirstOrDefault();
            if (childListProp == null)
                throw new TypeLoadException(String.Format("Type of {0} not found in {1}", typeof(T2).FullName, typeof(T1).FullName));

            // Find the property in T2 that contains the ForeignKey. We will use this to get the primary key value from the parent.
            KeyValuePair<string, ForeignKeyAttribute> foreignKey = childDataObjectInfo.ForeignKeyAttributes.FirstOrDefault();
            if (string.IsNullOrEmpty(foreignKey.Key))
                throw new Exception("No ForeignKeyAttribute found on child object.");

            if (childDataObjectInfo.Properties.Where((p) => p.Name == foreignKey.Key).FirstOrDefault().PropertyType.FullName != "System.Int32")
                throw new Exception("ForeignKeyAttribute must be on an int property.");

            foreach (var parent in objectList1)
            {
                //Set the prop with the filtered list of children
                List<T2> filteredList = GetForeignKeyFilteredList<T1, T2>(parent, objectList2, foreignKey.Key);
                if (IsCollectionType(childListProp.PropertyType))
                    childListProp.SetValue(parent, filteredList);
                else
                    childListProp.SetValue(parent, filteredList.FirstOrDefault());
            }
        }

        /// <summary>
        /// Get a filtered list of child object that have their foreign key match the parent.
        /// </summary>
        /// <typeparam name="T1">The type of the parent object.</typeparam>
        /// <typeparam name="T2">The type of the child object.</typeparam>
        /// <param name="parent">The parent object</param>
        /// <param name="children">The child List</param>
        /// <param name="foreignKey">The name of the foreign key property.</param>
        /// <returns></returns>
        private static List<T2> GetForeignKeyFilteredList<T1, T2>(T1 parent, List<T2> children, string foreignKey)
        {
            // Get the value of the parent's primary key.
            object pKeyValue = ObjectHelper.GetDataObjectInfo<T1>().GetMethods[foreignKey](parent);
            InvertedSoftware.DataBlock.ObjectHelper.DataObjectInfo childDataObjectInfo = ObjectHelper.GetDataObjectInfo<T2>();
            // Return a filtered list of children
            return children.Where((c) => (int)childDataObjectInfo.GetMethods[foreignKey](c) == (int)pKeyValue).ToList();
        }

        /// <summary>
        /// Check if a type is a generic collection.
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>true if this is a generic collection</returns>
        private static bool IsCollectionType(Type type)
        {
            // string implements IEnumerable, but for our purposes we don't consider it a collection.
            if (type == typeof(string)) return false;

            var interfaces = from inf in type.GetInterfaces()
                             where inf == typeof(IEnumerable) ||
                                 (inf.IsGenericType && inf.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                             select inf;
            return interfaces.Count() != 0;
        }
    }
}