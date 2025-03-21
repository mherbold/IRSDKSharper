
using System;
using System.Collections;

namespace IRSDKSharper
{
	/// <summary>
	/// Represents a collection of telemetry data from the iRacing SDK, exposed as a list.
	/// The class allows access to telemetry data mapped to key-value pairs, including metadata
	/// such as units and descriptions.
	/// </summary>
	/// <remarks>
	/// This class implements the <see cref="IList"/> interface but is read-only and fixed-size.
	/// It is designed for accessing the telemetry data provided by iRacing SDK and does not
	/// support modification or resizing.
	/// </remarks>
	public class IRacingSdkTelemetryDataAsList : IList
	{
		private readonly IRacingSdkData data;

		private int count = -1;

		private int lastIndex;
		private Datum lastDatum;

		public IRacingSdkTelemetryDataAsList( IRacingSdkData data )
		{
			this.data = data;

			Reset();
		}

		/// <summary>
		/// Resets the internal state of the telemetry data list to its initial state.
		/// </summary>
		/// <remarks>
		/// This method sets the internal index used for navigating telemetry data to its default value.
		/// It is typically used to reinitialize the telemetry data list for fresh access.
		/// </remarks>
		public void Reset()
		{
			lastIndex = -1;
		}

		/// <summary>
		/// Provides indexed access to the telemetry data in the collection.
		/// </summary>
		/// <param name="index">The zero-based index of the telemetry data to retrieve.</param>
		/// <returns>An object representing the telemetry datum at the specified index.</returns>
		/// <exception cref="NotImplementedException">Thrown when an attempt is made to set a value, as the collection is read-only.</exception>
		/// <remarks>
		/// Accessing an index retrieves the corresponding telemetry datum, internally caching the result
		/// to optimize repeated lookups. The set accessor is not implemented, as the collection is immutable.
		/// </remarks>
		public object this[ int index ]
		{
			get
			{
				if ( index != lastIndex )
				{
					Datum datum = new Datum( "???", "???", "???", "???" );

					var currentOffset = 0;

					foreach ( var keyValuePair in data.TelemetryDataProperties )
					{
						if ( currentOffset + keyValuePair.Value.Count > index )
						{
							var valueIndex = index - currentOffset;

							var key = keyValuePair.Key;

							if ( keyValuePair.Value.Count > 1 )
							{
								key += $"[ {valueIndex} ]";
							}

							var valueAsString = string.Empty;
							var bitsAsString = string.Empty;

							switch ( keyValuePair.Value.Unit )
							{
								case "irsdk_TrkLoc":
									valueAsString = EnumAsString<IRacingSdkEnum.TrkLoc>( keyValuePair.Value, valueIndex );
									break;

								case "irsdk_TrkSurf":
									valueAsString = EnumAsString<IRacingSdkEnum.TrkSurf>( keyValuePair.Value, valueIndex );
									break;

								case "irsdk_SessionState":
									valueAsString = EnumAsString<IRacingSdkEnum.SessionState>( keyValuePair.Value, valueIndex );
									break;

								case "irsdk_CarLeftRight":
									valueAsString = EnumAsString<IRacingSdkEnum.CarLeftRight>( keyValuePair.Value, valueIndex );
									break;

								case "irsdk_PitSvStatus":
									valueAsString = EnumAsString<IRacingSdkEnum.PitSvStatus>( keyValuePair.Value, valueIndex );
									break;

								case "irsdk_PaceMode":
									valueAsString = EnumAsString<IRacingSdkEnum.PaceMode>( keyValuePair.Value, valueIndex );
									break;

								case "irsdk_TrackWetness":
									valueAsString = EnumAsString<IRacingSdkEnum.TrackWetness>( keyValuePair.Value, valueIndex );
									break;

								default:

									switch ( keyValuePair.Value.VarType )
									{
										case IRacingSdkEnum.VarType.Char:
											valueAsString = data.GetChar( keyValuePair.Value, valueIndex ).ToString();
											break;

										case IRacingSdkEnum.VarType.Bool:
											valueAsString = data.GetBool( keyValuePair.Value, valueIndex ) ? "True" : "False";
											break;

										case IRacingSdkEnum.VarType.Int:
											var valueAsInt = data.GetInt( keyValuePair.Value, valueIndex );

											if ( valueAsInt == -1 )
											{
												valueAsString = string.Empty;
											}
											else
											{
												valueAsString = $"{valueAsInt,0:N0}";
											}

											break;

										case IRacingSdkEnum.VarType.BitField:
											valueAsString = $"0x{data.GetBitField( keyValuePair.Value, valueIndex ):X8}";

											switch ( keyValuePair.Value.Unit )
											{
												case "irsdk_EngineWarnings":
													bitsAsString = EnumAsString<IRacingSdkEnum.EngineWarnings>( keyValuePair.Value, valueIndex );
													break;

												case "irsdk_Flags":
													bitsAsString = EnumAsString<IRacingSdkEnum.Flags>( keyValuePair.Value, valueIndex );
													break;

												case "irsdk_CameraState":
													bitsAsString = EnumAsString<IRacingSdkEnum.CameraState>( keyValuePair.Value, valueIndex );
													break;

												case "irsdk_PitSvFlags":
													bitsAsString = EnumAsString<IRacingSdkEnum.PitSvFlags>( keyValuePair.Value, valueIndex );
													break;

												case "irsdk_PaceFlags":
													bitsAsString = EnumAsString<IRacingSdkEnum.PaceFlags>( keyValuePair.Value, valueIndex );
													break;
											}

											break;

										case IRacingSdkEnum.VarType.Float:
											var valueAsFloat = data.GetFloat( keyValuePair.Value, valueIndex );

											if ( valueAsFloat == -1 )
											{
												valueAsString = string.Empty;
											}
											else
											{
												if ( keyValuePair.Value.Unit == "%" )
												{
													valueAsFloat *= 100;
												}

												valueAsString = $"{valueAsFloat,0:N4}";
											}

											break;

										case IRacingSdkEnum.VarType.Double:
											valueAsString = $"{data.GetDouble( keyValuePair.Value, valueIndex ),0:N4}";
											break;
									}

									break;
							}

							datum = new Datum( key, valueAsString, keyValuePair.Value.Unit, bitsAsString == string.Empty ? keyValuePair.Value.Desc : bitsAsString );

							break;
						}


						currentOffset += keyValuePair.Value.Count;
					}

					lastIndex = index;
					lastDatum = datum;
				}

				return lastDatum;
			}

