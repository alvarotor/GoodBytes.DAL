using GoodBytes.DAL.Base.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace GoodBytes.DAL
{
	public class DALService : BaseService, IDALInterface
	{
		private readonly string _path;

		private SqlConnection _dataConnectionSql;
		private SqlTransaction _dataTransaction;

		private List<SqlParameter> parameters;

		private string _returnValue;
		private long _rowsAffected;
		private object _scalarValue;

		public long RowsAffected
		{
			get { return _rowsAffected; }
		}

		public object ScalarValue
		{
			get { return _scalarValue; }
		}

		public string ReturnValue
		{
			get { return _returnValue; }
		}

		#region Creation

		public DALService(string path)
		{
			_path = path;
		}

		~DALService()
		{
			Dispose();
		}

		public override void Dispose()
		{
			Disconnect();
			GC.SuppressFinalize(this);
		}

		//public DALService Clone()
		//{
		//	return (DALService)MemberwiseClone();
		//}

		#endregion

		#region Connection

		public object Connection
		{
			set { _dataConnectionSql = (SqlConnection)value; }
			get { return _dataConnectionSql; }
		}

		public void Connect()
		{
			var strConn = _path;
			_dataConnectionSql = new SqlConnection(strConn);
			if (_dataConnectionSql.State != ConnectionState.Open)
				_dataConnectionSql.Open();
			_dataTransaction = _dataConnectionSql.BeginTransaction();
		}

		public void Disconnect()
		{
			if (_dataConnectionSql != null && _dataConnectionSql.State != ConnectionState.Closed)
			{
				_dataConnectionSql.Close();
				_dataConnectionSql.Dispose();
				_dataConnectionSql = null;
			}
		}

		public void TransactionFinish(bool finishedProperly = true)
		{
			if (finishedProperly)
				_dataTransaction.Commit();
			else
				if (_dataTransaction.Connection != null)
				_dataTransaction.Rollback();
			_dataTransaction.Dispose();
		}

		public object Transaction
		{
			set { _dataTransaction = (SqlTransaction)value; }
			get { return _dataTransaction; }
		}

		#endregion

		/// <summary>
		/// Adds a parameter to the query or store procedure.
		/// </summary>
		/// <returns>
		/// Return true if parameter was added successfully.
		/// </returns>
		/// <param name="text">Name of the parameter.</param>
		/// <param name="value">Value of the parameter.</param>
		/// <param name="var">Type of data of the parameter.</param>
		/// <param name="directions">Direction of the parameter, Input, Output, Return, etc.</param>
		/// <author>GoodBytes</author>
		public void AddParameter(string text, object value, SqlVariables var, SqlDirections directions = SqlDirections.Input)
		{
			var parameter = new SqlParameter();
			parameter.ParameterName = "@" + text;
			if (var == SqlVariables.Guid)
				parameter.Value = new Guid(value.ToString());
			else
				parameter.Value = value ?? DBNull.Value;
			parameter.Direction = (ParameterDirection)directions;
			parameter.SqlDbType = (SqlDbType)var;

			if (parameters == null)
				parameters = new List<SqlParameter>();
			parameters.Add(parameter);
		}

		/// <summary>
		/// Execute the query.
		/// </summary>
		/// <param name="sp">String for the name of the store procedure or sql statement.</param>
		/// <param name="scalar">Gets only scalar value.</param>
		/// <author>GoodBytes</author>
		public void Execute(string sp, bool scalar = false)
		{
			try
			{
				//using (var scope = new System.Transactions.TransactionScope())
				//{
				using (SqlConnection _dataConnectionSql = new SqlConnection(_path))
				{
					using (SqlCommand _dataCommandSql = _dataConnectionSql.CreateCommand())
					{
						Connect(sp, _dataConnectionSql, _dataCommandSql);
						if (scalar)
							_scalarValue = _dataCommandSql.ExecuteScalar();
						else
							ExecuteAndGetReturnValues(_dataCommandSql);
						//if (_dataConnectionSql != null && _dataConnectionSql.State != ConnectionState.Closed)
						//	_dataConnectionSql.Close();
					}
					//}
				}
			}
			catch (SqlException sqlEx)
			{
				if (sqlEx.Number == 547)//Cant delete when exist relations with another tables, or trying to save data that does not exist in other tables.
					_returnValue = sqlEx.Number.ToString();
				else
					throw new Exception(ErrorsBuilder(sqlEx));
			}
			catch (Exception ex)
			{
				throw new Exception(ErrorsBuilder(ex));
			}
		}

		/// <summary>
		/// Execute the query with transaction.
		/// </summary>
		/// <param name="sp">String for the name of the store procedure or sql statement.</param>
		/// <author>GoodBytes</author>
		public void ExecuteWithTransaction(string sp)
		{
			try
			{
				SqlCommand _dataCommandSql = _dataConnectionSql.CreateCommand();
				Connect(sp, _dataConnectionSql, _dataCommandSql);
				//_dataCommandSql.CommandTimeout = 0; // Deadlock is occurring while editing, this will prevent deadlocks.
				_dataCommandSql.Transaction = _dataTransaction;
				ExecuteAndGetReturnValues(_dataCommandSql);
			}
			catch (SqlException sqlEx)
			{
				_dataTransaction.Rollback();
				if (sqlEx.Number == 547)//Cant delete when exist relations with another tables, or trying to save data that does not exist in other tables.
					_returnValue = sqlEx.Number.ToString();
				else
					throw new Exception(ErrorsBuilder(sqlEx));
			}
			catch (Exception ex)
			{
				_dataTransaction.Rollback();
				throw new Exception(ErrorsBuilder(ex));
			}
		}

		/// <summary>
		/// Read the data.
		/// </summary>
		/// <param name="sp">String for the name of the store procedure or sql statement.</param>
		/// <param name="columns">Columns that will keep the data in a dictionary before is moved to the proper model class.</param>
		/// <author>GoodBytes</author>
		public Dictionary<string, object> Read(string sp, Dictionary<string, object> columns)
		{
			try
			{
				using (SqlConnection _dataConnectionSql = new SqlConnection(_path))
				{
					using (SqlCommand _dataCommandSql = _dataConnectionSql.CreateCommand())
					{
						Connect(sp, _dataConnectionSql, _dataCommandSql);
						//ExecuteAndGetReturnValues(_dataCommandSql);
						using (SqlDataReader _dataReader = _dataCommandSql.ExecuteReader())
						{
							if (_dataReader.HasRows)
							{
								_dataReader.Read();
								foreach (string text in new List<string>(columns.Keys))
									columns[text] = (object)_dataReader[text];
							}
						}
						//_dataReader.Close();
						//if (_dataConnectionSql != null && _dataConnectionSql.State != ConnectionState.Closed)
						//	_dataConnectionSql.Close();
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception(ErrorsBuilder(ex));
			}
			return columns;
		}

		/// <summary>
		/// Read the list data.
		/// </summary>
		/// <param name="sp">String for the name of the store procedure or sql statement.</param>
		/// <param name="columns">Columns that will keep the data in a dictionary before is moved to the proper model class.</param>
		/// <author>GoodBytes</author>
		public List<Dictionary<string, object>> ReadList(string sp, Dictionary<string, object> columns)
		{
			List<Dictionary<string, object>> returnList = new List<Dictionary<string, object>>();
			try
			{
				using (SqlConnection _dataConnectionSql = new SqlConnection(_path))
				{
					using (SqlCommand _dataCommandSql = _dataConnectionSql.CreateCommand())
					{
						Connect(sp, _dataConnectionSql, _dataCommandSql);
						ExecuteAndGetReturnValues(_dataCommandSql);
						using (SqlDataReader _dataReader = _dataCommandSql.ExecuteReader())
						{
							if (_dataReader.HasRows)
								while (_dataReader.Read())
								{
									Dictionary<string, object> row = new Dictionary<string, object>();
									foreach (string text in new List<string>(columns.Keys))
										row[text] = (object)_dataReader[text];
									returnList.Add(row);
								}
						}
						//_dataReader.Close();
						//if (_dataConnectionSql != null && _dataConnectionSql.State != ConnectionState.Closed)
						//	_dataConnectionSql.Close();
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception(ErrorsBuilder(ex));
			}
			return returnList;
		}

		private void ExecuteAndGetReturnValues(SqlCommand _dataCommandSql)
		{
			SqlParameter returnValueParam = _dataCommandSql.Parameters.Add("@RETURN_VALUE", SqlDbType.NVarChar, 100);
			returnValueParam.Direction = ParameterDirection.ReturnValue;
			_rowsAffected = _dataCommandSql.ExecuteNonQuery();
			_returnValue = _dataCommandSql.Parameters["@RETURN_VALUE"].Value.ToString();
			if (!string.IsNullOrEmpty(IDReturnName))
			{
				ID = (long)_dataCommandSql.Parameters["@" + IDReturnName].Value;
				IDReturnName = string.Empty;
			}
		}

		private void Connect(string sp, SqlConnection _dataConnectionSql, SqlCommand _dataCommandSql)
		{
			_dataCommandSql.CommandText = sp;
			_dataCommandSql.CommandType = CommandType.StoredProcedure;
			if (_dataConnectionSql.State != ConnectionState.Open)
				_dataConnectionSql.Open();
			if (parameters == null) return;
			if (parameters.Count > 0)
				foreach (var param in parameters)
					_dataCommandSql.Parameters.Add(param);
			parameters.Clear();
		}
	}
}