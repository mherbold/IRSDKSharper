
using System;

namespace IRSDKSharper
{
	public partial class EventSystem
	{
		/// <summary>
		/// Represents a value recorded at a specific session number and time.
		/// </summary>
		public abstract class Event
		{
			public int SessionNum { get; private set; }
			public double SessionTime { get; private set; }

			protected readonly IRacingSdkDatum datum;

			/// <summary>
			/// Initializes a new instance of the <see cref="Event"/> class.
			/// </summary>
			/// <param name="sessionNum">The session number in which the event occurred.</param>
			/// <param name="sessionTime">The session time, in seconds, at which the event occurred.</param>
			/// <param name="datum">The source datum associated with the event, if any.</param>
			public Event( int sessionNum, double sessionTime, IRacingSdkDatum datum )
			{
				SessionNum = sessionNum;
				SessionTime = sessionTime;

				this.datum = datum;
			}

			/// <summary>
			/// Gets the session time formatted as <c>hh:mm:ss.ffff</c>.
			/// </summary>
			public string SessionTimeAsString
			{
				get
				{
					return TimeSpan.FromSeconds( (double) (object) SessionTime ).ToString( @"hh\:mm\:ss\.ffff" );
				}
			}

			/// <summary>
			/// Gets the recorded value formatted for display.
			/// </summary>
			public abstract string ValueAsString { get; }
		}

		/// <summary>
		/// Represents a strongly typed recorded event.
		/// </summary>
		/// <typeparam name="T">The event value type.</typeparam>
		public class Event<T> : Event
		{
			public T Value { get; private set; }

			/// <summary>
			/// Initializes a new instance of the <see cref="Event{T}"/> class.
			/// </summary>
			/// <param name="sessionNum">The session number in which the event occurred.</param>
			/// <param name="sessionTime">The session time, in seconds, at which the event occurred.</param>
			/// <param name="value">The recorded value.</param>
			/// <param name="datum">The source datum associated with the event, if any.</param>
			public Event( int sessionNum, double sessionTime, T value, IRacingSdkDatum datum ) : base( sessionNum, sessionTime, datum )
			{
				Value = value;
			}

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
