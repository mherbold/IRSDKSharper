
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace IRSDKSharper
{
	public partial class EventSystem
	{
		public Dictionary<string, EventTrack> Tracks { get; private set; } = new Dictionary<string, EventTrack>();
		public static HashSet<string> DisallowedTelemetryDataNames { get; private set; } = new HashSet<string>
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

		private readonly IRacingSdk irsdkSharper;

		private string directory = string.Empty;

		private int subSessionID = -1;

		private IRacingSdkDatum sessionNumDatum = new();
		private IRacingSdkDatum sessionTimeDatum = new();
		private IRacingSdkDatum sessionTickDatum = new();

		private int sessionNum = 0;
		private double sessionTime = 0;
		private int sessionTick = 0;

		private BinaryWriter binaryWriter = null;

		private readonly Dictionary<string, short> trackNameDictionary = new();
		private short lastUsedTrackNameId = 99;

		private bool frameHeaderRecorded = false;

		public EventSystem( IRacingSdk irsdkSharper )
		{
			this.irsdkSharper = irsdkSharper;
		}

		public void SetDirectory( string directory )
		{
			irsdkSharper.Log( $"EventSystem - SetDirectory( {directory} )" );

			this.directory = directory;

			if ( this.directory != string.Empty )
			{
				Directory.CreateDirectory( directory );
			}

			Reset();
		}

		public void Reset()
		{
			irsdkSharper.Log( "EventSystem - Reset()" );

			irsdkSharper.FireOnEventSystemDataReset();

			Tracks.Clear();

			subSessionID = -1;

			sessionNumDatum = new IRacingSdkDatum();
			sessionTimeDatum = new IRacingSdkDatum();
			sessionTickDatum = new IRacingSdkDatum();

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
					irsdkSharper.Log( "EventSystem - sessionInfo.WeekendInfo.SubSessionID changed" );

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
							irsdkSharper.Log( "EventSystem - opening telemetry file stream" );

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
			irsdkSharper.Log( "EventSystem - Initialize()" );

			sessionNumDatum = data.TelemetryDataProperties[ "SessionNum" ];
			sessionTimeDatum = data.TelemetryDataProperties[ "SessionTime" ];
			sessionTickDatum = data.TelemetryDataProperties[ "SessionTick" ];

			foreach ( var keyValuePair in data.TelemetryDataProperties )
			{
				if ( !DisallowedTelemetryDataNames.Contains( keyValuePair.Key ) )
				{
					for ( var index = 0; index < keyValuePair.Value.Count; index++ )
					{
						EventTrack eventTrack;

						switch ( keyValuePair.Value.VarType )
						{
							case IRacingSdkEnum.VarType.Char: eventTrack = new EventTrack<string>( keyValuePair.Value, index ); break;
							case IRacingSdkEnum.VarType.Bool: eventTrack = new EventTrack<bool>( keyValuePair.Value, index ); break;
							case IRacingSdkEnum.VarType.Int: eventTrack = new EventTrack<int>( keyValuePair.Value, index ); break;
							case IRacingSdkEnum.VarType.BitField: eventTrack = new EventTrack<uint>( keyValuePair.Value, index ); break;
							case IRacingSdkEnum.VarType.Float: eventTrack = new EventTrack<float>( keyValuePair.Value, index ); break;
							case IRacingSdkEnum.VarType.Double: eventTrack = new EventTrack<double>( keyValuePair.Value, index ); break;
							default: throw new Exception( $"Unexpected type ({keyValuePair.Value.VarType})!" );
						};

						Tracks.Add( eventTrack.ToString(), eventTrack );
					}
				}
			}

			InitializeCalculatedTracks( data );
		}

		private void LoadEvents( string filePath )
		{
			irsdkSharper.Log( $"EventSystem - LoadEvents( {filePath} )" );

			if ( !File.Exists( filePath ) )
			{
				irsdkSharper.Log( $"Warning - Event system file '{filePath}' does not exist." );

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
						var trackName = binaryReader.ReadString();

						trackNameIdDictionary[ trackNameId ] = trackName;

						if ( !Tracks.ContainsKey( trackName ) )
						{
							EventTrack eventTrack;

							switch ( varType )
							{
								case IRacingSdkEnum.VarType.Char: eventTrack = new EventTrack<string>( trackName, varType ); break;
								case IRacingSdkEnum.VarType.Bool: eventTrack = new EventTrack<bool>( trackName, varType ); break;
								case IRacingSdkEnum.VarType.Int: eventTrack = new EventTrack<int>( trackName, varType ); break;
								case IRacingSdkEnum.VarType.BitField: eventTrack = new EventTrack<uint>( trackName, varType ); break;
								case IRacingSdkEnum.VarType.Float: eventTrack = new EventTrack<float>( trackName, varType ); break;
								case IRacingSdkEnum.VarType.Double: eventTrack = new EventTrack<double>( trackName, varType ); break;
								default: throw new Exception( $"Unexpected type ({varType})!" );
							};

							Tracks.Add( trackName, eventTrack );
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

		private void RecordSessionInfoChanges( string trackName, object valueAsObject )
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

		private EventTrack CreateEventTrack( string trackName, object valueAsObject )
		{
			if ( !Tracks.ContainsKey( trackName ) )
			{
				EventTrack eventTrack;

				switch ( Type.GetTypeCode( valueAsObject.GetType() ) )
				{
					case TypeCode.String: eventTrack = new EventTrack<string>( trackName, valueAsObject ); break;
					case TypeCode.Int32: eventTrack = new EventTrack<int>( trackName, valueAsObject ); break;
					case TypeCode.Single: eventTrack = new EventTrack<float>( trackName, valueAsObject ); break;
					default: throw new Exception( $"Unexpected type ({valueAsObject?.GetType().Name})!" );
				};

				Tracks.Add( trackName, eventTrack );

				return eventTrack;
			}
			else
			{
				return Tracks[ trackName ];
			}
		}
	}
}
