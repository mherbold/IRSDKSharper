
using System.Collections;
using System.Diagnostics;

namespace HerboldRacing
{
	public partial class EventSystem
	{
		public Dictionary<string, EventTrack> Tracks { get; private set; } = new();
		public static HashSet<string> DisallowedTelemetryDataNames { get; private set; } = new()
			{
				"CamCameraNumber",
				"CamCameraState",
				"CamCarIdx",
				"CamGroupNumber",
				"CarIdxEstTime",
				"CarIdxLapDistPct",
				"CarIdxRPM",
				"CarIdxSteer",
				"Engine0_RPM",
				"IsDiskLoggingActive",
				"IsDiskLoggingEnabled",
				"IsGarageVisible",
				"IsReplayPlaying",
				"LapCurrentLapTime",
				"LapDist",
				"LapDistPct",
				"LatAccel",
				"LatAccel_ST",
				"LFbrakeLinePress",
				"LFshockDefl",
				"LFshockDefl_ST",
				"LFshockVel",
				"LFshockVel_ST",
				"LoadNumTextures",
				"LongAccel",
				"LongAccel_ST",
				"LRbrakeLinePress",
				"LRshockDefl",
				"LRshockDefl_ST",
				"LRshockVel",
				"LRshockVel_ST",
				"MemPageFaultSec",
				"MemSoftPageFaultSec",
				"OkToReloadTextures",
				"Pitch",
				"PitchRate",
				"PitchRate_ST",
				"ReplayFrameNum",
				"ReplayFrameNumEnd",
				"ReplayPlaySlowMotion",
				"ReplayPlaySpeed",
				"ReplaySessionNum",
				"ReplaySessionTime",
				"RFbrakeLinePress",
				"RFshockDefl",
				"RFshockDefl_ST",
				"RFshockVel",
				"RFshockVel_ST",
				"Roll",
				"RollRate",
				"RollRate_ST",
				"RPM",
				"RRbrakeLinePress",
				"RRshockDefl",
				"RRshockDefl_ST",
				"RRshockVel",
				"RRshockVel_ST",
				"SessionNum",
				"SessionTick",
				"SessionTime",
				"Speed",
				"SteeringWheelAngle",
				"SteeringWheelLimiter",
				"SteeringWheelMaxForceNm",
				"SteeringWheelPctDamper",
				"SteeringWheelPctIntensity",
				"SteeringWheelPctSmoothing",
				"SteeringWheelPctTorque",
				"SteeringWheelPctTorqueSign",
				"SteeringWheelPctTorqueSignStops",
				"SteeringWheelPeakForceNm",
				"SteeringWheelTorque",
				"SteeringWheelTorque_ST",
				"SteeringWheelUseLinear",
				"TireLF_RumblePitch",
				"TireLR_RumblePitch",
				"TireRF_RumblePitch",
				"TireRR_RumblePitch",
				"VelocityX",
				"VelocityX_ST",
				"VelocityY",
				"VelocityY_ST",
				"VelocityZ",
				"VelocityZ_ST",
				"VertAccel",
				"VertAccel_ST",
				"VidCapActive",
				"VidCapEnabled",
				"Yaw",
				"YawNorth",
				"YawRate",
				"YawRate_ST",
			};

		private readonly IRSDKSharper irsdkSharper;

		private string directory = string.Empty;

		private int subSessionID = -1;

		private IRacingSdkDatum sessionNumDatum = new();
		private IRacingSdkDatum sessionTimeDatum = new();
		private IRacingSdkDatum sessionTickDatum = new();

		private int sessionNum = 0;
		private double sessionTime = 0;
		private int sessionTick = 0;

		private BinaryWriter? binaryWriter = null;

		private readonly Dictionary<string, short> trackNameDictionary = new();
		private short lastUsedTrackNameId = 99;

		private bool frameHeaderRecorded = false;

		public EventSystem( IRSDKSharper irsdkSharper )
		{
			this.irsdkSharper = irsdkSharper;
		}

		public void SetDirectory( string directory )
		{
			Debug.WriteLine( $"EventSystem - SetDirectory( {directory} )" );

			this.directory = directory;

			if ( this.directory != string.Empty )
			{
				Directory.CreateDirectory( directory );
			}

			Reset();
		}

