
using System.Collections;
using System.Diagnostics;
using System.Text;

namespace HerboldRacing
{
	internal class ImprovedReplay
	{
		private const int BufferSize = 1 * 1024 * 1024;

		private readonly string dataFilesPath;

		private int subSessionID = -1;

		private StreamWriter? sessionInfoStreamWriter = null;
		private StreamWriter? telemetryDataStreamWriter = null;
		private IRacingSdkSessionInfo retainedSessionInfo = new();

		private readonly List<RetainedTelemetryDatum> retainedTelemetryData = new();

		private static readonly Dictionary<string, int> throttledTelemetry = new()
		{
			{ "AirDensity", 15 },
			{ "AirPressure", 15 },
			{ "AirTemp", 15 },
			{ "CarIdxRPM", 1 },
			{ "FogLevel", 15 },
			{ "FuelLevel", 1 },
			{ "FuelLevelPct", 1 },
			{ "FuelPress", 1 },
			{ "FuelUsePerHour", 1 },
			{ "ManifoldPress", 1 },
			{ "OilPress", 1 },
			{ "OilTemp", 15 },
			{ "OilLevel", 1 },
			{ "PitOptRepairLeft", 1 },
			{ "PitRepairLeft", 1 },
			{ "RelativeHumidity", 15 },
			{ "SolarAltitude", 15 },
			{ "SolarAzimuth", 15 },
			{ "TrackTempCrew", 1 },
			{ "Voltage", 1 },
			{ "WaterTemp", 15 },
			{ "WaterLevel", 1 },
			{ "WindDir", 15 },
			{ "WindVel", 15 },
		};

		#region notes

		// Stuff not replayed so we need them in our telemetry recording -
		//
		// CarIdxPaceFlags
		// CarIdxPaceLine
		// CarIdxPaceRow
		// CarIdxSessionFlags
		// CarLeftRight
		// FastRepairAvailable
		// FastRepairUsed
		// FrontTireSetsAvailable
		// FrontTireSetsUsed
		// LeftTireSetsAvailable
		// LeftTireSetsUsed
		// LFTiresAvailable
		// LFTiresUsed
		// LRTiresAvailable
		// LRTiresUsed
		// PaceMode
		// PitsOpen
		// PitstopActive
		// PlayerCarMyIncidentCount
		// PlayerCarTeamIncidentCount
		// RadioTransmitFrequencyIdx
		// RadioTransmitRadioIdx
		// RearTireSetsAvailable
		// RearTireSetsUsed
		// RFTiresAvailable
		// RFTiresUsed
		// RightTireSetsAvailable
		// RightTireSetsUsed
		// RRTiresAvailable
		// RRTiresUsed
		// SessionFlags
		// TireSetsAvailable
		// TireSetsUsed
		//
		// Stuff that I am not sure about so leaving them in our telemetry recording -
		//
		// BrakeAbsActive
		// CarIdxFastRepairsUsed
		// CarIdxP2P_Count
		// CarIdxP2P_Status
		// dcBrakeBias
		// dcDashPage
		// DCDriversSoFar
		// DCLapStatus
		// dcStarter
		// dpFastRepair
		// dpFuelAddKg
		// dpFuelFill
		// dpLFTireColdPress
		// dpLRTireColdPress
		// dpLTireChange
		// dpRFTireColdPress
		// dpRRTireColdPress
		// dpRTireChange
		// dpWeightJackerLeft
		// dpWeightJackerRight
		// dpWindshieldTearoff
		// DriverMarker
		// EngineWarnings
		// LFcoldPressure
		// LFtempCL
		// LFtempCM
		// LFtempCR
		// LFwearL
		// LFwearM
		// LFwearR
		// LRcoldPressure
		// LRtempCL
		// LRtempCM
		// LRtempCR
		// LRwearL
		// LRwearM
		// LRwearR
		// ManualBoost
		// ManualNoBoost
		// PitSvFlags
		// PitSvFuel
		// PitSvLFP
		// PitSvLRP
		// PitSvRFP
		// PitSvRRP
		// PitSvTireCompound
		// PlayerCarDryTireSetLimit
		// PlayerCarInPitStall
		// PlayerCarPitSvStatus
		// PlayerCarPowerAdjust
		// PlayerCarTowTime
		// PlayerCarWeightPenalty
		// PushToPass
		// RFcoldPressure
		// RFtempCL
		// RFtempCM
		// RFtempCR
		// RFwearL
		// RFwearM
		// RFwearR
		// RRcoldPressure
		// RRtempCL
		// RRtempCM
		// RRtempCR
		// RRwearL
		// RRwearM
		// RRwearR
		// SessionJokerLapsRemain
		// SessionOnJokerLap
		// Skies
		// WeatherType

