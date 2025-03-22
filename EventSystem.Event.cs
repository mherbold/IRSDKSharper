
using System;

namespace IRSDKSharper
{
	/// <summary>
	/// The EventSystem class is a high-level structure designed to manage and handle events in the IRacingSdk ecosystem.
	/// It provides mechanisms for recording, storing, and processing different kinds of events and tracks associated with
	/// racing simulations.
	/// </summary>
	public partial class EventSystem
	{
		public abstract class Event
		{
			/// <summary>
			/// Represents the session number associated with an event within the EventSystem.
			/// This property is uniquely tied to a specific instance of a session
			/// and is immutable after the object is constructed.
			/// </summary>
			public int SessionNum { get; private set; }

			/// <summary>
			/// Represents the time within the current session.
			/// The time is measured in seconds as a double-precision floating-point number.
			/// </summary>
			public double SessionTime { get; private set; }

			/// <summary>
			/// Represents an instance of IRacingSdkDatum used to capture data from the iRacing SDK.
			/// </summary>
			protected readonly IRacingSdkDatum datum;

			/// Represents a base event occurring during the session in the iRacing system.
			/// Provides properties and behavior related to the session's numerical identifier, time, and data source.
			public Event( int sessionNum, double sessionTime, IRacingSdkDatum datum )
			{
				SessionNum = sessionNum;
				SessionTime = sessionTime;

				this.datum = datum;
			}

			/// <summary>
			/// Gets the session time represented as a formatted string.
			/// </summary>
			/// <remarks>
			/// The session time is converted from seconds to a TimeSpan and formatted
			/// as a string in the "hh:mm:ss.ffff" format.
			/// </remarks>
			public string SessionTimeAsString
			{
				get
				{
					return TimeSpan.FromSeconds( (double) (object) SessionTime ).ToString( @"hh\:mm\:ss\.ffff" );
				}
			}

			/// <summary>
			/// Gets the string representation of the event value.
			/// The format of the output depends on the type of data and unit associated with the event.
			/// </summary>
			/// <remarks>
			/// If the event is associated with specific units or enumerations, the representation is formatted accordingly.
			/// For instance, enumerations like "irsdk_TrkLoc" or "irsdk_SessionState" are converted to their respective string equivalents.
			/// For numeric types such as integers or floats, they are formatted appropriately.
			/// Boolean values are displayed as "True" or "False". For BitField data types, the value is displayed as a hexadecimal string
			/// and may include additional descriptions based on the bit flags.
			/// </remarks>
			public abstract string ValueAsString { get; }
		}

		/// <summary>
		/// Represents an abstract base class for events within the EventSystem,
		/// including details about session number, session time, and data.
		/// </summary>
		public class Event<T> : Event
		{
			/// <summary>
			/// Gets the value of the event, typed as the generic parameter <typeparamref name="T"/>.
			/// This property is readonly and initialized during the object construction of the event.
			/// </summary>
			/// <typeparamref name="T"/> represents the type of the value associated with the event.
			public T Value { get; private set; }

			/// <summary>
			/// Represents a base abstract event in the event system.
			/// This serves as a foundation for other event types.
			/// </summary>
			/// <param name="sessionNum">The session number of the event.</param>
			/// <param name="sessionTime">The session time of the event, in seconds.</param>
			/// <param name="datum">An instance of <see cref="IRacingSdkDatum"/> containing metadata for the event.</param>
			public Event( int sessionNum, double sessionTime, T value, IRacingSdkDatum datum ) : base( sessionNum, sessionTime, datum )
			{
				Value = value;
			}

