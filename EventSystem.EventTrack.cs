
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace IRSDKSharper
{
	public partial class EventSystem
	{
		/// <summary>
		/// Represents a time-ordered series of recorded events for a single track.
		/// </summary>
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

			/// <summary>
			/// Gets the preferred recording interval, in seconds, for selected track names.
			/// </summary>
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

			/// <summary>
			/// Returns the track name.
			/// </summary>
			/// <returns>The event track name.</returns>
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

			/// <summary>
			/// Gets the most recent event at or before the specified session position.
			/// </summary>
			/// <param name="sessionNum">The session number to query.</param>
			/// <param name="sessionTime">The session time, in seconds, to query.</param>
			/// <returns>The matching event, or <see langword="null"/> if the track has no events.</returns>
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

			/// <summary>
			/// Records a new value from the current SDK snapshot if the track changed.
			/// </summary>
			/// <param name="eventSystem">The owning event system.</param>
			/// <param name="data">The current SDK data snapshot.</param>
			/// <returns>The recorded event, or <see langword="null"/> when nothing changed.</returns>
			public abstract Event Record( EventSystem eventSystem, IRacingSdkData data );

			/// <summary>
			/// Records a new value supplied directly by the caller if the track changed.
			/// </summary>
			/// <param name="eventSystem">The owning event system.</param>
			/// <param name="newValueAsObject">The value to record.</param>
			/// <returns>The recorded event, or <see langword="null"/> when nothing changed.</returns>
			public abstract Event Record( EventSystem eventSystem, object newValueAsObject );

			/// <summary>
			/// Loads a previously recorded event from a binary stream.
			/// </summary>
			/// <param name="sessionNum">The session number for the event.</param>
			/// <param name="sessionTime">The session time, in seconds, for the event.</param>
			/// <param name="binaryReader">The binary reader positioned at the event value.</param>
			public abstract void Load( int sessionNum, double sessionTime, BinaryReader binaryReader );
		}

		/// <summary>
		/// Represents a strongly typed event track.
		/// </summary>
		/// <typeparam name="T">The event value type.</typeparam>
		public class EventTrack<T> : EventTrack
		{
			private T value = default;

			/// <summary>
			/// Initializes a new instance of the <see cref="EventTrack{T}"/> class for a named track.
			/// </summary>
			/// <param name="trackName">The track name.</param>
			/// <param name="varType">The track value type.</param>
			public EventTrack( string trackName, IRacingSdkEnum.VarType varType ) : base( trackName, varType )
			{
				value = (T) GetDefaultValueAsObject();
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="EventTrack{T}"/> class for a telemetry datum.
			/// </summary>
			/// <param name="datum">The source telemetry datum.</param>
			/// <param name="index">The zero-based element index for array values.</param>
			public EventTrack( IRacingSdkDatum datum, int index ) : base( datum, index )
			{
				value = (T) GetDefaultValueAsObject();
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="EventTrack{T}"/> class for a non-telemetry value source.
			/// </summary>
			/// <param name="trackName">The track name.</param>
			/// <param name="valueAsObject">The initial value used to infer the track type.</param>
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

			/// <summary>
			/// Records a new event from telemetry data when the value changes.
			/// </summary>
			/// <param name="eventSystem">The owning event system.</param>
			/// <param name="data">The current SDK data snapshot.</param>
			/// <returns>The recorded event, or <see langword="null"/> when nothing changed.</returns>
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

			/// <summary>
			/// Records a new event from a supplied value when the value changes.
			/// </summary>
			/// <param name="eventSystem">The owning event system.</param>
			/// <param name="newValueAsObject">The value to record.</param>
			/// <returns>The recorded event, or <see langword="null"/> when nothing changed.</returns>
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

			/// <summary>
			/// Loads a recorded event from a binary reader.
			/// </summary>
			/// <param name="sessionNum">The session number for the event.</param>
			/// <param name="sessionTime">The session time, in seconds, for the event.</param>
			/// <param name="binaryReader">The binary reader positioned at the event value.</param>
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
