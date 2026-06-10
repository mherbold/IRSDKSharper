
namespace IRSDKSharper
{
	/// <summary>
	/// Describes a telemetry variable exposed by the iRacing shared memory header.
	/// </summary>
	public class IRacingSdkDatum
	{
		public const int MaxNameLength = 32;
		public const int MaxDescLength = 64;
		public const int MaxUnitLength = 32;

		public const int Size = sizeof( IRacingSdkEnum.VarType ) + sizeof( int ) * 2 + sizeof( bool ) + 3 /* padding */ + MaxNameLength + MaxDescLength + MaxUnitLength;

		public IRacingSdkEnum.VarType VarType { get; }
		public int Offset { get; }
		public int Count { get; }
		public bool CountAsTime { get; }
		public string Name { get; }
		public string Desc { get; }
		public string Unit { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="IRacingSdkDatum"/> class with placeholder values.
		/// </summary>
		public IRacingSdkDatum()
		{
			VarType = IRacingSdkEnum.VarType.Bool;
			Offset = 0;
			Count = 1;
			CountAsTime = false;
			Name = "Not initialized";
			Desc = "Not initialized";
			Unit = "Not initialized";
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IRacingSdkDatum"/> class.
		/// </summary>
		/// <param name="varType">The underlying telemetry value type.</param>
		/// <param name="offset">The byte offset of the value within a telemetry buffer.</param>
		/// <param name="count">The number of values available for this datum.</param>
		/// <param name="countAsTime">Indicates whether the count should be interpreted as a time value.</param>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="desc">The telemetry variable description.</param>
		/// <param name="unit">The telemetry variable unit or enum identifier.</param>
		public IRacingSdkDatum( IRacingSdkEnum.VarType varType, int offset, int count, bool countAsTime, string name, string desc, string unit )
		{
			VarType = varType;
			Offset = offset;
			Count = count;
			CountAsTime = countAsTime;
			Name = name;
			Desc = desc;
			Unit = unit;
		}
	}
}