			/// <summary>
			/// Provides a string representation of the value associated with the event.
			/// The formatting and content depend on the type and unit of the value.
			/// </summary>
			/// <remarks>
			/// This property is an abstract member of the Event base class.
			/// Derived classes must implement this property to define how the value
			/// is converted to a string based on the underlying data type or unit.
			/// The implementation may handle special cases such as enumerations,
			/// bit fields, or numeric types, formatting the output accordingly.
			/// </remarks>
			public override string ValueAsString
			{
				get
				{
					if ( datum == null )
					{
						return Value.ToString() ?? "";
					}
					else
					{
						var valueAsString = "";

						switch ( datum.Unit )
						{
							case "irsdk_TrkLoc":
								valueAsString = EnumAsString<IRacingSdkEnum.TrkLoc>();
								break;

							case "irsdk_TrkSurf":
								valueAsString = EnumAsString<IRacingSdkEnum.TrkSurf>();
								break;

							case "irsdk_SessionState":
								valueAsString = EnumAsString<IRacingSdkEnum.SessionState>();
								break;

							case "irsdk_CarLeftRight":
								valueAsString = EnumAsString<IRacingSdkEnum.CarLeftRight>();
								break;

							case "irsdk_PitSvStatus":
								valueAsString = EnumAsString<IRacingSdkEnum.PitSvStatus>();
								break;

							case "irsdk_PaceMode":
								valueAsString = EnumAsString<IRacingSdkEnum.PaceMode>();
								break;

							case "irsdk_TrackWetness":
								valueAsString = EnumAsString<IRacingSdkEnum.TrackWetness>();
								break;

							default:

								switch ( datum.VarType )
								{
									case IRacingSdkEnum.VarType.Char:
									case IRacingSdkEnum.VarType.Int:
										valueAsString = Value.ToString() ?? "";
										break;

									case IRacingSdkEnum.VarType.Bool:
										valueAsString = ( (bool) (object) Value ) ? "True" : "False";
										break;

									case IRacingSdkEnum.VarType.BitField:
										valueAsString = $"0x{Value:X8}";

										var bitsAsString = string.Empty;

										switch ( datum.Unit )
										{
											case "irsdk_EngineWarnings":
												bitsAsString = EnumAsString<IRacingSdkEnum.EngineWarnings>();
												break;

											case "irsdk_Flags":
												bitsAsString = EnumAsString<IRacingSdkEnum.Flags>();
												break;

											case "irsdk_CameraState":
												bitsAsString = EnumAsString<IRacingSdkEnum.CameraState>();
												break;

											case "irsdk_PitSvFlags":
												bitsAsString = EnumAsString<IRacingSdkEnum.PitSvFlags>();
												break;

											case "irsdk_PaceFlags":
												bitsAsString = EnumAsString<IRacingSdkEnum.PaceFlags>();
												break;
										}

										if ( bitsAsString != string.Empty )
										{
											valueAsString += " - " + bitsAsString;
										}

										break;

									case IRacingSdkEnum.VarType.Float:
									case IRacingSdkEnum.VarType.Double:
										valueAsString = $"{Value,0:N4}";
										break;
								}

								break;
						}

						return valueAsString;
					}
				}
			}

			/// <summary>
			/// Converts the value of the generic type to its string representation based on the given enum.
			/// </summary>
			/// <typeparam name="E">The enum type used for converting the value.</typeparam>
			/// <returns>A string representation of the value of the generic type, based on the defined enum. For a single value, it returns the enum name as a string. For bit fields, it returns a combination of flag names.</returns>
			private string EnumAsString<E>() where E : Enum
			{
				if ( datum == null )
				{
					return Value.ToString() ?? "";
				}
				else if ( datum.VarType == IRacingSdkEnum.VarType.Int )
				{
					var enumValue = (E) (object) Value;

					return enumValue.ToString();
				}
				else
				{
					var bits = (uint) (object) Value;

					var bitsString = string.Empty;

					foreach ( uint bitMask in Enum.GetValues( typeof( E ) ) )
					{
						if ( ( bits & bitMask ) != 0 )
						{
							if ( bitsString != string.Empty )
							{
								bitsString += " | ";
							}

							bitsString += Enum.GetName( typeof( E ), bitMask );
						}
					}

					return bitsString;
				}
			}
		}
	}
}
