// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED.
//
// Copyright (C) Inverted Software(TM). All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace InvertedSoftware.DataBlock
{
    /// <summary>
    /// This class is used for basic CRUD operations.
    /// </summary>
    public static class CRUDHelper
    {
        #region Create
        /// <summary>
        /// Adds an object to the database using a stored procedure.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="objectToAdd">The live object.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <returns>The new generated Row ID.</returns>
        public static int AddObject<T>(T objectToAdd, string sprocName, string stringConnection)
        {
            int newRecordID = 0;
            try
            {
                SqlParameter[] paramArray = ObjectHelper.GetSQLParametersFromPublicProperties<T>(objectToAdd, CrudFieldType.Create);
                newRecordID = Convert.ToInt32(SqlHelper.ExecuteScalar(stringConnection, CommandType.StoredProcedure, sprocName, paramArray));
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Adding object {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }

            return newRecordID;
        }

        /// <summary>
        /// Adds an object to the database using a stored procedure.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="objectToAdd">The live object.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <returns>The new generated Row ID.</returns>
        public static async Task<int> AddObjectAsync<T>(T objectToAdd, string sprocName, string stringConnection)
        {
            int newRecordID = 0;
            try
            {
                SqlParameter[] paramArray = ObjectHelper.GetSQLParametersFromPublicProperties<T>(objectToAdd, CrudFieldType.Create);
                newRecordID = Convert.ToInt32(await SqlHelper.ExecuteScalarAsync(stringConnection, CommandType.StoredProcedure, sprocName, paramArray));
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Adding object {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }

            return newRecordID;
        }
        #endregion

        #region Read
        /// <summary>
        /// Gets an object with its properties populated from a stored procedure.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="generator">Function to generate a new object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A data object.</returns>
        public static T GetObject<T>(Func<T> generator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
            T newobject;
            try
            {
                newobject = generator();
                using (SqlDataReader rdr = SqlHelper.ExecuteReader(stringConnection, CommandType.StoredProcedure, sprocName, commandParameters))
                {
                    PropertyInfo[] props = ObjectHelper.GetDataObjectInfo<T>().Properties;
                    List<string> columnList = ObjectHelper.GetColumnNames(rdr, sprocName);
                    while (rdr.Read())
                        ObjectHelper.LoadAs<T>(rdr, newobject, props, columnList, sprocName);
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting object {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }

            return newobject;
        }

        /// <summary>
        /// Gets an object with its properties populated from a stored procedure.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="generator">Function to generate a new object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A data object.</returns>
        public static async Task<T> GetObjectAsync<T>(Func<T> generator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
            T newobject;
            try
            {
                newobject = generator();
                using (SqlDataReader rdr = await SqlHelper.ExecuteReaderAsync(stringConnection, CommandType.StoredProcedure, sprocName, commandParameters))
                {
                    PropertyInfo[] props = ObjectHelper.GetDataObjectInfo<T>().Properties;
                    List<string> columnList = ObjectHelper.GetColumnNames(rdr, sprocName);
                    while (rdr.Read())
                        ObjectHelper.LoadAs<T>(rdr, newobject, props, columnList, sprocName);
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting object {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }

            return newobject;
        }

        /// <summary>
        /// Gets a list of objects with their properties populated from a stored procedure.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="generator">Function to generate a new object. Example: () => new MyCustomObject()</param>
        /// <param name="pageIndex">The 0 based page index.</param>
        /// <param name="rowsPerPage">Numbers of rows to return.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="virtualTotal">The total records in the database. Can be used to create paging navigation.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A list of objects.</returns>
        public static List<T> GetObjectList<T>(Func<T> generator, int pageIndex, int rowsPerPage, string sprocName, string stringConnection, out int virtualTotal, params SqlParameter[] commandParameters)
        {
            List<T> objectList = new List<T>();

            SqlParameter[] paramArray = new SqlParameter[]{ 
                new SqlParameter("@PageIndex", SqlDbType.Int){ Value = pageIndex},
                new SqlParameter("@PageSize", SqlDbType.Int){ Value = rowsPerPage},
                new SqlParameter("@TotalRecords", SqlDbType.Int){ Direction = ParameterDirection.ReturnValue }
            };

            if (commandParameters != null)
                paramArray = paramArray.Concat(commandParameters).ToArray();

            SqlCommand cmd = SqlHelper.CommandPool.GetObject();
            using (SqlConnection conn = new SqlConnection(stringConnection))
            {
                try
                {
                    SqlHelper.PrepareCommand(cmd, conn, null, CommandType.StoredProcedure, sprocName, paramArray);
                    using (SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        PropertyInfo[] props = ObjectHelper.GetDataObjectInfo<T>().Properties;
                        List<string> columnList = ObjectHelper.GetColumnNames(rdr, sprocName);
                        T newobject;
                        while (rdr.Read())
                        {
                            newobject = generator();
                            ObjectHelper.LoadAs<T>(rdr, newobject, props, columnList, sprocName);
                            objectList.Add(newobject);
                        }
                    }
                    virtualTotal = Convert.ToInt32(paramArray.Where(p => p.ParameterName == "@TotalRecords").First().Value);
                    cmd.Parameters.Clear();
                }
                catch (Exception e)
                {
                    throw new DataBlockException(String.Format("Error Getting object list {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
                }
                finally
                {
                    SqlHelper.CommandPool.PutObject(cmd);
                }
            }

            return objectList;
        }

        public static async Task<ObjectListResult<T>> GetObjectListAsync<T>(Func<T> generator, int pageIndex, int rowsPerPage, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
            List<T> objectList = new List<T>();
            ObjectListResult<T> result = new ObjectListResult<T>();

            SqlParameter[] paramArray = new SqlParameter[]{ 
                new SqlParameter("@PageIndex", SqlDbType.Int){ Value = pageIndex},
                new SqlParameter("@PageSize", SqlDbType.Int){ Value = rowsPerPage},
                new SqlParameter("@TotalRecords", SqlDbType.Int){ Direction = ParameterDirection.ReturnValue }
            };

            if (commandParameters != null)
                paramArray = paramArray.Concat(commandParameters).ToArray();

            SqlCommand cmd = SqlHelper.CommandPool.GetObject();
            using (SqlConnection conn = new SqlConnection(stringConnection))
            {
                try
                {
                    SqlHelper.PrepareCommand(cmd, null, CommandType.StoredProcedure, sprocName, paramArray);
                    if (conn.State != ConnectionState.Open)
                        await conn.OpenAsync();
                    cmd.Connection = conn;
                    using (SqlDataReader rdr = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                    {
                        PropertyInfo[] props = ObjectHelper.GetDataObjectInfo<T>().Properties;
                        List<string> columnList = ObjectHelper.GetColumnNames(rdr, sprocName);
                        T newobject;
                        while (rdr.Read())
                        {
                            newobject = generator();
                            ObjectHelper.LoadAs<T>(rdr, newobject, props, columnList, sprocName);
                            objectList.Add(newobject);
                        }
                    }
                    result.CurrentPage = objectList;
                    result.VirtualTotal = Convert.ToInt32(paramArray.Where(p => p.ParameterName == "@TotalRecords").First().Value);
                    cmd.Parameters.Clear();
                }
                catch (Exception e)
                {
                    throw new DataBlockException(String.Format("Error Getting object list {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
                }
                finally
                {
                    SqlHelper.CommandPool.PutObject(cmd);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets a list of objects with their properties populated from a stored procedure.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="generator">Function to generate a new object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <returns>A list of objects.</returns>
        public static List<T> GetObjectList<T>(Func<T> generator, string sprocName, string stringConnection)
        {
            List<T> objectList = new List<T>();

            try
            {
                using (SqlDataReader reader = SqlHelper.ExecuteReader(stringConnection, CommandType.StoredProcedure, sprocName))
                {
                    PropertyInfo[] props = ObjectHelper.GetDataObjectInfo<T>().Properties;
                    List<string> columnList = ObjectHelper.GetColumnNames(reader, sprocName);
                    T newobject;
                    while (reader.Read())
                    {
                        newobject = generator();
                        ObjectHelper.LoadAs<T>(reader, newobject, props, columnList, sprocName);
                        objectList.Add(newobject);
                    }
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting object list {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }

            return objectList;
        }

        /// <summary>
        /// Gets a list of objects with their properties populated from a stored procedure.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="generator">Function to generate a new object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <returns>A list of objects.</returns>
        public static async Task<List<T>> GetObjectListAsync<T>(Func<T> generator, string sprocName, string stringConnection)
        {
            List<T> objectList = new List<T>();

            try
            {
                using (SqlDataReader reader = await SqlHelper.ExecuteReaderAsync(stringConnection, CommandType.StoredProcedure, sprocName))
                {
                    PropertyInfo[] props = ObjectHelper.GetDataObjectInfo<T>().Properties;
                    List<string> columnList = ObjectHelper.GetColumnNames(reader, sprocName);
                    T newobject;
                    while (reader.Read())
                    {
                        newobject = generator();
                        ObjectHelper.LoadAs<T>(reader, newobject, props, columnList, sprocName);
                        objectList.Add(newobject);
                    }
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting object list {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }

            return objectList;
        }

        /// <summary>
        /// Get a list of strings.
        /// </summary>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="columnName">The name of the column containing the result. Leave empty if this is the first column in the result.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>List of string</returns>
        public static List<string> GetStringList(string sprocName, string columnName, string stringConnection, params SqlParameter[] commandParameters)
        {
            List<string> stringList = new List<string>();

            try
            {
                using (SqlDataReader reader = SqlHelper.ExecuteReader(stringConnection, CommandType.StoredProcedure, sprocName, commandParameters))
                {
                    if (string.IsNullOrWhiteSpace(columnName))
                        while (reader.Read())
                            stringList.Add(reader.GetString(0));
                    else
                        while (reader.Read())
                            stringList.Add(reader.GetString(reader.GetOrdinal(columnName)));
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting string list. Stored Procedure: {0}", sprocName), e);
            }

            return stringList;
        }

        /// <summary>
        /// Get a list of integers.
        /// </summary>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="columnName">The name of the column containing the result. Leave empty if this is the first column in the result.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>List of int</returns>
        public static List<int> GetIntList(string sprocName, string columnName, string stringConnection, params SqlParameter[] commandParameters)
        {
            List<int> intList = new List<int>();

            try
            {
                using (SqlDataReader reader = SqlHelper.ExecuteReader(stringConnection, CommandType.StoredProcedure, sprocName, commandParameters))
                {
                    if (string.IsNullOrWhiteSpace(columnName))
                        while (reader.Read())
                            intList.Add(reader.GetInt32(0));
                    else
                        while (reader.Read())
                            intList.Add(reader.GetInt32(reader.GetOrdinal(columnName)));
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting int list. Stored Procedure: {0}", sprocName), e);
            }

            return intList;
        }

        /// <summary>
        /// Get a list of decimals.
        /// </summary>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="columnName">The name of the column containing the result. Leave empty if this is the first column in the result.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>List of decimal</returns>
        public static List<decimal> GetDecimalList(string sprocName, string columnName, string stringConnection, params SqlParameter[] commandParameters)
        {
            List<decimal> decimalList = new List<decimal>();

            try
            {
                using (SqlDataReader reader = SqlHelper.ExecuteReader(stringConnection, CommandType.StoredProcedure, sprocName, commandParameters))
                {
                    if (string.IsNullOrWhiteSpace(columnName))
                        while (reader.Read())
                            decimalList.Add(reader.GetDecimal(0));
                    else
                        while (reader.Read())
                            decimalList.Add(reader.GetDecimal(reader.GetOrdinal(columnName)));
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting decimal list. Stored Procedure: {0}", sprocName), e);
            }

            return decimalList;
        }

        /// <summary>
        /// Get a list of objects with eager loaded child objects from the next result set in the same query. Parent objects are in the first result.
        /// </summary>
        /// <typeparam name="T1">The parent type.</typeparam>
        /// <typeparam name="T2">The child type.</typeparam>
        /// <param name="parentGenerator">Function to generate a new parent object. Example: () => new MyCustomObject()</param>
        /// <param name="childGenerator">Function to generate a new child. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A list of objects with children.</returns>
        public static List<T1> GetEagerLoadedObjectListFromMultipleResults<T1, T2>(Func<T1> parentGenerator, Func<T2> childGenerator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
            return GetEagerLoadedObjectListFromMultipleResults<T1, T2, T2, T2>(parentGenerator, childGenerator, null, null, sprocName, stringConnection, commandParameters);
        }

        /// <summary>
        /// Get a list of objects with eager loaded child objects from the next result set in the same query. Parent objects are in the first result.
        /// </summary>
        /// <typeparam name="T1">The parent type.</typeparam>
        /// <typeparam name="T2">The first child type.</typeparam>
        /// <typeparam name="T3">The second child type.</typeparam>
        /// <param name="parentGenerator">Function to generate a new parent object. Example: () => new MyCustomObject()</param>
        /// <param name="childGenerator">Function to generate a new child object. Example: () => new MyCustomObject()</param>
        /// <param name="secondChildGenerator">Function to generate a new second child object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The string connection.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A list of objects with children.</returns>
        public static List<T1> GetEagerLoadedObjectListFromMultipleResults<T1, T2, T3>(Func<T1> parentGenerator, Func<T2> childGenerator, Func<T3> secondChildGenerator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
            return GetEagerLoadedObjectListFromMultipleResults<T1, T2, T3, T3>(parentGenerator, childGenerator, secondChildGenerator, null, sprocName, stringConnection, commandParameters);
        }

        /// <summary>
        /// Get a list of objects with eager loaded child objects from the next result set in the same query. Parent objects are in the first result.
        /// </summary>
        /// <typeparam name="T1">The parent type.</typeparam>
        /// <typeparam name="T2">The first child type.</typeparam>
        /// <typeparam name="T3">The second child type.</typeparam>
        /// <typeparam name="T4">The third child type</typeparam>
        /// <param name="parentGenerator">Function to generate a new parent object. Example: () => new MyCustomObject()</param>
        /// <param name="childGenerator">Function to generate a new child object. Example: () => new MyCustomObject()</param>
        /// <param name="secondChildGenerator">Function to generate a new second child object. Example: () => new MyCustomObject()</param>
        /// <param name="thirdChildGenerator">Function to generate a new third child object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A list of objects with children.</returns>
        public static List<T1> GetEagerLoadedObjectListFromMultipleResults<T1, T2, T3, T4>(Func<T1> parentGenerator, Func<T2> childGenerator, Func<T3> secondChildGenerator, Func<T4> thirdChildGenerator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
            List<T1> objectList1 = null;
            List<T2> objectList2 = null;
            List<T3> objectList3 = null;
            List<T4> objectList4 = null;
            try
            {
                using (SqlDataReader reader = SqlHelper.ExecuteReader(stringConnection, CommandType.StoredProcedure, sprocName, commandParameters))
                {
                    objectList1 = GetObjectListFromReader<T1>(parentGenerator, sprocName, reader);
                    reader.NextResult();
                    objectList2 = GetObjectListFromReader<T2>(childGenerator, sprocName, reader);

                    if (objectList1 != null && objectList2 != null) // Map the objects
                        ObjectHelper.MapRelatedObjects(objectList1, objectList2);
                    if (secondChildGenerator != null && objectList1 != null)
                    {
                        reader.NextResult();
                        objectList3 = GetObjectListFromReader<T3>(secondChildGenerator, sprocName, reader);
                        ObjectHelper.MapRelatedObjects(objectList1, objectList3);
                    }

                    if (thirdChildGenerator != null)
                    {
                        reader.NextResult();
                        objectList4 = GetObjectListFromReader<T4>(thirdChildGenerator, sprocName, reader);
                        ObjectHelper.MapRelatedObjects(objectList1, objectList4);
                    }
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting object list {0}. Stored Procedure: {1}", typeof(T1).FullName, sprocName), e);
            }
            return objectList1;
        }

        /// <summary>
        /// Get a list of objects with eager loaded child objects from a flat inner join query. The query output colums follow the the pattern: ObjectTypeName_PropertyName.
        /// For Example: Category_CategoryID, Category_CategoryName, Product_CategoryID, Product_ProductID, Product_ProductName.
        /// </summary>
        /// <typeparam name="T1">The parent type.</typeparam>
        /// <typeparam name="T2">The child type.</typeparam>
        /// <param name="parentGenerator">Function to generate a new parent object. Example: () => new MyCustomObject()</param>
        /// <param name="childGenerator">Function to generate a new child object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A list of objects with children.</returns>
        public static List<T1> GetEagerLoadedObjectListFromInnerJoinQuery<T1, T2>(Func<T1> parentGenerator, Func<T2> childGenerator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
           return  GetEagerLoadedObjectListFromInnerJoinQuery<T1, T2, T2, T2>(parentGenerator, childGenerator, null, null, sprocName, stringConnection, commandParameters);
        }

        /// <summary>
        /// Get a list of objects with eager loaded child objects from a flat inner join query. The query output colums follow the the pattern: ObjectTypeName_PropertyName.
        /// For Example: Category_CategoryID, Category_CategoryName, Product_CategoryID, Product_ProductID, Product_ProductName.
        /// </summary>
        /// <typeparam name="T1">The parent type.</typeparam>
        /// <typeparam name="T2">The child type.</typeparam>
        /// <typeparam name="T3">The second child type.</typeparam>
        /// <param name="parentGenerator">Function to generate a new parent object. Example: () => new MyCustomObject()</param>
        /// <param name="childGenerator">Function to generate a new child object. Example: () => new MyCustomObject()</param>
        /// <param name="secondChildGenerator">Function to generate a new second child object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A list of objects with children.</returns>
        public static List<T1> GetEagerLoadedObjectListFromInnerJoinQuery<T1, T2, T3>(Func<T1> parentGenerator, Func<T2> childGenerator, Func<T3> secondChildGenerator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
           return  GetEagerLoadedObjectListFromInnerJoinQuery<T1, T2, T3, T3>(parentGenerator, childGenerator, secondChildGenerator, null, sprocName, stringConnection, commandParameters);
        }

        /// <summary>
        /// Get a list of objects with eager loaded child objects from a flat inner join query. The query output colums follow the the pattern: ObjectTypeName_PropertyName.
        /// For Example: Category_CategoryID, Category_CategoryName, Product_CategoryID, Product_ProductID, Product_ProductName.
        /// </summary>
        /// <typeparam name="T1">The parent type.</typeparam>
        /// <typeparam name="T2">The child type.</typeparam>
        /// <typeparam name="T3">The second child type.</typeparam>
        /// <typeparam name="T4">The third child type.</typeparam>
        /// <param name="parentGenerator">Function to generate a new parent object. Example: () => new MyCustomObject()</param>
        /// <param name="childGenerator">Function to generate a new child object. Example: () => new MyCustomObject()</param>
        /// <param name="secondChildGenerator">Function to generate a new second child object. Example: () => new MyCustomObject()</param>
        /// <param name="thirdChildGenerator">Function to generate a new third child object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A list of objects with children.</returns>
        public static List<T1> GetEagerLoadedObjectListFromInnerJoinQuery<T1, T2, T3, T4>(Func<T1> parentGenerator, Func<T2> childGenerator, Func<T3> secondChildGenerator, Func<T4> thirdChildGenerator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
            List<T1> objectList1 = new List<T1>();
            List<T2> objectList2 = new List<T2>();
            List<T3> objectList3 = new List<T3>();
            List<T4> objectList4 = new List<T4>();
            try
            {
                using (SqlDataReader reader = SqlHelper.ExecuteReader(stringConnection, CommandType.StoredProcedure, sprocName, commandParameters))
                {
                    List<string> allColumns = ObjectHelper.GetColumnNames(reader, sprocName);

                    while (reader.Read())
                    {
                        ObjectHelper.LoadObjectFromReaderWithColumnPrefix<T1>(parentGenerator, ref objectList1, allColumns, reader);
                        ObjectHelper.LoadObjectFromReaderWithColumnPrefix<T2>(childGenerator, ref objectList2, allColumns, reader);
                        if (secondChildGenerator != null)
                            ObjectHelper.LoadObjectFromReaderWithColumnPrefix<T3>(secondChildGenerator, ref objectList3, allColumns, reader);
                        if (thirdChildGenerator != null)
                            ObjectHelper.LoadObjectFromReaderWithColumnPrefix<T4>(thirdChildGenerator, ref objectList4, allColumns, reader);
                    }
                }

                ObjectHelper.MapRelatedObjects(objectList1, objectList2);
                if (secondChildGenerator != null)
                    ObjectHelper.MapRelatedObjects(objectList1, objectList3);
                if (thirdChildGenerator != null)
                    ObjectHelper.MapRelatedObjects(objectList1, objectList4);
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting object list {0}. Stored Procedure: {1}", typeof(T1).FullName, sprocName), e);
            }
            return objectList1;
        }

        /// <summary>
        /// Gets a list of objects with their properties populated from an open SqlDataReader
        /// </summary>
        /// <typeparam name="T">he object's type.</typeparam>
        /// <param name="generator">Function to generate a new object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="reader">An open SqlDataReader</param>
        /// <returns>A list of objects.</returns>
        private static List<T> GetObjectListFromReader<T>(Func<T> generator, string sprocName, SqlDataReader reader)
        {
            List<T> objectList = new List<T>();

            try
            {
                PropertyInfo[] props = ObjectHelper.GetDataObjectInfo<T>().Properties;
                List<string> columnList = ObjectHelper.GetColumnNames(reader, String.Format("sprocName{0}", typeof(T).Name), false);
                if (columnList.Count == 1 && string.IsNullOrWhiteSpace(columnList[0])) // The select is NULL
                    return objectList;
                T newobject;
                while (reader.Read())
                {
                    newobject = generator();
                    ObjectHelper.LoadAs<T>(reader, newobject, props, columnList, sprocName);
                    objectList.Add(newobject);
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting object list {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }

            return objectList;
        }

        /// <summary>
        /// Gets a list of objects with their properties populated from a stored procedure.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="generator">Function to generate a new object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A list of objects.</returns>
        public static List<T> GetObjectList<T>(Func<T> generator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
            List<T> objectList = new List<T>();
            try
            {
                using (SqlDataReader reader = SqlHelper.ExecuteReader(stringConnection, CommandType.StoredProcedure, sprocName, commandParameters))
                {
                    PropertyInfo[] props = ObjectHelper.GetDataObjectInfo<T>().Properties;
                    List<string> columnList = ObjectHelper.GetColumnNames(reader, sprocName);
                    T newobject;
                    while (reader.Read())
                    {
                        newobject = generator();
                        ObjectHelper.LoadAs<T>(reader, newobject, props, columnList, sprocName);
                        objectList.Add(newobject);
                    }
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting object list {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }

            return objectList;
        }

        /// <summary>
        /// Gets a list of objects with their properties populated from a stored procedure.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="generator">Function to generate a new object. Example: () => new MyCustomObject()</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        /// <param name="commandParameters">Any parameters required by the stored procedure.</param>
        /// <returns>A list of objects.</returns>
        public static async Task<List<T>> GetObjectListAsync<T>(Func<T> generator, string sprocName, string stringConnection, params SqlParameter[] commandParameters)
        {
            List<T> objectList = new List<T>();
            try
            {
                using (SqlDataReader reader = await SqlHelper.ExecuteReaderAsync(stringConnection, CommandType.StoredProcedure, sprocName, commandParameters))
                {
                    PropertyInfo[] props = ObjectHelper.GetDataObjectInfo<T>().Properties;
                    List<string> columnList = ObjectHelper.GetColumnNames(reader, sprocName);
                    T newobject;
                    while (reader.Read())
                    {
                        newobject = generator();
                        ObjectHelper.LoadAs<T>(reader, newobject, props, columnList, sprocName);
                        objectList.Add(newobject);
                    }
                }
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Getting object list {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }

            return objectList;
        }
        #endregion

        #region Update
        /// <summary>
        /// Updates a record in the database based on the values of an object's properties.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="objectToUpdate">The live object.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        public static void UpdateObject<T>(T objectToUpdate, string sprocName, string stringConnection)
        {
            try
            {
                SqlParameter[] paramArray = ObjectHelper.GetSQLParametersFromPublicProperties<T>(objectToUpdate, CrudFieldType.Update);
                SqlHelper.ExecuteNonQuery(stringConnection, CommandType.StoredProcedure, sprocName, paramArray);
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Updating object {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }
        }

        /// <summary>
        /// Updates a record in the database based on the values of an object's properties.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="objectToUpdate">The live object.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        public static async void UpdateObjectAsync<T>(T objectToUpdate, string sprocName, string stringConnection)
        {
            try
            {
                SqlParameter[] paramArray = ObjectHelper.GetSQLParametersFromPublicProperties<T>(objectToUpdate, CrudFieldType.Update);
                await SqlHelper.ExecuteNonQueryAsync(stringConnection, CommandType.StoredProcedure, sprocName, paramArray);
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Updating object {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }
        }
        #endregion

        #region Delete
        /// <summary>
        /// Deletes a row in the database based on the values of an object's properties.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="objectToDelete">The live object.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        public static void DeleteObject<T>(T objectToDelete, string sprocName, string stringConnection)
        {
            try
            {
                SqlParameter[] paramArray = ObjectHelper.GetSQLParametersFromPublicProperties<T>(objectToDelete, CrudFieldType.Delete);
                SqlHelper.ExecuteNonQuery(stringConnection, CommandType.StoredProcedure, sprocName, paramArray);
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Deleting object {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }
        }

        /// <summary>
        /// Deletes a row in the database based on the values of an object's properties.
        /// </summary>
        /// <typeparam name="T">The object's type.</typeparam>
        /// <param name="objectToDelete">The live object.</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        public static async void DeleteObjectAsync<T>(T objectToDelete, string sprocName, string stringConnection)
        {
            try
            {
                SqlParameter[] paramArray = ObjectHelper.GetSQLParametersFromPublicProperties<T>(objectToDelete, CrudFieldType.Delete);
                await SqlHelper.ExecuteNonQueryAsync(stringConnection, CommandType.StoredProcedure, sprocName, paramArray);
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Deleting object {0}. Stored Procedure: {1}", typeof(T).FullName, sprocName), e);
            }
        }

        /// <summary>
        /// Deletes a record in the database.
        /// </summary>
        /// <param name="rowID">The ID to use when deleting.</param>
        /// <param name="parameterName">The ID parameter name on the stored procedure to use. Example: @CustomerID</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        public static void DeleteRecord(int rowID, string parameterName, string sprocName, string stringConnection)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(stringConnection, CommandType.StoredProcedure, sprocName, new SqlParameter(parameterName, SqlDbType.Int) { Value = rowID });
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Deleting Row. Stored Procedure: {0}", sprocName), e);
            }
        }

        /// <summary>
        /// Deletes a record in the database.
        /// </summary>
        /// <param name="rowID">The ID to use when deleting.</param>
        /// <param name="parameterName">The ID parameter name on the stored procedure to use. Example: @CustomerID</param>
        /// <param name="sprocName">The name of the stored procedure to use.</param>
        /// <param name="stringConnection">The string connection.</param>
        public static async void DeleteRecordAsync(int rowID, string parameterName, string sprocName, string stringConnection)
        {
            try
            {
                await SqlHelper.ExecuteNonQueryAsync(stringConnection, CommandType.StoredProcedure, sprocName, new SqlParameter(parameterName, SqlDbType.Int) { Value = rowID });
            }
            catch (Exception e)
            {
                throw new DataBlockException(String.Format("Error Deleting Row. Stored Procedure: {0}", sprocName), e);
            }
        }
        #endregion
    }
}