		public void Reset()
		{
			Debug.WriteLine( "EventSystem - Reset()" );

			irsdkSharper.FireOnEventSystemDataReset();

			Tracks.Clear();

			subSessionID = -1;

			sessionNumDatum = new();
			sessionTimeDatum = new();
			sessionTickDatum = new();

			sessionNum = 0;
			sessionTime = 0;
			sessionTick = 0;

			binaryWriter?.Close();
			binaryWriter = null;

			trackNameDictionary.Clear();
			lastUsedTrackNameId = 99;

			frameHeaderRecorded = false;

			ResetCalculatedTracks();
		}

		public void Update( IRacingSdkData data )
		{
			if ( data.SessionInfo is IRacingSdkSessionInfo sessionInfo )
			{
				if ( subSessionID != sessionInfo.WeekendInfo.SubSessionID )
				{
					Debug.WriteLine( "EventSystem - sessionInfo.WeekendInfo.SubSessionID changed" );

					Reset();

					subSessionID = sessionInfo.WeekendInfo.SubSessionID;

					if ( directory != string.Empty )
					{
						Initialize( data );

						var filePath = Path.Combine( directory, $"subses{subSessionID}.bin" );

						if ( sessionInfo.WeekendInfo.SimMode == "replay" )
						{
							LoadEvents( filePath );
						}
						else
						{
							Debug.WriteLine( "EventSystem - opening telemetry file stream" );

							var stream = File.Open( filePath, FileMode.Append );

							binaryWriter = new BinaryWriter( stream );
						}
					}
				}

				if ( binaryWriter != null )
				{
					frameHeaderRecorded = false;

					sessionNum = data.GetInt( sessionNumDatum );
					sessionTime = data.GetDouble( sessionTimeDatum );
					sessionTick = data.GetInt( sessionTickDatum );

					RecordTelemetryDataChanges( data );
					RecordCalculatedTracks( data );

					foreach ( var propertyInfo in sessionInfo.GetType().GetProperties() )
					{
						RecordSessionInfoChanges( propertyInfo.Name, propertyInfo.GetValue( sessionInfo ) );
					}
				}
			}
		}

		public short GetTrackNameId( string trackName, IRacingSdkEnum.VarType varType )
		{
			if ( trackNameDictionary.ContainsKey( trackName ) )
			{
				return trackNameDictionary[ trackName ];
			}

			var trackNameId = (short) ( lastUsedTrackNameId + 1 );

			trackNameDictionary.Add( trackName, trackNameId );

			lastUsedTrackNameId = trackNameId;

#pragma warning disable CS8602
			binaryWriter.Write( (short) 1 );
			binaryWriter.Write( trackNameId );
			binaryWriter.Write( (char) varType );
			binaryWriter.Write( trackName );
#pragma warning restore CS8602

			return trackNameId;
		}

		private void Initialize( IRacingSdkData data )
		{
			Debug.WriteLine( "EventSystem - Initialize()" );

			sessionNumDatum = data.TelemetryDataProperties[ "SessionNum" ];
			sessionTimeDatum = data.TelemetryDataProperties[ "SessionTime" ];
			sessionTickDatum = data.TelemetryDataProperties[ "SessionTick" ];

			foreach ( var keyValuePair in data.TelemetryDataProperties )
			{
				if ( !DisallowedTelemetryDataNames.Contains( keyValuePair.Key ) )
				{
					for ( var index = 0; index < keyValuePair.Value.Count; index++ )
					{
						EventTrack eventTrack = keyValuePair.Value.VarType switch
						{
							IRacingSdkEnum.VarType.Char => new EventTrack<string>( keyValuePair.Value, index ),
							IRacingSdkEnum.VarType.Bool => new EventTrack<bool>( keyValuePair.Value, index ),
							IRacingSdkEnum.VarType.Int => new EventTrack<int>( keyValuePair.Value, index ),
							IRacingSdkEnum.VarType.BitField => new EventTrack<uint>( keyValuePair.Value, index ),
							IRacingSdkEnum.VarType.Float => new EventTrack<float>( keyValuePair.Value, index ),
							IRacingSdkEnum.VarType.Double => new EventTrack<double>( keyValuePair.Value, index ),
							_ => throw new Exception( $"Unexpected type ({keyValuePair.Value.VarType})!" )
						};

						Tracks.Add( eventTrack.ToString(), eventTrack );
					}
				}
			}

			InitializeCalculatedTracks( data );
		}

