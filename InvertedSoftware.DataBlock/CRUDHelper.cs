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

            SqlCommand cmd = new SqlCommand();
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
        #endregion
    }
}