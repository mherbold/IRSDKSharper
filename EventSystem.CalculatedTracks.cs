
using System.Globalization;
using System.Text.RegularExpressions;

namespace IRSDKSharper
{
	/// <summary>
	/// Provides functionality to manage and record events for telemetry and data tracking in the IRacing SDK system.
	/// </summary>
	/// <remarks>
	/// The EventSystem class is a central component of the IRSDKSharper namespace used for tracking changes in telemetry data.
	/// It provides mechanisms to record events and associate them with specific data streams.
	/// This class is implemented as partial and includes additional nested and derived types for extensibility.
	/// </remarks>
	public partial class EventSystem
	{
		public float MinimumGForce { get; set; } = 2.0f; // in g's

		private IRacingSdkDatum carIdxLapDistPctDatum = new();

		private float trackLengthInMeters = 0.0f;

		private int lastSessionNum = -1;

		private const float OneG = 9.80665f; // in meters per second squared

		private readonly int[] lastSessionTick = new int[ 2 ];

		private readonly float[][] lastCarIdxLapDistPct = new float[ IRacingSdkConst.MaxNumCars ][];

		private readonly EventTrack[] carIdxGForceEventTrack = new EventTrack[ IRacingSdkConst.MaxNumCars ];

		/// <summary>
		/// Resets all calculated track-related data within the EventSystem.
		/// This includes reinitializing the internal data structures and variables
		/// responsible for maintaining track information, session details, and
		/// lap distance data. Designed to be used for resetting the state
		/// of track-related computations in preparation for a new session or race.
		/// </summary>
		private void ResetCalculatedTracks()
		{
			irsdkSharper.Log( "EventSystem - ResetCalculatedTracks()" );

			carIdxLapDistPctDatum = new IRacingSdkDatum();

			trackLengthInMeters = 0.0f;

			lastSessionNum = -1;

			lastSessionTick[ 0 ] = -1;
			lastSessionTick[ 1 ] = -1;
		}

		/// <summary>
		/// Initializes the calculated tracks by processing session information and telemetry data.
		/// </summary>
		/// <param name="data">An instance of IRacingSdkData containing telemetry and session information used to initialize calculated tracks.</param>
		private void InitializeCalculatedTracks( IRacingSdkData data )
		{
			irsdkSharper.Log( "EventSystem - InitializeCalculatedTracks()" );

			if ( data.SessionInfo is IRacingSdkSessionInfo sessionInfo )
			{
				carIdxLapDistPctDatum = data.TelemetryDataProperties[ "CarIdxLapDistPct" ];

				var match = Regex.Match( sessionInfo.WeekendInfo.TrackLength, @"([-+]?[0-9]*\.?[0-9]+)" );

				if ( match.Success )
				{
					trackLengthInMeters = 1000.0f * float.Parse( match.Groups[ 1 ].Value, CultureInfo.InvariantCulture.NumberFormat );
				}

				for ( var index = 0; index < IRacingSdkConst.MaxNumCars; index++ )
				{
					lastCarIdxLapDistPct[ index ] = new float[ 2 ];

					var trackName = $"CarIdxGForce[{index}]";

					var eventTrack = new EventTrack<float>( trackName, IRacingSdkEnum.VarType.Float );

					Tracks.Add( eventTrack.ToString(), eventTrack );

					carIdxGForceEventTrack[ index ] = eventTrack;
				}
			}
		}

		/// <summary>
		/// Records the calculated tracks and updates telemetry data for cars during a session.
		/// </summary>
		/// <param name="data">The telemetry data object containing current session and track data.</param>
		private void RecordCalculatedTracks( IRacingSdkData data )
		{
			if ( sessionNum != lastSessionNum )
			{
				for ( var index = 0; index < IRacingSdkConst.MaxNumCars; index++ )
				{
					lastCarIdxLapDistPct[ index ][ 0 ] = -1.0f;
					lastCarIdxLapDistPct[ index ][ 1 ] = -1.0f;
				}

				lastSessionNum = sessionNum;
			}

			var carIdxLapDistPctList = new float[ IRacingSdkConst.MaxNumCars ];

			data.GetFloatArray( carIdxLapDistPctDatum, carIdxLapDistPctList, 0, carIdxLapDistPctList.Length );

			for ( var index = 0; index < IRacingSdkConst.MaxNumCars; index++ )
			{
				// g force

				var carIdxLapDistPct = carIdxLapDistPctList[ index ];

				if ( ( lastCarIdxLapDistPct[ index ][ 0 ] != -1.0f ) && ( lastCarIdxLapDistPct[ index ][ 1 ] != -1.0f ) && ( carIdxLapDistPct != -1.0f ) )
				{
					var sessionTick01 = lastSessionTick[ 1 ] - lastSessionTick[ 0 ];
					var sessionTick12 = sessionTick - lastSessionTick[ 1 ];
					var sessionTick02 = sessionTick - lastSessionTick[ 0 ];

					if ( ( sessionTick01 > 0 ) && ( sessionTick12 > 0 ) )
					{
						var pct01 = lastCarIdxLapDistPct[ index ][ 1 ] - lastCarIdxLapDistPct[ index ][ 0 ];

						if ( pct01 <= -0.5f )
						{
							pct01 += 1.0f;
						}
						else if ( pct01 >= 0.05f )
						{
							pct01 -= 1.0f;
						}

						var pct12 = carIdxLapDistPct - lastCarIdxLapDistPct[ index ][ 1 ];

						if ( pct12 <= -0.5f )
						{
							pct12 += 1.0f;
						}
						else if ( pct12 >= 0.05f )
						{
							pct12 -= 1.0f;
						}

						var tickRate = (float) data.TickRate;

						var deltaTime01 = sessionTick01 / tickRate;
						var deltaTime12 = sessionTick12 / tickRate;
						var deltaTime02 = sessionTick02 / tickRate;

						var velocity01 = ( pct01 * trackLengthInMeters ) / deltaTime01;
						var velocity12 = ( pct12 * trackLengthInMeters ) / deltaTime12;

						var gForce = ( velocity12 - velocity01 ) / deltaTime02 / OneG;

						if ( ( gForce < MinimumGForce ) && ( gForce > -MinimumGForce ) )
						{
							gForce = 0.0f;
						}

						carIdxGForceEventTrack[ index ].Record( this, gForce );
					}
				}

				//

				lastCarIdxLapDistPct[ index ][ 0 ] = lastCarIdxLapDistPct[ index ][ 1 ];
				lastCarIdxLapDistPct[ index ][ 1 ] = carIdxLapDistPct;
			}

			lastSessionTick[ 0 ] = lastSessionTick[ 1 ];
			lastSessionTick[ 1 ] = sessionTick;
		}
	}
}
