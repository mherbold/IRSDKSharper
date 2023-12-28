
using System.Diagnostics;
using System.Text;

namespace HerboldRacing
{
	public class EventSystem
	{
		private const int MaxNumCars = 64;
		private const int BufferSize = 8 * 1024;

		private readonly string directory;

		private int subSessionID = -1;
		private StreamWriter? streamWriter = null;
		private double sessionTime = 0;
		private readonly EventTracks eventTracks = new();
		private readonly StringBuilder stringBuilder = new( BufferSize );

		public EventSystem( string directory )
		{
			this.directory = directory;
		}

		public void Update( IRacingSdkData data )
		{
			if ( subSessionID != data.SessionInfo.WeekendInfo.SubSessionID )
			{
				Reset();

				subSessionID = data.SessionInfo.WeekendInfo.SubSessionID;

				Directory.CreateDirectory( directory );

				var filePath = Path.Combine( directory, $"subses{subSessionID}.yaml" );

				if ( data.SessionInfo.WeekendInfo.SimMode == "replay" )
				{
					LoadEvents( filePath );
				}
				else
				{
					streamWriter = new StreamWriter( filePath, true, Encoding.UTF8, BufferSize );
				}
			}
		}

		public void Reset()
		{
			subSessionID = -1;
			streamWriter?.Close();
			streamWriter = null;
			sessionTime = 0;
			eventTracks.Reset();
		}

		public void Record( IRacingSdkData data )
		{
			if ( streamWriter != null )
			{
				var sessionNum = data.GetInt( "SessionNum" );
				var sessionTime = data.GetDouble( "SessionTime" );
				var sessionTick = data.GetInt( "SessionTick" );

				eventTracks.Update( sessionNum, sessionTime, sessionTick, stringBuilder, data );

				if ( stringBuilder.Length > 0 )
				{
					streamWriter.WriteLine( "---" );
					streamWriter.WriteLine( $" SessionNum: {sessionNum}" );
					streamWriter.WriteLine( $" SessionTime: {sessionTime:0.0000}" );
					streamWriter.Write( stringBuilder );
					streamWriter.WriteLine( "..." );

					stringBuilder.Clear();
				}

				if ( ( sessionTime < this.sessionTime ) || ( ( this.sessionTime + 5 ) <= sessionTime ) )
				{
					this.sessionTime = sessionTime;

					streamWriter.Flush();
				}
			}
		}

		private void LoadEvents( string filePath )
		{
			if ( !File.Exists( filePath ) )
			{
				Debug.WriteLine( $"Warning - Event system file '{filePath}' does not exist." );

				return;
			}

			// TODO
		}

		public class EventTracks
		{
			public EventTrack<uint> sessionFlagsEventTrack = new( "SessionFlags", 0 );
			public EventTrack<int> paceModeEventTrack = new( "PaceMode", (int) IRacingSdkEnum.PaceMode.SingleFileStart );
			public EventTrack<int> carLeftRightTrack = new( "CarLeftRight", (int) IRacingSdkEnum.CarLeftRight.Off );
			public EventTrack<float> fuelLevelTrack = new( "FuelLevel", 0, 120 );

			public EventTrack<uint>[] carIdxSessionFlagsEventTracks = new EventTrack<uint>[ MaxNumCars ];
			public EventTrack<int>[] carIdxPositionEventTracks = new EventTrack<int>[ MaxNumCars ];
			public EventTrack<int>[] carIdxClassPositionEventTracks = new EventTrack<int>[ MaxNumCars ];
			public EventTrack<int>[] carIdxPaceLineEventTracks = new EventTrack<int>[ MaxNumCars ];
			public EventTrack<int>[] carIdxPaceRowEventTracks = new EventTrack<int>[ MaxNumCars ];
			public EventTrack<uint>[] carIdxPaceFlagsEventTracks = new EventTrack<uint>[ MaxNumCars ];

			public EventTrack<int>[] curDriverIncidentCountEventTracks = new EventTrack<int>[ MaxNumCars ];