		#endregion

		private static readonly HashSet<string> ignoredTelemetry = new() {
			"Brake",							// replayed
			"BrakeRaw",							// not replayed but chatty
			"CamCameraNumber",					// live
			"CamCameraState",					// live
			"CamCarIdx",						// live
			"CamGroupNumber",					// live
			"CarIdxBestLapNum",					// replayed
			"CarIdxBestLapTime",				// replayed
			"CarIdxClass",						// replayed
			"CarIdxClassPosition",				// replayed
			"CarIdxEstTime",					// replayed
			"CarIdxF2Time",						// replayed
			"CarIdxGear",						// replayed
			"CarIdxLap",						// replayed
			"CarIdxLapCompleted",				// replayed
			"CarIdxLapDistPct",					// replayed
			"CarIdxLastLapTime",				// replayed
			"CarIdxOnPitRoad",					// replayed
			"CarIdxPosition",					// replayed
			"CarIdxQualTireCompound",			// not replayed but dont need this
			"CarIdxQualTireCompoundLocked",		// not replayed but dont need this
			"CarIdxRPM",						// replayed only for player car but is chatty
			"CarIdxSteer",						// replayed
			"CarIdxTireCompound",				// not replayed but dont need this
			"CarIdxTrackSurface",				// replayed
			"CarIdxTrackSurfaceMaterial",		// replayed
			"ChanAvgLatency",					// not replayed but dont need this
			"ChanClockSkew",					// not replayed but dont need this
			"ChanLatency",						// not replayed but dont need this
			"ChanPartnerQuality",				// not replayed but dont need this
			"ChanQuality",						// not replayed but dont need this
			"Clutch",							// replayed
			"ClutchRaw",						// not replayed but chatty
			"CpuUsageBG",						// live
			"CpuUsageBG",						// live
			"CpuUsageFG",						// live
			"CRshockDefl",						// not replayed but chatty
			"CRshockDefl_ST",					// not replayed but chatty
			"CRshockVel",						// not replayed but chatty
			"CRshockVel_ST",					// not replayed but chatty
			"DisplayUnits",						// live
			"Engine0_RPM",						// is not replayed but whats the difference with RPM?
			"EnterExitReset",					// live
			"FrameRate",						// live
			"Gear",								// duplicate of CarIdxGear
			"GpuUsage",							// live
			"HandbrakeRaw",						// not replayed but chatty
			"IsDiskLoggingActive",				// live
			"IsDiskLoggingEnabled",				// live
			"IsGarageVisible",					// live
			"IsInGarage",						// live
			"IsOnTrack",						// live
			"IsOnTrackCar",						// live
			"IsReplayPlaying",					// live
			"Lap",								// duplicate of CarIdxLap
			"LapBestLap",						// not replayed but dont need this
			"LapBestLapTime",					// not replayed but dont need this
			"LapBestNLapLap",					// not replayed but dont need this
			"LapBestNLapTime",					// not replayed but dont need this
			"LapCompleted",						// duplicate of CarIdxLapCompleted
			"LapCurrentLapTime",				// not replayed but dont need this
			"LapDeltaToBestLap",				// not replayed but dont need this
			"LapDeltaToBestLap_DD",				// not replayed but dont need this
			"LapDeltaToBestLap_OK",				// not replayed but dont need this
			"LapDeltaToOptimalLap",				// not replayed but dont need this
			"LapDeltaToOptimalLap_DD",			// not replayed but dont need this
			"LapDeltaToOptimalLap_OK",			// not replayed but dont need this
			"LapDeltaToSessionBestLap",			// not replayed but dont need this
			"LapDeltaToSessionBestLap_DD",		// not replayed but dont need this
			"LapDeltaToSessionBestLap_OK",		// not replayed but dont need this
			"LapDeltaToSessionLastlLap",		// not replayed but dont need this
			"LapDeltaToSessionLastlLap_DD",		// not replayed but dont need this
			"LapDeltaToSessionLastlLap_OK",		// not replayed but dont need this
			"LapDeltaToSessionOptimalLap",		// not replayed but dont need this
			"LapDeltaToSessionOptimalLap_DD",	// not replayed but dont need this
			"LapDeltaToSessionOptimalLap_OK",	// not replayed but dont need this
			"LapDist",							// replayed
			"LapDistPct",						// duplicate of CarIdxLapDistPct
			"LapLasNLapSeq",					// not replayed but dont need this
			"LapLastLapTime",					// duplicate of CarIdxLastLapTime
			"LapLastNLapTime",					// not replayed but dont need this
			"LatAccel",							// replayed
			"LatAccel_ST",						// replayed
			"LFbrakeLinePress",					// unknown if replayed but dont need this
			"LFshockDefl",						// not replayed but chatty
			"LFshockDefl_ST",					// not replayed but chatty
			"LFshockVel",						// not replayed but chatty
			"LFshockVel_ST",					// not replayed but chatty
			"LoadNumTextures",					// live
			"LongAccel",						// replayed
			"LongAccel_ST",						// replayed
			"LRbrakeLinePress",					// unknown if replayed but dont need this
			"LRshockDefl",						// not replayed but chatty
			"LRshockDefl_ST",					// not replayed but chatty
			"LRshockVel",						// not replayed but chatty
			"LRshockVel_ST",					// not replayed but chatty
			"MemPageFaultSec",					// live
			"MemSoftPageFaultSec",				// live
			"OkToReloadTextures",				// live
			"OnPitRoad",						// duplicate of CarIdxOnPitRoad
			"Pitch",							// replayed
			"PitchRate",						// replayed
			"PitchRate_ST",						// replayed
			"PlayerCarClass",					// duplicate of CarIdxClass
			"PlayerCarClassPosition",			// duplicate of CarIdxClassPosition
			"PlayerCarIdx",						// replayed
			"PlayerCarPosition",				// duplicate of CarIdxPosition
			"PlayerFastRepairsUsed",			// duplicate of CarIdxFastRepairsUsed
			"PlayerTireCompound",				// duplicate of CarIdxTireCompound
			"PlayerTrackSurface",				// duplicate of CarIdxTrackSurface
			"PlayerTrackSurfaceMaterial",		// duplicate of CarIdxTrackSurfaceMaterial
			"PushToTalk",						// live
			"RaceLaps",							// replayed
			"RadioTransmitCarIdx",				// replayed
			"ReplayFrameNum",					// live
			"ReplayFrameNumEnd",				// live
			"ReplayPlaySlowMotion",				// live
			"ReplayPlaySpeed",					// live
			"ReplaySessionNum",					// live
			"ReplaySessionTime",				// live
			"RFbrakeLinePress",					// unknown if replayed but dont need this
			"RFshockDefl",						// not replayed but chatty
			"RFshockDefl_ST",					// not replayed but chatty
			"RFshockVel",						// not replayed but chatty
			"RFshockVel_ST",					// not replayed but chatty
			"Roll",								// replayed
			"RollRate",							// replayed
			"RollRate_ST",						// replayed
			"RPM",								// duplicate of CarIdxRPM
			"RRbrakeLinePress",					// unknown if replayed but dont need this
			"RRshockDefl",						// not replayed but chatty
			"RRshockDefl_ST",					// not replayed but chatty
			"RRshockVel",						// not replayed but chatty
			"RRshockVel_ST",					// not replayed but chatty
			"SessionLapsRemain",				// superseded by SessionLapsRemainEx
			"SessionLapsRemainEx",				// replayed
			"SessionLapsTotal",					// replayed
			"SessionNum",						// replayed
			"SessionState",						// replayed
			"SessionTick",						// live
			"SessionTime",						// replayed but note that live = accurate and replay = junk so don't use this
			"SessionTimeOfDay",					// not replayed but this can be calculated from weekend information
			"SessionTimeRemain",				// replayed
			"SessionTimeTotal",					// replayed
			"SessionUniqueID",					// replayed
			"ShiftGrindRpm",					// not replayed but dont need this
			"ShiftIndicatorPct",				// depreciated, use DriverCarSLBlinkRPM instead
			"ShiftPowerPct",					// not replayed but dont need this
			"Speed",							// replayed
			"SteeringWheelAngle",				// duplicate of CarIdxSteer
			"SteeringWheelAngleMax",			// live
			"SteeringWheelLimiter",				// not replayed but chatty
			"SteeringWheelMaxForceNm",			// not replayed but chatty
			"SteeringWheelPeakForceNm",			// not replayed but chatty
			"SteeringWheelPctDamper",			// not replayed but chatty
			"SteeringWheelPctIntensity",		// not replayed but chatty
			"SteeringWheelPctSmoothing",		// not replayed but chatty
			"SteeringWheelPctTorque",			// not replayed but chatty
			"SteeringWheelPctTorqueSign",		// not replayed but chatty
			"SteeringWheelPctTorqueSignStops",	// not replayed but chatty
			"SteeringWheelTorque",				// not replayed but dont need this
			"SteeringWheelTorque_ST",			// not replayed but dont need this
			"SteeringWheelUseLinear",			// not replayed but dont need this
			"Throttle",							// replayed
			"ThrottleRaw",						// not replayed but chatty
			"TireLF_RumblePitch",				// not replayed but dont need this
			"TireLR_RumblePitch",				// not replayed but dont need this
			"TireRF_RumblePitch",				// not replayed but dont need this
			"TireRR_RumblePitch",				// not replayed but dont need this
			"TrackTemp",						// depreciated, use TrackTempCrew instead
			"VelocityX",						// replayed
			"VelocityX_ST",						// replayed
			"VelocityY",						// replayed
			"VelocityY_ST",						// replayed
			"VelocityZ",						// replayed
			"VelocityZ_ST",						// replayed
			"VertAccel",						// replayed
			"VertAccel_ST",						// replayed
			"VidCapActive",						// live
			"VidCapEnabled",					// live
			"Yaw",								// replayed
			"YawNorth",							// replayed
			"YawRate",							// replayed
			"YawRate_ST",						// replayed
		};

