using GoodBytes.DAL.Base.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace GoodBytes.DAL
{
	public enum SqlVariables
	{
		DateTime = SqlDbType.DateTime,
		Numeric = SqlDbType.BigInt,
		Boolean = SqlDbType.Bit,
		Decimal = SqlDbType.Decimal,
		String = SqlDbType.NVarChar,
		Float = SqlDbType.Float,
		Guid = SqlDbType.UniqueIdentifier
	}

	public enum SqlDirections
	{
		Input = ParameterDirection.Input,
		InputOutput = ParameterDirection.InputOutput,
		Output = ParameterDirection.Output,
		ReturnValue = ParameterDirection.ReturnValue,
	}

	public interface IDALInterface : IBaseInterface, IConnectionBaseInterface
	{
		//string Path { get; }

		object ScalarValue { get; }

		string ReturnValue { get; }

		long RowsAffected { get; }

		void AddParameter(string text, object value, SqlVariables var, SqlDirections direction = SqlDirections.Input);

		//object ReadParameter(string text);

		void Execute(string sp, bool scalar = false);

		void ExecuteWithTransaction(string sp);

		Dictionary<string, object> Read(string sp, Dictionary<string, object> columns);

		List<Dictionary<string, object>> ReadList(string sp, Dictionary<string, object> columns);

		//bool ReadStart();

		//bool Read();

		//bool ReadHaveData();

		//void ReadClose();

		//object Fill(string text);

		//bool Fill(ref Dictionary<string, object> columns);

		//object Fill(int column);

		//DALService Clone();
	}
}