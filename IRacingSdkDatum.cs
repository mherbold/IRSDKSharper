
namespace HerboldRacing
{
	public class IRacingSdkDatum
	{
		public const int Size = 144;

		public const int MaxNameLength = 32;
		public const int MaxDescLength = 64;
		public const int MaxUnitLength = 32;

		public IRacingSdkEnum.VarType VarType { get; }
		public int Offset { get; }
		public int Count { get; }
		public int Bytes { get; }
		public string Name { get; }
		public string Desc { get; }
		public string Unit { get; }

		public IRacingSdkDatum( IRacingSdkEnum.VarType varType, int offset, int count, int bytes, string name, string desc, string unit )
		{
			VarType = varType;
			Offset = offset;
			Count = count;
			Bytes = bytes;
			Name = name;
			Desc = desc;
			Unit = unit;
		}
	}
}