		public ImprovedReplay( string dataFilesPath )
		{
			this.dataFilesPath = dataFilesPath;
		}

		public void Update( IRacingSdkData data )
		{
			if ( subSessionID != data.SessionInfo.WeekendInfo.SubSessionID )
			{
				subSessionID = data.SessionInfo.WeekendInfo.SubSessionID;

				sessionInfoStreamWriter = null;
				telemetryDataStreamWriter = null;

				if ( dataFilesPath != null )
				{
					Directory.CreateDirectory( dataFilesPath );

					var sessionInfoFilePath = Path.Combine( dataFilesPath, $"subses{subSessionID}.info" );
					var telemetryDataFilePath = Path.Combine( dataFilesPath, $"subses{subSessionID}.data" );

					if ( data.SessionInfo.WeekendInfo.SimMode == "replay" )
					{
						LoadSessionInfoFile( sessionInfoFilePath );
						LoadTelemetryDataFile( telemetryDataFilePath );
					}
					else
					{
						sessionInfoStreamWriter = new StreamWriter( sessionInfoFilePath, false, Encoding.UTF8, BufferSize );
						telemetryDataStreamWriter = new StreamWriter( telemetryDataFilePath, false, Encoding.UTF8, BufferSize );
					}
				}
			}
		}

		public void Reset()
		{
			subSessionID = -1;

			sessionInfoStreamWriter = null;
			telemetryDataStreamWriter = null;

			retainedSessionInfo = new();

			retainedTelemetryData.Clear();
		}

