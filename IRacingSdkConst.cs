
namespace IRSDKSharper
{
	/// <summary>
	/// The IRacingSdkConst class provides constant values commonly used throughout the iRacing SDK Sharper framework.
	/// </summary>
	/// <remarks>
	/// This class encapsulates fundamental values such as maximum counts for cars and drivers, unlimited laps and time values,
	/// and variable type sizes in bytes. These constants are integral to managing and interpreting simulation data within the iRacing platform.
	/// </remarks>
	/// <example>
	/// IRacingSdkConst is used in various modules and features, enabling consistent configuration and data processing.
	/// For example, MaxNumCars sets the upper limit of supported cars within simulations.
	/// </example>
	public class IRacingSdkConst
	{
		public const int MaxNumCars = 64;
		public const int MaxNumDrivers = 256;
		public const int UnlimitedLaps = 32767;
		public const float UnlimitedTime = 604800.0f;
		public static readonly int[] VarTypeBytes = { 1, 1, 4, 4, 4, 8 };
	}
}
