
namespace IRSDKSharper
{
	/// <summary>
	/// Provides library-wide constants that mirror iRacing SDK limits and type sizes.
	/// </summary>
	public class IRacingSdkConst
	{
		public const int MaxNumCars = 72; // Use with caution - this can and will change in the future!
		public const int MaxNumDrivers = 256;
		public const int UnlimitedLaps = 32767;
		public const float UnlimitedTime = 604800.0f;
		public static readonly int[] VarTypeBytes = { 1, 1, 4, 4, 4, 8 };
	}
}
