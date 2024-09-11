
using System.Globalization;
using System.Text.RegularExpressions;

namespace IRSDKSharper
{
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

		private void ResetCalculatedTracks()
		{
			irsdkSharper.Log( "EventSystem - ResetCalculatedTracks()" );

			carIdxLapDistPctDatum = new IRacingSdkDatum();

			trackLengthInMeters = 0.0f;

			lastSessionNum = -1;

			lastSessionTick[ 0 ] = -1;
			lastSessionTick[ 1 ] = -1;
		}

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
