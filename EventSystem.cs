
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

		/// <summary>
		/// Sets the directory path for the EventSystem object and creates the directory if it doesn't already exist.
		/// Resets the EventSystem after setting the directory.
		/// </summary>
		/// <param name="directory">The directory path to set for the EventSystem.</param>
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

		/// <summary>
		/// Resets the internal state of the EventSystem to its default state.
		/// This includes clearing all event tracks, resetting session-related data,
		/// disposing of any active binary writer, and resetting calculated tracks.
		/// </summary>
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

		/// <summary>
		/// Updates the event system state with the given telemetry data from the iRacing SDK.
		/// </summary>
		/// <param name="data">The telemetry data provided by the iRacing SDK, containing session and telemetry information to be processed.</param>
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

		/// Retrieves the unique track name ID associated with the given track name and variable type.
		/// If the track name does not already exist in the dictionary, a new ID is generated, mapped,
		/// and recorded in the binary writer.
		/// <param name="trackName">The name of the track for which the ID is requested.</param>
		/// <param name="varType">The data type of the variable associated with the track.</param>
		/// <returns>A short value representing the unique ID of the specified track name.</returns>
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

		/// Initializes the telemetry data tracks and prepares the event system for processing data.
		/// <param name="data">
		/// An instance of IRacingSdkData containing the telemetry data properties to initialize the tracks.
		/// </param>
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

		/// <summary>
		/// Loads event data from a specified binary file and updates the internal event system accordingly.
		/// </summary>
		/// <param name="filePath">The path of the binary file containing the event data to read and process.</param>
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

		/// Records telemetry data changes by delegating the processing of data to individual tracks.
		/// <param name="data">The telemetry data received and processed from the iRacing SDK.</param>
		private void RecordTelemetryDataChanges( IRacingSdkData data )
		{
			foreach ( var keyValuePair in Tracks )
			{
				keyValuePair.Value.Record( this, data );
			}
		}

		/// <summary>
		/// Records changes in session information by creating or updating event tracks based on the provided session information data.
		/// This method works recursively to capture property changes in complex objects or collections.
		/// </summary>
		/// <param name="trackName">The name of the track or property being recorded.</param>
		/// <param name="valueAsObject">The current value of the session information property to be evaluated and recorded.</param>
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

		/// <summary>
		/// Ensures that the frame header is recorded once per session frame.
		/// Writes the session number and session time to the binary writer if the header
		/// has not already been recorded.
		/// </summary>
		/// <remarks>
		/// This method uses a flag to avoid duplicating the frame header for multiple recordings.
		/// The values written include a short value of 0 for framing purposes, the session number,
		/// and the session time. Uses a binary writer to handle the writing operations.
		/// </remarks>
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

		/// <summary>
		/// Creates or retrieves an existing event track associated with the provided track name and initializes it with the given value object.
		/// </summary>
		/// <param name="trackName">The name of the track to create or retrieve.</param>
		/// <param name="valueAsObject">The initial value to determine the type of the event track and initialize it.</param>
		/// <returns>An instance of <see cref="EventTrack"/> corresponding to the provided track name.</returns>
		/// <exception cref="Exception">Thrown if the type of <paramref name="valueAsObject"/> is unsupported.</exception>
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