			public EventTrack<int>[] qualifyPositionEventTracks = new EventTrack<int>[ MaxNumCars ];
			public EventTrack<int>[] qualifyClassPositionEventTracks = new EventTrack<int>[ MaxNumCars ];
			public EventTrack<int>[] qualifyFastestLapEventTracks = new EventTrack<int>[ MaxNumCars ];
			public EventTrack<float>[] qualifyFastestTimeEventTracks = new EventTrack<float>[ MaxNumCars ];


			public EventTracks()
			{
				for ( var i = 0; i < MaxNumCars; i++ )
				{
					carIdxSessionFlagsEventTracks[ i ] = new( $"CarIdxSessionFlag.{i}", 0 );
					carIdxPositionEventTracks[ i ] = new( $"CarIdxPosition.{i}", 0 );
					carIdxClassPositionEventTracks[ i ] = new( $"CarIdxClassPosition.{i}", 0 );
					carIdxPaceLineEventTracks[ i ] = new( $"CarIdxPaceLine.{i}", -1 );
					carIdxPaceRowEventTracks[ i ] = new( $"CarIdxPaceRow.{i}", -1 );
					carIdxPaceFlagsEventTracks[ i ] = new( $"CarIdxPaceFlags.{i}", 0 );

					curDriverIncidentCountEventTracks[ i ] = new( $"CurDriverIncidentCount.{i}", -1 );

					qualifyPositionEventTracks[ i ] = new( $"QualifyPosition.{i}", -1 );
					qualifyClassPositionEventTracks[ i ] = new( $"QualifyClassPosition.{i}", -1 );
					qualifyFastestLapEventTracks[ i ] = new( $"QualifyFastestLap.{i}", -1 );
					qualifyFastestTimeEventTracks[ i ] = new( $"QualifyFastestTime.{i}", 0.0f );
				}
			}

			public void Reset()
			{
				sessionFlagsEventTrack.Reset();
				paceModeEventTrack.Reset();
				carLeftRightTrack.Reset();
				fuelLevelTrack.Reset();

				for ( var i = 0; i < MaxNumCars; i++ )
				{
					carIdxSessionFlagsEventTracks[ i ].Reset();
					carIdxPositionEventTracks[ i ].Reset();
					carIdxClassPositionEventTracks[ i ].Reset();
					carIdxPaceLineEventTracks[ i ].Reset();
					carIdxPaceRowEventTracks[ i ].Reset();
					carIdxPaceFlagsEventTracks[ i ].Reset();

					curDriverIncidentCountEventTracks[ i ].Reset();

					qualifyPositionEventTracks[ i ].Reset();
					qualifyClassPositionEventTracks[ i ].Reset();
					qualifyFastestLapEventTracks[ i ].Reset();
					qualifyFastestTimeEventTracks[ i ].Reset();
				}
			}