		public void RecordSessionInfo( IRacingSdkData data )
		{
			if ( sessionInfoStreamWriter == null )
			{
				return;
			}

			var stringBuilder = new StringBuilder( BufferSize );

			foreach ( var propertyInfo in data.SessionInfo.GetType().GetProperties() )
			{
				var updatedObject = propertyInfo.GetValue( data.SessionInfo );

				if ( updatedObject != null )
				{
					var retainedObject = propertyInfo.GetValue( retainedSessionInfo );

					if ( retainedObject == null )
					{
						var type = updatedObject.GetType();

						retainedObject = Activator.CreateInstance( type ) ?? throw new Exception( $"Could not create new insteance of type {type}!" );

						propertyInfo.SetValue( retainedSessionInfo, retainedObject );
					}

					RecordSessionInfo( propertyInfo.Name, retainedObject, updatedObject, stringBuilder );
				}
			}

			if ( stringBuilder.Length > 0 )
			{
				var sessionNum = data.GetInt( "SessionNum" );
				var sessionTime = data.GetDouble( "SessionTime" );

				sessionInfoStreamWriter.WriteLine( "" );
				sessionInfoStreamWriter.WriteLine( $"SessionTime = {sessionNum}:{sessionTime:0.0000}" );
				sessionInfoStreamWriter.Write( stringBuilder );
			}
		}

