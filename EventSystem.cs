
using System.Collections;
using System.Diagnostics;
using System.Text;

namespace HerboldRacing
{
	public class EventSystem
	{
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
			var sessionInfo = data.SessionInfo;

			if ( sessionInfo != null )
			{
				if ( subSessionID != sessionInfo.WeekendInfo.SubSessionID )
				{
					Reset();

					subSessionID = sessionInfo.WeekendInfo.SubSessionID;

					Directory.CreateDirectory( directory );

					var filePath = Path.Combine( directory, $"subses{subSessionID}.yaml" );

					if ( sessionInfo.WeekendInfo.SimMode == "replay" )
					{
						LoadEvents( filePath );
					}
					else
					{
						streamWriter = new StreamWriter( filePath, true, Encoding.UTF8, BufferSize );
					}
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
				if ( eventTracks.Initialize( data ) )
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
			private bool initialized = false;

			public readonly Dictionary<string, TelemetryDataTrack> telemetryDataTracks = new();
			public readonly Dictionary<string, SessionInfoTrack> sessionInfoTracks = new();

			public static Dictionary<string, bool> DisallowedTelemetryDataDictionary { get; private set; } = new()
			{
				{ "CamCameraNumber", false },
				{ "CamCameraState", false },
				{ "CamCarIdx", false },
				{ "CamGroupNumber", false },
				{ "CarIdxEstTime", false },
				{ "CarIdxLapDistPct", false },
				{ "CarIdxRPM", false },
				{ "CarIdxSteer", false },
				{ "Engine0_RPM", false },
				{ "IsDiskLoggingActive", false },
				{ "IsDiskLoggingEnabled", false },
				{ "IsGarageVisible", false },
				{ "IsReplayPlaying", false },
				{ "LapCurrentLapTime", false },
				{ "LapDist", false },
				{ "LapDistPct", false },
				{ "LatAccel", false },
				{ "LatAccel_ST", false },
				{ "LFbrakeLinePress", false },
				{ "LFshockDefl", false },
				{ "LFshockDefl_ST", false },
				{ "LFshockVel", false },
				{ "LFshockVel_ST", false },
				{ "LoadNumTextures", false },
				{ "LongAccel", false },
				{ "LongAccel_ST", false },
				{ "LRbrakeLinePress", false },
				{ "LRshockDefl", false },
				{ "LRshockDefl_ST", false },
				{ "LRshockVel", false },
				{ "LRshockVel_ST", false },
				{ "MemPageFaultSec", false },
				{ "MemSoftPageFaultSec", false },
				{ "OkToReloadTextures", false },
				{ "Pitch", false },
				{ "PitchRate", false },
				{ "PitchRate_ST", false },
				{ "ReplayFrameNum", false },
				{ "ReplayFrameNumEnd", false },
				{ "ReplayPlaySlowMotion", false },
				{ "ReplayPlaySpeed", false },
				{ "ReplaySessionNum", false },
				{ "ReplaySessionTime", false },
				{ "RFbrakeLinePress", false },
				{ "RFshockDefl", false },
				{ "RFshockDefl_ST", false },
				{ "RFshockVel", false },
				{ "RFshockVel_ST", false },
				{ "Roll", false },
				{ "RollRate", false },
				{ "RollRate_ST", false },
				{ "RPM", false },
				{ "RRbrakeLinePress", false },
				{ "RRshockDefl", false },
				{ "RRshockDefl_ST", false },
				{ "RRshockVel", false },
				{ "RRshockVel_ST", false },
				{ "SessionNum", false },
				{ "SessionTick", false },
				{ "SessionTime", false },
				{ "Speed", false },
				{ "SteeringWheelAngle", false },
				{ "SteeringWheelLimiter", false },
				{ "SteeringWheelMaxForceNm", false },
				{ "SteeringWheelPctDamper", false },
				{ "SteeringWheelPctIntensity", false },
				{ "SteeringWheelPctSmoothing", false },
				{ "SteeringWheelPctTorque", false },
				{ "SteeringWheelPctTorqueSign", false },
				{ "SteeringWheelPctTorqueSignStops", false },
				{ "SteeringWheelPeakForceNm", false },
				{ "SteeringWheelTorque", false },
				{ "SteeringWheelTorque_ST", false },
				{ "SteeringWheelUseLinear", false },
				{ "TireLF_RumblePitch", false },
				{ "TireLR_RumblePitch", false },
				{ "TireRF_RumblePitch", false },
				{ "TireRR_RumblePitch", false },
				{ "VelocityX", false },
				{ "VelocityX_ST", false },
				{ "VelocityY", false },
				{ "VelocityY_ST", false },
				{ "VelocityZ", false },
				{ "VelocityZ_ST", false },
				{ "VertAccel", false },
				{ "VertAccel_ST", false },
				{ "VidCapActive", false },
				{ "VidCapEnabled", false },
				{ "Yaw", false },
				{ "YawNorth", false },
				{ "YawRate", false },
				{ "YawRate_ST", false },
			};

			public bool Initialize( IRacingSdkData data )
			{
				if ( !initialized )
				{
					foreach ( var keyValuePair in data.TelemetryDataProperties )
					{
						if ( !DisallowedTelemetryDataDictionary.ContainsKey( keyValuePair.Key ) )
						{
							for ( var index = 0; index < keyValuePair.Value.Count; index++ )
							{
								TelemetryDataTrack telemetryDataTrack = keyValuePair.Value.VarType switch
								{
									IRacingSdkEnum.VarType.Char => new TelemetryDataTrack<char>( keyValuePair.Value, index ),
									IRacingSdkEnum.VarType.Bool => new TelemetryDataTrack<bool>( keyValuePair.Value, index ),
									IRacingSdkEnum.VarType.Int => new TelemetryDataTrack<int>( keyValuePair.Value, index ),
									IRacingSdkEnum.VarType.BitField => new TelemetryDataTrack<uint>( keyValuePair.Value, index ),
									IRacingSdkEnum.VarType.Float => new TelemetryDataTrack<float>( keyValuePair.Value, index ),
									IRacingSdkEnum.VarType.Double => new TelemetryDataTrack<double>( keyValuePair.Value, index ),
									_ => throw new Exception( $"Unexpected type ({keyValuePair.Value.VarType})!" )
								};

								telemetryDataTracks.Add( $"{keyValuePair.Value.Name}.{index}", telemetryDataTrack );
							}
						}
					}

					initialized = true;
				}

				return initialized;
			}

			public void Reset()
			{
				telemetryDataTracks.Clear();
				sessionInfoTracks.Clear();

				initialized = false;
			}

			public void Update( int sessionNum, double sessionTime, int sessionTick, StringBuilder stringBuilder, IRacingSdkData data )
			{
				foreach ( var keyValuePair in telemetryDataTracks )
				{
					keyValuePair.Value.Update( sessionNum, sessionTime, sessionTick, stringBuilder, data );
				}

				var sessionInfo = data.SessionInfo;

				if ( sessionInfo != null )
				{
					foreach ( var propertyInfo in sessionInfo.GetType().GetProperties() )
					{
						Update( sessionNum, sessionTime, sessionTick, stringBuilder, propertyInfo.Name, propertyInfo.GetValue( sessionInfo ) );
					}
				}
			}

			private void Update( int sessionNum, double sessionTime, int sessionTick, StringBuilder stringBuilder, string propertyName, object? valueAsObject )
			{
				if ( valueAsObject != null )
				{
					var isSimpleValue = ( ( valueAsObject is string ) || ( valueAsObject is int ) || ( valueAsObject is float ) || ( valueAsObject is double ) );

					if ( isSimpleValue )
					{
						var sessionInfoTrack = Initialize( propertyName, valueAsObject );

						sessionInfoTrack.Update( sessionNum, sessionTime, sessionTick, stringBuilder, valueAsObject );
					}
					else if ( valueAsObject is IList list )
					{
						var index = 0;

						foreach ( var item in list )
						{
							Update( sessionNum, sessionTime, sessionTick, stringBuilder, $"{propertyName}[{index}]", item );

							index++;
						}
					}
					else
					{
						foreach ( var propertyInfo in valueAsObject.GetType().GetProperties() )
						{
							Update( sessionNum, sessionTime, sessionTick, stringBuilder, $"{propertyName}.{propertyInfo.Name}", propertyInfo.GetValue( valueAsObject ) );
						}
					}
				}
			}

			private SessionInfoTrack Initialize( string propertyName, object? valueAsObject )
			{
				if ( !sessionInfoTracks.ContainsKey( propertyName ) )
				{
					SessionInfoTrack sessionInfoTrack = valueAsObject switch
					{
						string => new SessionInfoTrack<string>( propertyName, valueAsObject ),
						int => new SessionInfoTrack<int>( propertyName, valueAsObject ),
						float => new SessionInfoTrack<float>( propertyName, valueAsObject ),
						_ => throw new Exception( $"Unexpected type ({valueAsObject?.GetType().Name})!" )
					};

					sessionInfoTracks.Add( propertyName, sessionInfoTrack );

					return sessionInfoTrack;
				}
				else
				{
					return sessionInfoTracks[ propertyName ];
				}
			}

			public abstract class Event
			{
			}

			public class Event<T> : Event where T : IEquatable<T>
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

			public abstract class TelemetryDataTrack
			{
				protected readonly IRacingSdkDatum datum;
				protected readonly int index;

				protected int secondsPerValue = 0;
				protected int newValueCount = 0;

				protected readonly List<Event> events = new();

				public static Dictionary<string, int> ManualSecondsPerValueDictionary { get; private set; } = new()
				{
					{ "AirDensity", 60 },
					{ "AirPressure", 60 },
					{ "AirTemp", 60 },
					{ "Brake", 1 },
					{ "BrakeRaw", 1 },
					{ "CarIdxRPM", 1 },
					{ "ChanAvgLatency", 15 },
					{ "ChanClockSkew", 15 },
					{ "ChanLatency", 15 },
					{ "ChanPartnerQuality", 15 },
					{ "ChanQuality", 15 },
					{ "Clutch", 1 },
					{ "ClutchRaw", 1 },
					{ "CpuUsageBG", 60 },
					{ "CpuUsageFG", 60 },
					{ "dcBrakeBias", 1 },
					{ "Engine0_RPM", 1 },
					{ "FogLevel", 60 },
					{ "FrameRate", 60 },
					{ "FuelLevel", 1 },
					{ "FuelLevelPct", 1 },
					{ "FuelPress", 1 },
					{ "FuelUsePerHour", 1 },
					{ "GpuUsage", 60 },
					{ "HandbrakeRaw", 1 },
					{ "ManifoldPress", 1 },
					{ "OilLevel", 1 },
					{ "OilPress", 1 },
					{ "OilTemp", 1 },
					{ "PlayerCarTowTime", 1 },
					{ "Precipitation", 60 },
					{ "RelativeHumidity", 60 },
					{ "RPM", 1 },
					{ "SessionTimeOfDay", 60 },
					{ "SessionTimeRemain", 1 },
					{ "SolarAltitude", 60 },
					{ "SolarAzimuth", 60 },
					{ "Throttle", 1 },
					{ "ThrottleRaw", 1 },
					{ "TrackTemp", 60 },
					{ "TrackTempCrew", 60 },
					{ "Voltage", 1 },
					{ "WaterLevel", 1 },
					{ "WaterTemp", 1 },
					{ "WindDir", 60 },
					{ "WindVel", 60 },
				};

				protected TelemetryDataTrack( IRacingSdkDatum datum, int index )
				{
					this.datum = datum;
					this.index = index;

					if ( ManualSecondsPerValueDictionary.ContainsKey( datum.Name ) )
					{
						secondsPerValue = ManualSecondsPerValueDictionary[ datum.Name ];
					}
				}

				public abstract void Update( int sessionNum, double sessionTime, int sessionTick, StringBuilder stringBuilder, IRacingSdkData data );
			}

			public class TelemetryDataTrack<T> : TelemetryDataTrack where T : IEquatable<T>
			{
				private bool initialized = false;
				private T value;

				public TelemetryDataTrack( IRacingSdkDatum datum, int index ) : base( datum, index )
				{
					object defaultValue = datum.VarType switch
					{
						IRacingSdkEnum.VarType.Char => (char) 0,
						IRacingSdkEnum.VarType.Bool => false,
						IRacingSdkEnum.VarType.Int => 0,
						IRacingSdkEnum.VarType.BitField => (uint) 0,
						IRacingSdkEnum.VarType.Float => 0.0f,
						IRacingSdkEnum.VarType.Double => 0.0,
						_ => throw new Exception( $"Unexpected type ({datum.VarType})!" )
					};

					value = (T) defaultValue;
				}

				public override void Update( int sessionNum, double sessionTime, int sessionTick, StringBuilder stringBuilder, IRacingSdkData data )
				{
					if ( !initialized || ( secondsPerValue == 0 ) || ( sessionTick % ( data.TickRate * secondsPerValue ) ) == 0 )
					{
						var newValue = (T) data.GetValue( datum, index );

						if ( !initialized || !value.Equals( newValue ) )
						{
							stringBuilder.AppendLine( $" TD.{datum.Name}.{index}: {newValue}" );

							events.Add( new Event<T>( sessionNum, sessionTime, newValue ) );

							initialized = true;
							value = newValue;

							newValueCount++;
						}
					}

					if ( ( secondsPerValue == 0 ) && ( newValueCount > 0 ) )
					{
						if ( ( sessionTick % ( data.TickRate / 5 ) ) == 0 )
						{
							newValueCount--;
						}

						if ( newValueCount >= 5 )
						{
							secondsPerValue = 1;
						}
					}
				}
			}

			public abstract class SessionInfoTrack
			{
				protected readonly string propertyName;

				protected readonly List<Event> events = new();

				protected SessionInfoTrack( string propertyName )
				{
					this.propertyName = propertyName;
				}

				public abstract void Update( int sessionNum, double sessionTime, int sessionTick, StringBuilder stringBuilder, object valueAsObject );
			}

			public class SessionInfoTrack<T> : SessionInfoTrack where T : IEquatable<T>
			{
				private bool initialized = false;
				private T value;

				public SessionInfoTrack( string propertyName, object valueAsObject ) : base( propertyName )
				{
					object defaultValue = valueAsObject switch
					{
						string => string.Empty,
						int => 0,
						float => 0.0f,
						_ => throw new Exception( $"Unexpected type ({valueAsObject.GetType().Name})!" )
					};

					value = (T) defaultValue;
				}

				public override void Update( int sessionNum, double sessionTime, int sessionTick, StringBuilder stringBuilder, object valueAsObject )
				{
					var newValue = (T) valueAsObject;

					if ( !initialized || !value.Equals( newValue ) )
					{
						stringBuilder.AppendLine( $" SI.{propertyName}: {newValue}" );

						events.Add( new Event<T>( sessionNum, sessionTime, newValue ) );

						initialized = true;
						value = newValue;
					}
				}
			}
		}
	}
}
