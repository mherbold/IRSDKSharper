
namespace IRSDKSharper
{
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