		private void RecordSessionInfo( string propertyName, object retainedObject, object updatedObject, StringBuilder stringBuilder )
		{
			foreach ( var propertyInfo in updatedObject.GetType().GetProperties() )
			{
				var updatedValue = propertyInfo.GetValue( updatedObject );
				var retainedValue = propertyInfo.GetValue( retainedObject );

				var isSimpleValue = ( ( updatedValue is null ) || ( updatedValue is string ) || ( updatedValue is int ) || ( updatedValue is float ) || ( updatedValue is double ) );

				if ( isSimpleValue )
				{
					if ( ( ( updatedValue is null ) && ( retainedValue is not null ) ) || ( ( updatedValue is not null ) && !updatedValue.Equals( retainedValue ) ) )
					{
						propertyInfo.SetValue( retainedObject, updatedValue );

						stringBuilder.AppendLine( $"{propertyName}.{propertyInfo.Name} = {updatedValue}" );
					}
				}
				else
				{
					if ( updatedValue is IList updatedList )
					{
						var elementType = propertyInfo.PropertyType.GenericTypeArguments[ 0 ] ?? throw new Exception( "List element type could not be determined!" );

						if ( retainedValue is not IList retainedList )
						{
							var constructedListType = typeof( List<> ).MakeGenericType( elementType );

							retainedList = Activator.CreateInstance( constructedListType ) as IList ?? throw new Exception( "Failed to create new list!" );

							propertyInfo.SetValue( retainedObject, retainedList );
						}

						var index = 0;

						foreach ( var updatedItem in updatedList )
						{
							var retainedItem = ( index < retainedList.Count ) ? retainedList[ index ] : null;

							if ( retainedItem == null )
							{
								retainedItem = Activator.CreateInstance( elementType ) ?? throw new Exception( "Failed to create list item!" );

								retainedList.Add( retainedItem );
							}

							RecordSessionInfo( $"{propertyName}.{propertyInfo.Name}[{index}]", retainedItem, updatedItem, stringBuilder );

							index++;
						}
					}
					else
					{
						Debug.Assert( updatedValue != null );

						if ( retainedValue == null )
						{
							retainedValue = Activator.CreateInstance( propertyInfo.PropertyType ) ?? throw new Exception( "Failed to create object!" );

							propertyInfo.SetValue( retainedObject, retainedValue );
						}

						RecordSessionInfo( $"{propertyName}.{propertyInfo.Name}", retainedValue, updatedValue, stringBuilder );
					}
				}
			}
		}

