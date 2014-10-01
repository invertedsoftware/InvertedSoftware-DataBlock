// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED.
//
// Copyright (C) Inverted Software(TM). All rights reserved.
//

using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace InvertedSoftware.DataBlock
{
    /// <summary>
    /// The SqlHelper class is intended to encapsulate high performance, common uses of SqlClient.
    /// </summary>
    public static class SqlHelper
    {
        public static ObjectPool<SqlCommand> CommandPool = new ObjectPool<SqlCommand>(() => new SqlCommand());

        /// <summary>
        /// Executes a Transact-SQL statement against the connection and returns the number of rows affected.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connectionString">a valid connection string for a SqlConnection</param>
        /// <param name="commandType">The CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">The stored procedure name or T-SQL command.</param>
        /// <param name="commandParameters">An array of SqlParamters used to execute the command.</param>
        /// <returns>An int representing the number of rows affected by the command.</returns>
        public static int ExecuteNonQuery(string connectionString, CommandType cmdType, string cmdText, params SqlParameter[] commandParameters)
        {
            int val;
            SqlCommand cmd = CommandPool.GetObject();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    PrepareCommand(cmd, conn, null, cmdType, cmdText, commandParameters);
                    val = cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                CommandPool.PutObject(cmd);
            }

            return val;
        }

        /// <summary>
        /// Executes a Transact-SQL statement against the connection and returns the number of rows affected.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connectionString">a valid connection string for a SqlConnection</param>
        /// <param name="commandType">The CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">The stored procedure name or T-SQL command.</param>
        /// <param name="commandParameters">An array of SqlParamters used to execute the command.</param>
        /// <returns>An int representing the number of rows affected by the command.</returns>
        public static async Task<int> ExecuteNonQueryAsync(string connectionString, CommandType cmdType, string cmdText, params SqlParameter[] commandParameters)
        {
            int val;
            SqlCommand cmd = CommandPool.GetObject();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    PrepareCommand(cmd, null, cmdType, cmdText, commandParameters);
                    if (conn.State != ConnectionState.Open)
                        await conn.OpenAsync();
                    cmd.Connection = conn;
                    val = await cmd.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                CommandPool.PutObject(cmd);
            }

            return val;
        }

        /// <summary>
        /// Executes a Transact-SQL statement against the connection and returns the number of rows affected.
        /// </summary>
        /// <param name="conn">The connection to use.</param>
        /// <param name="tran">The SqlTransaction to use.</param>
        /// <param name="cmdType">The CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">The stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">An array of SqlParamters used to execute the command.</param>
        /// <returns>An int representing the number of rows affected by the command.</returns>
        public static int ExecuteNonQuery(SqlConnection conn, SqlTransaction tran, CommandType cmdType, string cmdText, params SqlParameter[] commandParameters)
        {
            int val;
            SqlCommand cmd = CommandPool.GetObject();
            try
            {
                PrepareCommand(cmd, conn, tran, cmdType, cmdText, commandParameters);
                val = cmd.ExecuteNonQuery();
            }
            finally
            {
                cmd.Transaction = null;
                CommandPool.PutObject(cmd);
            }

            return val;
        }

        /// <summary>
        /// Execute a SqlCommand that returns a resultset against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  SqlDataReader r = ExecuteReader(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connectionString">a valid connection string for a SqlConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">an array of SqlParamters used to execute the command</param>
        /// <returns>A SqlDataReader containing the results.</returns>
        public static SqlDataReader ExecuteReader(string connectionString, CommandType cmdType, string cmdText, params SqlParameter[] commandParameters)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            SqlCommand cmd = conn.CreateCommand();

            // we use a try/catch here because if the method throws an exception we want to 
            // close the connection throw code, because no datareader will exist, hence the 
            // commandBehaviour.CloseConnection will not work
            try
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, commandParameters);
                SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                return rdr;
            }
            catch
            {
                cmd.Dispose();
                if (conn.State == ConnectionState.Open)
                    conn.Close();
                conn.Dispose();
                throw;
            }
        }


        /// <summary>
        /// Execute a SqlCommand that returns a resultset against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  SqlDataReader r = ExecuteReader(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connectionString">a valid connection string for a SqlConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">an array of SqlParamters used to execute the command</param>
        /// <returns>A SqlDataReader containing the results.</returns>
        public static async Task<SqlDataReader> ExecuteReaderAsync(string connectionString, CommandType cmdType, string cmdText, params SqlParameter[] commandParameters)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            SqlCommand cmd = conn.CreateCommand();

            // we use a try/catch here because if the method throws an exception we want to 
            // close the connection throw code, because no datareader will exist, hence the 
            // commandBehaviour.CloseConnection will not work
            try
            {
                PrepareCommand(cmd, null, cmdType, cmdText, commandParameters);
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();
                cmd.Connection = conn;
                SqlDataReader rdr = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
                return rdr;
            }
            catch
            {
                cmd.Dispose();
                if (conn.State == ConnectionState.Open)
                    conn.Close();
                conn.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Sends the CommandText to the Connection and builds a SqlDataReader withing the current transaction using the provided parameters.
        /// </summary>
        /// <param name="conn">The connection to use.</param>
        /// <param name="tran">The SqlTransaction to use.</param>
        /// <param name="cmdType">The CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">The stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">>An array of SqlParamters used to execute the command.</param>
        /// <returns>A SqlDataReader containing the results.</returns>
        public static SqlDataReader ExecuteReader(SqlConnection conn, SqlTransaction tran, CommandType cmdType, string cmdText, params SqlParameter[] commandParameters)
        {
            SqlCommand cmd = new SqlCommand();
            PrepareCommand(cmd, conn, tran, cmdType, cmdText, commandParameters);
            SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            return rdr;
        }

        /// <summary>
        /// Execute a SqlCommand that returns the first column of the first record against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  Object obj = ExecuteScalar(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connectionString">A valid connection string for a SqlConnection</param>
        /// <param name="commandType">The CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">The stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">An array of SqlParamters used to execute the command.</param>
        /// <returns>The first column of the first row in the result set, or a null reference if the result set is empty. Returns a maximum of 2033 characters. This object should be converted to the expected type using Convert.To{Type}.</returns>
        public static object ExecuteScalar(string connectionString, CommandType cmdType, string cmdText, params SqlParameter[] commandParameters)
        {
            object val;
            SqlCommand cmd = CommandPool.GetObject();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    PrepareCommand(cmd, connection, null, cmdType, cmdText, commandParameters);
                    val = cmd.ExecuteScalar();
                }
            }
            finally
            {
                CommandPool.PutObject(cmd);
            }

            return val;
        }

        /// <summary>
        /// Execute a SqlCommand that returns the first column of the first record against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <param name="conn">The connection to use.</param>
        /// <param name="cmdType">The CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">The stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">An array of SqlParamters used to execute the command.</param>
        /// <returns>The first column of the first row in the result set, or a null reference if the result set is empty. Returns a maximum of 2033 characters. This object should be converted to the expected type using Convert.To{Type}.</returns>
        public static async Task<object> ExecuteScalarAsync(string connectionString, CommandType cmdType, string cmdText, params SqlParameter[] commandParameters)
        {
            object val;
            SqlCommand cmd = CommandPool.GetObject();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    PrepareCommand(cmd, null, cmdType, cmdText, commandParameters);
                    if (connection.State != ConnectionState.Open)
                        await connection.OpenAsync();
                    cmd.Connection = connection;
                    val = await cmd.ExecuteScalarAsync();
                }
            }
            finally
            {
                CommandPool.PutObject(cmd);
            }

            return val;
        }

        /// <summary>
        /// Execute a SqlCommand that returns the first column of the first record against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <param name="conn">The connection to use.</param>
        /// <param name="tran">The SqlTransaction to use.</param>
        /// <param name="cmdType">The CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">The stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">An array of SqlParamters used to execute the command.</param>
        /// <returns>The first column of the first row in the result set, or a null reference if the result set is empty. Returns a maximum of 2033 characters. This object should be converted to the expected type using Convert.To{Type}.</returns>
        public static object ExecuteScalar(SqlConnection conn, SqlTransaction tran, CommandType cmdType, string cmdText, params SqlParameter[] commandParameters)
        {
            object val;
            SqlCommand cmd = CommandPool.GetObject();
            try
            {
                PrepareCommand(cmd, conn, tran, cmdType, cmdText, commandParameters);
                val = cmd.ExecuteScalar();
            }
            finally
            {
                cmd.Transaction = null;
                CommandPool.PutObject(cmd);
            }

            return val;
        }

        /// <summary>
        /// Prepare a command for execution
        /// </summary>
        /// <param name="cmd">SqlCommand object</param>
        /// <param name="conn">SqlConnection object</param>
        /// <param name="trans">SqlTransaction object</param>
        /// <param name="cmdType">Cmd type e.g. stored procedure or text</param>
        /// <param name="cmdText">Command text, e.g. Select * from Products</param>
        /// <param name="cmdParms">SqlParameters to use in the command</param>
        public static void PrepareCommand(SqlCommand cmd, SqlConnection conn, SqlTransaction trans, CommandType cmdType, string cmdText, SqlParameter[] cmdParms)
        {
            cmd.Parameters.Clear();

            if (conn.State != ConnectionState.Open)
                conn.Open();

            cmd.Connection = conn;
            cmd.CommandText = cmdText;
            cmd.CommandType = cmdType;
            cmd.Transaction = trans;

            if (cmdParms != null)
            {
                foreach (SqlParameter parm in cmdParms)
                    if (parm != null)
                        cmd.Parameters.Add(parm);
            }
        }

        /// <summary>
        /// Prepare a command for execution
        /// </summary>
        /// <param name="cmd">SqlCommand object</param>
        /// <param name="trans">SqlTransaction object</param>
        /// <param name="cmdType">Cmd type e.g. stored procedure or text</param>
        /// <param name="cmdText">Command text, e.g. Select * from Products</param>
        /// <param name="cmdParms">SqlParameters to use in the command</param>
        public static void PrepareCommand(SqlCommand cmd, SqlTransaction trans, CommandType cmdType, string cmdText, SqlParameter[] cmdParms)
        {
            cmd.Parameters.Clear();
            cmd.CommandText = cmdText;
            cmd.CommandType = cmdType;
            cmd.Transaction = trans;

            if (cmdParms != null)
            {
                foreach (SqlParameter parm in cmdParms)
                    if (parm != null)
                        cmd.Parameters.Add(parm);
            }
        }
    }
}