			set => throw new NotImplementedException();
		}

		private string EnumAsString<T>( IRacingSdkDatum var, int index ) where T : Enum
		{
			if ( var.VarType == IRacingSdkEnum.VarType.Int )
			{
				var enumValue = (T) (object) data.GetInt( var, index );

				return enumValue.ToString();
			}
			else
			{
				var bits = data.GetBitField( var, index );

				var bitsString = string.Empty;

				foreach ( uint bitMask in Enum.GetValues( typeof( T ) ) )
				{
					if ( ( bits & bitMask ) != 0 )
					{
						if ( bitsString != string.Empty )
						{
							bitsString += " | ";
						}

						bitsString += Enum.GetName( typeof( T ), bitMask );
					}
				}

				return bitsString;
			}
		}

		public bool IsFixedSize => true;

		public bool IsReadOnly => true;

		public int Count
		{
			get
			{
				if ( count == -1 )
				{
					if ( !data.TelemetryDataPropertiesReady )
					{
						return 0;
					}
					else
					{
						count = 0;

						foreach ( var keyValuePair in data.TelemetryDataProperties )
						{
							count += keyValuePair.Value.Count;
						}
					}
				}

				return count;
			}

			set => throw new NotImplementedException();
		}

		public bool IsSynchronized => false;

		public object SyncRoot => throw new NotImplementedException();

		public int Add( object value )
		{
			throw new NotImplementedException();
		}

		public void Clear()
		{
			throw new NotImplementedException();
		}

		public bool Contains( object value )
		{
			throw new NotImplementedException();
		}

		public void CopyTo( Array array, int index )
		{
			throw new NotImplementedException();
		}

		public IEnumerator GetEnumerator()
		{
			throw new NotImplementedException();
		}

		public int IndexOf( object value )
		{
			throw new NotImplementedException();
		}

		public void Insert( int index, object value )
		{
			throw new NotImplementedException();
		}

		public void Remove( object value )
		{
			throw new NotImplementedException();
		}

		public void RemoveAt( int index )
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Represents a telemetry data point within the iRacing SDK telemetry system.
		/// Each Datum instance encapsulates a key, value, unit, and description
		/// for detailed representation of a telemetry property.
		/// </summary>
		/// <remarks>
		/// The class is primarily used internally by the <see cref="IRacingSdkTelemetryDataAsList"/>
		/// to store telemetry property metadata and values. It includes essential information
		/// that describes what the telemetry field represents, its value, and its unit of measure.
		/// </remarks>
		public class Datum
		{
			public string key;
			public string value;
			public string unit;
			public string description;

			public Datum( string key, string value, string unit, string description )
			{
				this.key = key;
				this.value = value;
				this.unit = unit;
				this.description = description;
			}
		}
	}
}