		public void RecordTelemetryData( IRacingSdkData data )
		{
			if ( telemetryDataStreamWriter == null )
			{
				return;
			}

			if ( retainedTelemetryData.Count == 0 )
			{
				foreach ( var keyValuePair in data.TelemetryDataProperties )
				{
					var iRacingSdkDatum = keyValuePair.Value;

					if ( !ignoredTelemetry.Contains( iRacingSdkDatum.Name ) )
					{
						retainedTelemetryData.Add( new RetainedTelemetryDatum( keyValuePair.Value ) );
					}
				}
			}

			var stringBuilder = new StringBuilder( BufferSize );

			foreach ( var retainedTelemetryDatum in retainedTelemetryData )
			{
				if ( ( retainedTelemetryDatum.lastUpdatedTickCount + retainedTelemetryDatum.updateFrequencyInSeconds * data.TickRate ) <= data.TickCount )
				{
					for ( var valueIndex = 0; valueIndex < retainedTelemetryDatum.iRacingSdkDatum.Count; valueIndex++ )
					{
						object updatedValue = data.GetValue( retainedTelemetryDatum.iRacingSdkDatum.Name, valueIndex );
						object retainedValue = retainedTelemetryDatum.retainedValue[ valueIndex ];

						if ( !updatedValue.Equals( retainedValue ) )
						{
							retainedTelemetryDatum.retainedValue[ valueIndex ] = updatedValue;
							retainedTelemetryDatum.lastUpdatedTickCount = data.TickCount;

							stringBuilder.AppendLine( $"{retainedTelemetryDatum.iRacingSdkDatum.Name}[{valueIndex}] = {updatedValue}" );
						}
					}
				}
			}

			if ( stringBuilder.Length > 0 )
			{
				var sessionNum = data.GetInt( "SessionNum" );
				var sessionTime = data.GetDouble( "SessionTime" );

				telemetryDataStreamWriter.WriteLine( "" );
				telemetryDataStreamWriter.WriteLine( $"SessionTime = {sessionNum}:{sessionTime:0.0000}" );
				telemetryDataStreamWriter.Write( stringBuilder );
			}
		}

		private void LoadSessionInfoFile( string sessionInfoFilePath )
		{
			if ( !File.Exists( sessionInfoFilePath ) )
			{
				Debug.WriteLine( $"Warning - Improved replay file '{sessionInfoFilePath}' does not exist." );

				return;
			}
		}

		private void LoadTelemetryDataFile( string telemetryDataFilePath )
		{
			if ( File.Exists( telemetryDataFilePath ) )
			{
				Debug.WriteLine( $"Warning - Improved replay file '{telemetryDataFilePath}' does not exist." );

				return;
			}
		}

		internal class RetainedTelemetryDatum
		{
			public IRacingSdkDatum iRacingSdkDatum;
			public object[] retainedValue;
			public int lastUpdatedTickCount;
			public int updateFrequencyInSeconds;

			public RetainedTelemetryDatum( IRacingSdkDatum iRacingSdkDatum )
			{
				this.iRacingSdkDatum = iRacingSdkDatum;

				Type type = typeof( char );

				switch ( iRacingSdkDatum.VarType )
				{
					case IRacingSdkEnum.VarType.Char: type = typeof( char ); break;
					case IRacingSdkEnum.VarType.Bool: type = typeof( bool ); break;
					case IRacingSdkEnum.VarType.Int: type = typeof( int ); break;
					case IRacingSdkEnum.VarType.BitField: type = typeof( uint ); break;
					case IRacingSdkEnum.VarType.Float: type = typeof( float ); break;
					case IRacingSdkEnum.VarType.Double: type = typeof( double ); break;
				}

				retainedValue = new object[ iRacingSdkDatum.Count ];

				for ( var index = 0; index < iRacingSdkDatum.Count; index++ )
				{
					retainedValue[ index ] = Activator.CreateInstance( type ) ?? throw new Exception( $"Could not create instance of {type}!" );
				}

				lastUpdatedTickCount = int.MinValue;

				updateFrequencyInSeconds = throttledTelemetry.GetValueOrDefault( iRacingSdkDatum.Name, 0 );
			}
		}
	}
}
