
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace IRSDKSharper
{
	public partial class EventSystem
	{
		public abstract class EventTrack
		{
			public List<Event> Events { get; private set; } = new List<Event>();

			protected readonly string trackName;

			protected readonly IRacingSdkEnum.VarType varType;
			protected readonly IRacingSdkDatum datum;
			protected readonly int datumIndex;

			protected bool initialized = false;

			protected int dataTick = 0; // dont record any more data until this tick
			protected int dataRate = 0; // how often (in seconds) to record data (0 = not capped)

			protected int updatesPerSecond = 0;

			private int eventIndex = 0;

			public static Dictionary<string, int> TrackNamesAndDataRates { get; private set; } = new Dictionary<string, int>
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

			public override string ToString()
			{
				return trackName;
			}

			protected EventTrack( string trackName, IRacingSdkEnum.VarType varType )
			{
				this.trackName = trackName;
				this.varType = varType;
			}

			protected EventTrack( IRacingSdkDatum datum, int index )
			{
				trackName = ( datum.Count <= 1 ) ? datum.Name : $"{datum.Name}[{index}]";

				varType = datum.VarType;

				this.datum = datum;
				this.datumIndex = index;

				if ( TrackNamesAndDataRates.ContainsKey( this.datum.Name ) )
				{
					dataRate = TrackNamesAndDataRates[ this.datum.Name ];
				}
			}

			protected EventTrack( string trackName, object valueAsObject )
			{
				this.trackName = trackName;

				switch ( Type.GetTypeCode( valueAsObject.GetType() ) )
				{
					case TypeCode.String: varType = IRacingSdkEnum.VarType.Char; break;
					case TypeCode.Int32: varType = IRacingSdkEnum.VarType.Int; break;
					case TypeCode.Single: varType = IRacingSdkEnum.VarType.Float; break;
					default: throw new Exception( $"Unexpected type ({valueAsObject.GetType().Name})!" );
				};
			}

			public Event GetAt( int sessionNum, double sessionTime )
			{
				if ( Events.Count == 0 )
				{
					return null;
				}
				else if ( Events.Count == 1 )
				{
					return Events[ 0 ];
				}
				else
				{
					var _event = Events[ eventIndex ];

					if ( ( _event.SessionNum > sessionNum ) || ( ( _event.SessionNum == sessionNum ) && ( _event.SessionTime > sessionTime ) ) )
					{
						eventIndex = 0;

						_event = Events[ eventIndex ];
					}

					var maxEventIndex = Events.Count - 1;

					while ( eventIndex < maxEventIndex )
					{
						var nextEventIndex = eventIndex + 1;

						var nextEvent = Events[ nextEventIndex ];

						if ( ( nextEvent.SessionNum > sessionNum ) || ( ( nextEvent.SessionNum == sessionNum ) && ( nextEvent.SessionTime > sessionTime ) ) )
						{
							break;
						}

						_event = nextEvent;

						eventIndex = nextEventIndex;
					}

					return _event;
				}
			}

			public abstract Event Record( EventSystem eventSystem, IRacingSdkData data );

			public abstract Event Record( EventSystem eventSystem, object newValueAsObject );

			public abstract void Load( int sessionNum, double sessionTime, BinaryReader binaryReader );
		}

		public class EventTrack<T> : EventTrack
		{
			private T value = default;

			public EventTrack( string trackName, IRacingSdkEnum.VarType varType ) : base( trackName, varType )
			{
				value = (T) GetDefaultValueAsObject();
			}

			public EventTrack( IRacingSdkDatum datum, int index ) : base( datum, index )
			{
				value = (T) GetDefaultValueAsObject();
			}

			public EventTrack( string trackName, object valueAsObject ) : base( trackName, valueAsObject )
			{
				value = (T) GetDefaultValueAsObject();
			}

			private object GetDefaultValueAsObject()
			{
				object defaultValue;

				switch ( varType )
				{
					case IRacingSdkEnum.VarType.Char: defaultValue = string.Empty; break;
					case IRacingSdkEnum.VarType.Bool: defaultValue = false; break;
					case IRacingSdkEnum.VarType.Int: defaultValue = 0; break;
					case IRacingSdkEnum.VarType.BitField: defaultValue = (uint) 0; break;
					case IRacingSdkEnum.VarType.Float: defaultValue = 0.0f; break;
					case IRacingSdkEnum.VarType.Double: defaultValue = 0.0; break;
					default: throw new Exception( $"Unexpected type ({varType})!" );
				};

				return defaultValue;
			}

			public override Event Record( EventSystem eventSystem, IRacingSdkData data )
			{
				Event _event = null;

				if ( datum != null )
				{
					if ( !initialized || ( dataRate == 0 ) || ( eventSystem.sessionTick >= dataTick ) )
					{
						var newValueAsObject = data.GetValue( datum, datumIndex );

						var newValueAsT = (T) newValueAsObject;

						if ( !initialized || !value.Equals( newValueAsT ) )
						{
							eventSystem.RecordFrameHeader();

							var trackNameId = eventSystem.GetTrackNameId( trackName, varType );

#pragma warning disable CS8602
							eventSystem.binaryWriter.Write( trackNameId );

							switch ( varType )
							{
								case IRacingSdkEnum.VarType.Char: eventSystem.binaryWriter.Write( (string) newValueAsObject ); break;
								case IRacingSdkEnum.VarType.Bool: eventSystem.binaryWriter.Write( (bool) newValueAsObject ); break;
								case IRacingSdkEnum.VarType.Int: eventSystem.binaryWriter.Write( (int) newValueAsObject ); break;
								case IRacingSdkEnum.VarType.BitField: eventSystem.binaryWriter.Write( (uint) newValueAsObject ); break;
								case IRacingSdkEnum.VarType.Float: eventSystem.binaryWriter.Write( (float) newValueAsObject ); break;
								case IRacingSdkEnum.VarType.Double: eventSystem.binaryWriter.Write( (double) newValueAsObject ); break;
							}
#pragma warning restore CS8602

							_event = new Event<T>( eventSystem.sessionNum, eventSystem.sessionTime, newValueAsT, datum );

							Events.Add( _event );

							initialized = true;
							value = newValueAsT;

							if ( dataRate == 0 )
							{
								updatesPerSecond++;
							}
							else
							{
								dataTick = EventTrack<T>.GetNextDataTick( eventSystem.sessionTick, data.TickRate, dataRate );
							}
						}
					}

					if ( ( dataRate == 0 ) && ( updatesPerSecond > 0 ) )
					{
						if ( eventSystem.sessionTick >= dataTick )
						{
							updatesPerSecond--;

							dataTick = eventSystem.sessionTick + data.TickRate / 5;
						}

						if ( updatesPerSecond >= 5 )
						{
							dataRate = 1;
							dataTick = EventTrack<T>.GetNextDataTick( eventSystem.sessionTick, data.TickRate, dataRate );

							updatesPerSecond = 0;
						}
					}
				}

				return _event;
			}

			public override Event Record( EventSystem eventSystem, object newValueAsObject )
			{
				Event _event = null;

				var newValueAsT = (T) newValueAsObject;

				if ( !initialized || !value.Equals( newValueAsT ) )
				{
					eventSystem.RecordFrameHeader();

					var trackNameId = eventSystem.GetTrackNameId( trackName, varType );

#pragma warning disable CS8602
					eventSystem.binaryWriter.Write( trackNameId );

					switch ( varType )
					{
						case IRacingSdkEnum.VarType.Char: eventSystem.binaryWriter.Write( (string) newValueAsObject ); break;
						case IRacingSdkEnum.VarType.Int: eventSystem.binaryWriter.Write( (int) newValueAsObject ); break;
						case IRacingSdkEnum.VarType.Float: eventSystem.binaryWriter.Write( (float) newValueAsObject ); break;
					}
#pragma warning restore CS8602

					_event = new Event<T>( eventSystem.sessionNum, eventSystem.sessionTime, newValueAsT, datum );

					Events.Add( _event );

					initialized = true;
					value = newValueAsT;
				}

				return _event;
			}

			public override void Load( int sessionNum, double sessionTime, BinaryReader binaryReader )
			{
				object newValueAsObject;

				switch ( varType )
				{
					case IRacingSdkEnum.VarType.Char: newValueAsObject = binaryReader.ReadString(); break;
					case IRacingSdkEnum.VarType.Bool: newValueAsObject = binaryReader.ReadBoolean(); break;
					case IRacingSdkEnum.VarType.Int: newValueAsObject = binaryReader.ReadInt32(); break;
					case IRacingSdkEnum.VarType.BitField: newValueAsObject = binaryReader.ReadUInt32(); break;
					case IRacingSdkEnum.VarType.Float: newValueAsObject = binaryReader.ReadSingle(); break;
					case IRacingSdkEnum.VarType.Double: newValueAsObject = binaryReader.ReadDouble(); break;
					default: throw new Exception( $"Unexpected type ({varType})!" );
				};

				Events.Add( new Event<T>( sessionNum, sessionTime, (T) newValueAsObject, datum ) );
			}

			[MethodImpl( MethodImplOptions.AggressiveInlining )]
			private static int GetNextDataTick( int sessionTick, int tickRate, int dataRate )
			{
				return ( sessionTick + ( dataRate * tickRate ) ) / dataRate * dataRate;
			}
		}
	}
}