			public void Update( int sessionNum, double sessionTime, int sessionTick, StringBuilder stringBuilder, IRacingSdkData data )
			{
				var sessionFlags = data.GetBitField( "SessionFlags" );
				var paceMode = data.GetInt( "PaceMode" );
				var carLeftRight = data.GetInt( "CarLeftRight" );
				var fuelLevel = data.GetFloat( "FuelLevel" );

				sessionFlagsEventTrack.Update( sessionNum, sessionTime, sessionTick, stringBuilder, sessionFlags );
				paceModeEventTrack.Update( sessionNum, sessionTime, sessionTick, stringBuilder, paceMode );
				carLeftRightTrack.Update( sessionNum, sessionTime, sessionTick, stringBuilder, carLeftRight );
				fuelLevelTrack.Update( sessionNum, sessionTime, sessionTick, stringBuilder, fuelLevel );

				uint[] carIdxSessionFlags = new uint[ MaxNumCars ];
				int[] carIdxPaceLine = new int[ MaxNumCars ];
				int[] carIdxPaceRow = new int[ MaxNumCars ];
				uint[] carIdxPaceFlags = new uint[ MaxNumCars ];

				data.GetBitFieldArray( "CarIdxSessionFlags", carIdxSessionFlags, 0, MaxNumCars );
				data.GetIntArray( "CarIdxPaceLine", carIdxPaceLine, 0, MaxNumCars );
				data.GetIntArray( "CarIdxPaceRow", carIdxPaceRow, 0, MaxNumCars );
				data.GetBitFieldArray( "CarIdxPaceFlags", carIdxPaceFlags, 0, MaxNumCars );

				for ( var i = 0; i < MaxNumCars; i++ )
				{
					carIdxSessionFlagsEventTracks[ i ].Update( sessionNum, sessionTime, sessionTick, stringBuilder, carIdxSessionFlags[ i ] );
					carIdxPaceLineEventTracks[ i ].Update( sessionNum, sessionTime, sessionTick, stringBuilder, carIdxPaceLine[ i ] );
					carIdxPaceRowEventTracks[ i ].Update( sessionNum, sessionTime, sessionTick, stringBuilder, carIdxPaceRow[ i ] );
					carIdxPaceFlagsEventTracks[ i ].Update( sessionNum, sessionTime, sessionTick, stringBuilder, carIdxPaceFlags[ i ] );
				}

				var sessionInfo = data.SessionInfo;

				foreach ( var driver in sessionInfo.DriverInfo.Drivers )
				{
					var carIdx = driver.CarIdx;

					if ( carIdx != -1 )
					{
						curDriverIncidentCountEventTracks[ carIdx ].Update( sessionNum, sessionTime, sessionTick, stringBuilder, driver.CurDriverIncidentCount );
					}
				}

				var qualifyPositions = sessionInfo.SessionInfo.Sessions[ sessionNum ].QualifyPositions;

				if ( qualifyPositions != null )
				{
					foreach ( var qualifyPosition in qualifyPositions )
					{
						var carIdx = qualifyPosition.CarIdx;

						qualifyPositionEventTracks[ carIdx ].Update( sessionNum, sessionTime, sessionTick, stringBuilder, qualifyPosition.Position );
						qualifyClassPositionEventTracks[ carIdx ].Update( sessionNum, sessionTime, sessionTick, stringBuilder, qualifyPosition.ClassPosition );
						qualifyFastestLapEventTracks[ carIdx ].Update( sessionNum, sessionTime, sessionTick, stringBuilder, qualifyPosition.FastestLap );
						qualifyFastestTimeEventTracks[ carIdx ].Update( sessionNum, sessionTime, sessionTick, stringBuilder, qualifyPosition.FastestTime );
					}
				}
			}

			public class EventTrack<T> where T : IEquatable<T>
			{
				string trackName;

				private T defaultValue;
				private T retainedValue;
				private T currentValue;

				private int sessionTickMask;

				private List<Event> events = new();

				public EventTrack( string trackName, T defaultValue, int sessionTickMask = 1 )
				{
					this.trackName = trackName;
					this.defaultValue = defaultValue;
					this.sessionTickMask = sessionTickMask;

					retainedValue = defaultValue;
					currentValue = defaultValue;
				}

				public void Reset()
				{
					retainedValue = defaultValue;
					currentValue = defaultValue;

					events.Clear();
				}

				public void Update( int sessionNum, double sessionTime, int sessionTick, StringBuilder stringBuilder, T value )
				{
					if ( ( sessionTick % sessionTickMask ) == 0 )
					{
						if ( !retainedValue.Equals( value ) )
						{
							stringBuilder.AppendLine( $" {trackName}: {value}" );

							events.Add( new Event( sessionNum, sessionTime, value ) );

							retainedValue = value;
						}
					}
				}

				public class Event
				{
					public readonly int sessionNum;
					public readonly double sessionTime;
					public readonly T value;

					public Event( int sessionNum, double sessionTime, T value )
					{
						this.sessionNum = sessionNum;
						this.sessionTime = sessionTime;
						this.value = value;
					}
				}
			}
		}
	}
}
