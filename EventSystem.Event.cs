
using System;

namespace IRSDKSharper
{
	public partial class EventSystem
	{
		public abstract class Event
		{
			public int SessionNum { get; private set; }
			public double SessionTime { get; private set; }

			protected readonly IRacingSdkDatum datum;

			public Event( int sessionNum, double sessionTime, IRacingSdkDatum datum )
			{
				SessionNum = sessionNum;
				SessionTime = sessionTime;

				this.datum = datum;
			}

			public string SessionTimeAsString
			{
				get
				{
					return TimeSpan.FromSeconds( (double) (object) SessionTime ).ToString( @"hh\:mm\:ss\.ffff" );
				}
			}

			public abstract string ValueAsString { get; }
		}

		public class Event<T> : Event
		{
			public T Value { get; private set; }

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
