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
    internal class ObjectHelper
    {
        /// <summary>
        /// Keeps metadata information about a POCO object
        /// </summary>
        internal struct DataObjectInfo
        {
            public PropertyInfo[] Properties { get; set; }
            public PropertyInfo[] InnerCollections { get; set; }
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
                    AllAttributes = new Dictionary<string, CrudField>(),
                    ForeignKeyAttributes = new Dictionary<string, ForeignKeyAttribute>(),
                    IdentityAttributes = new Dictionary<string, DatabaseGeneratedAttribute>(),
                    SetMethods = new Dictionary<string, Action<object, object>>(),
                    GetMethods = new Dictionary<string, Func<object, object>>()
                };

                foreach (var property in dataObjectInfo.Properties)
                {
                    // Fill in attributes
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
        internal static List<string> GetColumnNames(SqlDataReader reader, string sprocName)
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
        internal static SqlParameter[] GetSQLParametersFromPublicProperties<T>(object dataObject, CrudFieldType usedFor)
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
                string parentColumnName = String.Format("{0}_{1}", prefix, objectInfo.Properties[i].Name);

                if (allColumns.Contains(parentColumnName) && reader[parentColumnName] != DBNull.Value)
                    objectInfo.SetMethods[objectInfo.Properties[i].Name](newObject, reader[parentColumnName]);
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
                childListProp.SetValue(parent, filteredList);
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