		private void LoadEvents( string filePath )
		{
			Debug.WriteLine( $"EventSystem - LoadEvents( {filePath} )" );

			if ( !File.Exists( filePath ) )
			{
				Debug.WriteLine( $"Warning - Event system file '{filePath}' does not exist." );

				return;
			}

			sessionNum = -1;
			sessionTime = 0.0;

			var stream = File.Open( filePath, FileMode.Open );

			var binaryReader = new BinaryReader( stream );

			var trackNameIdDictionary = new Dictionary<short, string>();

			while ( true )
			{
				try
				{
					var trackNameId = binaryReader.ReadInt16();

					if ( trackNameId == 0 )
					{
						sessionNum = binaryReader.ReadInt32();
						sessionTime = binaryReader.ReadDouble();
					}
					else if ( trackNameId == 1 )
					{
						trackNameId = binaryReader.ReadInt16();
						var varType = (IRacingSdkEnum.VarType) binaryReader.ReadChar();
						string trackName = binaryReader.ReadString();

						trackNameIdDictionary[ trackNameId ] = trackName;

						if ( !Tracks.ContainsKey( trackName ) )
						{
							EventTrack track = varType switch
							{
								IRacingSdkEnum.VarType.Char => new EventTrack<string>( trackName, varType ),
								IRacingSdkEnum.VarType.Bool => new EventTrack<bool>( trackName, varType ),
								IRacingSdkEnum.VarType.Int => new EventTrack<int>( trackName, varType ),
								IRacingSdkEnum.VarType.BitField => new EventTrack<uint>( trackName, varType ),
								IRacingSdkEnum.VarType.Float => new EventTrack<float>( trackName, varType ),
								IRacingSdkEnum.VarType.Double => new EventTrack<double>( trackName, varType ),
								_ => throw new Exception( $"Unexpected type ({varType})!" )
							};

							Tracks.Add( trackName, track );
						}
					}
					else
					{
						var track = Tracks[ trackNameIdDictionary[ trackNameId ] ];

						track.Load( sessionNum, sessionTime, binaryReader );
					}
				}
				catch ( EndOfStreamException )
				{
					break;
				}
			}

			irsdkSharper.FireOnEventSystemDataLoaded();
		}

		private void RecordTelemetryDataChanges( IRacingSdkData data )
		{
			foreach ( var keyValuePair in Tracks )
			{
				keyValuePair.Value.Record( this, data );
			}
		}

		private void RecordSessionInfoChanges( string trackName, object? valueAsObject )
		{
			if ( valueAsObject != null )
			{
				var isSimpleValue = ( ( valueAsObject is string ) || ( valueAsObject is int ) || ( valueAsObject is float ) || ( valueAsObject is double ) );

				if ( isSimpleValue )
				{
					var track = CreateEventTrack( trackName, valueAsObject );

					track.Record( this, valueAsObject );
				}
				else if ( valueAsObject is IList list )
				{
					var index = 0;

					foreach ( var item in list )
					{
						RecordSessionInfoChanges( $"{trackName}[{index}]", item );

						index++;
					}
				}
				else
				{
					foreach ( var propertyInfo in valueAsObject.GetType().GetProperties() )
					{
						RecordSessionInfoChanges( $"{trackName}.{propertyInfo.Name}", propertyInfo.GetValue( valueAsObject ) );
					}
				}
			}
		}

		private void RecordFrameHeader()
		{
			if ( !frameHeaderRecorded )
			{
#pragma warning disable CS8602
				binaryWriter.Write( (short) 0 );
				binaryWriter.Write( sessionNum );
				binaryWriter.Write( sessionTime );
#pragma warning restore CS8602

				frameHeaderRecorded = true;
			}
		}

		private EventTrack CreateEventTrack( string trackName, object? valueAsObject )
		{
			if ( !Tracks.ContainsKey( trackName ) )
			{
				EventTrack track = valueAsObject switch
				{
					string => new EventTrack<string>( trackName, valueAsObject ),
					int => new EventTrack<int>( trackName, valueAsObject ),
					float => new EventTrack<float>( trackName, valueAsObject ),
					_ => throw new Exception( $"Unexpected type ({valueAsObject?.GetType().Name})!" )
				};

				Tracks.Add( trackName, track );

				return track;
			}
			else
			{
				return Tracks[ trackName ];
			}
		}
	}
}
