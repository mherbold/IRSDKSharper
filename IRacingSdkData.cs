﻿
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Text;

using YamlDotNet.Serialization;

namespace IRSDKSharper
{
	public class IRacingSdkData
	{
		public bool TelemetryDataPropertiesReady { get; private set; } = false;
		public readonly Dictionary<string, IRacingSdkDatum> TelemetryDataProperties = new();
		public string SessionInfoYaml { get; private set; } = string.Empty;
		public IRacingSdkSessionInfo SessionInfo { get; private set; } = null;

		public int Version => memoryMappedViewAccessor?.ReadInt32( 0 ) ?? 0;
		public int Status => memoryMappedViewAccessor?.ReadInt32( 4 ) ?? 0;
		public int TickRate => memoryMappedViewAccessor?.ReadInt32( 8 ) ?? 0;
		public int SessionInfoUpdate => memoryMappedViewAccessor?.ReadInt32( 12 ) ?? 0;
		public int SessionInfoLength => memoryMappedViewAccessor?.ReadInt32( 16 ) ?? 0;
		public int SessionInfoOffset => memoryMappedViewAccessor?.ReadInt32( 20 ) ?? 0;
		public int VarCount => memoryMappedViewAccessor?.ReadInt32( 24 ) ?? 0;
		public int VarHeaderOffset => memoryMappedViewAccessor?.ReadInt32( 28 ) ?? 0;
		public int BufferCount => memoryMappedViewAccessor?.ReadInt32( 32 ) ?? 0;
		public int BufferLength => memoryMappedViewAccessor?.ReadInt32( 36 ) ?? 0;

		public int TickCount { get; private set; } = -1;
		public int Offset { get; private set; } = 0;
		public int FramesDropped { get; private set; } = 0;

		public IRacingSdkHeaderDataAsList headerDataAsList;
		public IRacingSdkSessionInfoAsList sessionInfoAsList;
		public IRacingSdkTelemetryDataAsList telemetryDataAsList;

		public int retryUpdateSessionInfoAfterTickCount = int.MaxValue;

		private readonly bool throwYamlExceptions;

		private readonly Encoding encoding;
		private readonly IDeserializer deserializer;

		private MemoryMappedViewAccessor memoryMappedViewAccessor = null;

		public IRacingSdkData( bool throwYamlExceptions )
		{
			this.throwYamlExceptions = throwYamlExceptions;

			headerDataAsList = new IRacingSdkHeaderDataAsList( this );
			sessionInfoAsList = new IRacingSdkSessionInfoAsList( this );
			telemetryDataAsList = new IRacingSdkTelemetryDataAsList( this );

#if !NET471 && !NET_UNITY_4_8
			Encoding.RegisterProvider( CodePagesEncodingProvider.Instance );
#endif

			encoding = Encoding.GetEncoding( 1252 );

			var deserializerBuilder = new DeserializerBuilder();

			if ( !throwYamlExceptions )
			{
				deserializerBuilder.IgnoreUnmatchedProperties();
			}

			deserializer = deserializerBuilder.Build();
		}

		public void SetMemoryMappedViewAccessor( MemoryMappedViewAccessor memoryMappedViewAccessor )
		{
			this.memoryMappedViewAccessor = memoryMappedViewAccessor;
		}

		public void Reset()
		{
			TelemetryDataPropertiesReady = false;
			TelemetryDataProperties.Clear();

			SessionInfoYaml = string.Empty;
			SessionInfo = null;

			TickCount = -1;
			Offset = 0;
			FramesDropped = 0;

			retryUpdateSessionInfoAfterTickCount = int.MaxValue;
		}

		public void Update()
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			var lastTickCount = TickCount;

			TickCount = -1;
			Offset = 0;

			for ( var i = 0; i < BufferCount; i++ )
			{
				var tickCount = memoryMappedViewAccessor.ReadInt32( 48 + ( i * 16 ) );

				if ( tickCount > TickCount )
				{
					TickCount = tickCount;
					Offset = memoryMappedViewAccessor.ReadInt32( 48 + ( i * 16 ) + 4 );
				}
			}

			if ( lastTickCount != -1 )
			{
				FramesDropped += TickCount - lastTickCount - 1;
			}

			if ( !TelemetryDataPropertiesReady )
			{
				var nameArray = new byte[ IRacingSdkDatum.MaxNameLength ];
				var descArray = new byte[ IRacingSdkDatum.MaxDescLength ];
				var unitArray = new byte[ IRacingSdkDatum.MaxUnitLength ];

				for ( var i = 0; i < VarCount; i++ )
				{
					var varOffset = i * IRacingSdkDatum.Size;

					var type = memoryMappedViewAccessor.ReadInt32( VarHeaderOffset + varOffset );
					var offset = memoryMappedViewAccessor.ReadInt32( VarHeaderOffset + varOffset + 4 );
					var count = memoryMappedViewAccessor.ReadInt32( VarHeaderOffset + varOffset + 8 );
					var countAsTime = memoryMappedViewAccessor.ReadBoolean( VarHeaderOffset + varOffset + 12 );
					var name = ReadString( VarHeaderOffset + varOffset + 16, nameArray );
					var desc = ReadString( VarHeaderOffset + varOffset + 48, descArray );
					var unit = ReadString( VarHeaderOffset + varOffset + 112, unitArray );

					#region Fix some iRacing bugs

					switch ( name )
					{
						case "CRSHshockDefl": name = "CRshockDefl"; break;
						case "CRSHshockDefl_ST": name = "CRshockDefl_ST"; break;
						case "CRSHshockVel": name = "CRshockVel"; break;
						case "CRSHshockVel_ST": name = "CRshockVel_ST"; break;
						case "LFSHshockDefl": name = "LFshockDefl"; break;
						case "LFSHshockDefl_ST": name = "LFshockDefl_ST"; break;
						case "LFSHshockVel": name = "LFshockVel"; break;
						case "LFSHshockVel_ST": name = "LFshockVel_ST"; break;
						case "LRSHshockDefl": name = "LRshockDefl"; break;
						case "LRSHshockDefl_ST": name = "LRshockDefl_ST"; break;
						case "LRSHshockVel": name = "LRshockVel"; break;
						case "LRSHshockVel_ST": name = "LRshockVel_ST"; break;
						case "RFSHshockDefl": name = "RFshockDefl"; break;
						case "RFSHshockDefl_ST": name = "RFshockDefl_ST"; break;
						case "RFSHshockVel": name = "RFshockVel"; break;
						case "RFSHshockVel_ST": name = "RFshockVel_ST"; break;
						case "RRSHshockDefl": name = "RRshockDefl"; break;
						case "RRSHshockDefl_ST": name = "RRshockDefl_ST"; break;
						case "RRSHshockVel": name = "RRshockVel"; break;
						case "RRSHshockVel_ST": name = "RRshockVel_ST"; break;
					};

					switch ( unit )
					{
						case "irsdk_CarLeftRight": type = (int) IRacingSdkEnum.VarType.Int; break;
						case "irsdk_PaceFlags": type = (int) IRacingSdkEnum.VarType.BitField; break;
					};

					#endregion

					TelemetryDataProperties[ name ] = new IRacingSdkDatum( (IRacingSdkEnum.VarType) type, offset, count, countAsTime, name, desc, unit );
				}

				TelemetryDataPropertiesReady = true;
			}
		}

		public bool UpdateSessionInfo()
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			var sessionInfoLength = SessionInfoLength;

			if ( sessionInfoLength > 0 )
			{
				var bytes = new byte[ sessionInfoLength ];

				memoryMappedViewAccessor.ReadArray( SessionInfoOffset, bytes, 0, sessionInfoLength );

				SessionInfoYaml = FixInvalidYaml( bytes );

				var stringReader = new StringReader( SessionInfoYaml );

				try
				{
					var sessionInfo = deserializer.Deserialize<IRacingSdkSessionInfo>( stringReader );

					if ( sessionInfo != null )
					{
						SessionInfo = sessionInfo;

						sessionInfoAsList.Reset();

						return true;
					}
				}
				catch ( Exception )
				{
					if ( throwYamlExceptions )
					{
						throw;
					}
					else
					{
						retryUpdateSessionInfoAfterTickCount = TickCount + TickRate / 2;
					}
				}
			}

			return false;
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public char GetChar( string name, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index, IRacingSdkEnum.VarType.Char );

			return memoryMappedViewAccessor.ReadChar( Offset + TelemetryDataProperties[ name ].Offset + index );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public char GetChar( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Char );

			return memoryMappedViewAccessor.ReadChar( Offset + datum.Offset + index );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetCharArray( string name, char[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Char );

			return memoryMappedViewAccessor.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public bool GetBool( string name, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index, IRacingSdkEnum.VarType.Bool );

			return memoryMappedViewAccessor.ReadBoolean( Offset + TelemetryDataProperties[ name ].Offset + index );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public bool GetBool( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Bool );

			return memoryMappedViewAccessor.ReadBoolean( Offset + datum.Offset + index );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetBoolArray( string name, bool[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Bool );

			return memoryMappedViewAccessor.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetBoolArray( IRacingSdkDatum datum, bool[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.Bool );

			return memoryMappedViewAccessor.ReadArray( Offset + datum.Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetInt( string name, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index, IRacingSdkEnum.VarType.Int );

			return memoryMappedViewAccessor.ReadInt32( Offset + TelemetryDataProperties[ name ].Offset + index * 4 );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetInt( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Int );

			return memoryMappedViewAccessor.ReadInt32( Offset + datum.Offset + index * 4 );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetIntArray( string name, int[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Int );

			return memoryMappedViewAccessor.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetIntArray( IRacingSdkDatum datum, int[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.Int );

			return memoryMappedViewAccessor.ReadArray( Offset + datum.Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public uint GetBitField( string name, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index, IRacingSdkEnum.VarType.BitField );

			return memoryMappedViewAccessor.ReadUInt32( Offset + TelemetryDataProperties[ name ].Offset + index * 4 );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public uint GetBitField( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index, IRacingSdkEnum.VarType.BitField );

			return memoryMappedViewAccessor.ReadUInt32( Offset + datum.Offset + index * 4 );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetBitFieldArray( string name, uint[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.BitField );

			return memoryMappedViewAccessor.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetBitFieldArray( IRacingSdkDatum datum, uint[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.BitField );

			return memoryMappedViewAccessor.ReadArray( Offset + datum.Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public float GetFloat( string name, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index, IRacingSdkEnum.VarType.Float );

			return memoryMappedViewAccessor.ReadSingle( Offset + TelemetryDataProperties[ name ].Offset + index * 4 );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public float GetFloat( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Float );

			return memoryMappedViewAccessor.ReadSingle( Offset + datum.Offset + index * 4 );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetFloatArray( string name, float[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Float );

			return memoryMappedViewAccessor.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetFloatArray( IRacingSdkDatum datum, float[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.Float );

			return memoryMappedViewAccessor.ReadArray( Offset + datum.Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public double GetDouble( string name, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index, IRacingSdkEnum.VarType.Double );

			return memoryMappedViewAccessor.ReadDouble( Offset + TelemetryDataProperties[ name ].Offset + index * 8 );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public double GetDouble( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Double );

			return memoryMappedViewAccessor.ReadDouble( Offset + datum.Offset + index * 8 );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetDoubleArray( string name, double[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Double );

			return memoryMappedViewAccessor.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetDoubleArray( IRacingSdkDatum datum, double[] array, int index, int count )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.Double );

			return memoryMappedViewAccessor.ReadArray( Offset + datum.Offset, array, index, count );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public object GetValue( string name, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( name, index, null );

			var iRacingSdkDatum = TelemetryDataProperties[ name ];

			switch ( iRacingSdkDatum.VarType )
			{
				case IRacingSdkEnum.VarType.Char: return memoryMappedViewAccessor.ReadChar( Offset + iRacingSdkDatum.Offset + index );
				case IRacingSdkEnum.VarType.Bool: return memoryMappedViewAccessor.ReadBoolean( Offset + iRacingSdkDatum.Offset + index );
				case IRacingSdkEnum.VarType.Int: return memoryMappedViewAccessor.ReadInt32( Offset + iRacingSdkDatum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.BitField: return memoryMappedViewAccessor.ReadUInt32( Offset + iRacingSdkDatum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.Float: return memoryMappedViewAccessor.ReadSingle( Offset + iRacingSdkDatum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.Double: return memoryMappedViewAccessor.ReadDouble( Offset + iRacingSdkDatum.Offset + index * 4 );
				default: throw new Exception( "Unexpected type!" );
			};
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public object GetValue( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			Validate( datum, index, null );

			switch ( datum.VarType )
			{
				case IRacingSdkEnum.VarType.Char: return memoryMappedViewAccessor.ReadChar( Offset + datum.Offset + index );
				case IRacingSdkEnum.VarType.Bool: return memoryMappedViewAccessor.ReadBoolean( Offset + datum.Offset + index );
				case IRacingSdkEnum.VarType.Int: return memoryMappedViewAccessor.ReadInt32( Offset + datum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.BitField: return memoryMappedViewAccessor.ReadUInt32( Offset + datum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.Float: return memoryMappedViewAccessor.ReadSingle( Offset + datum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.Double: return memoryMappedViewAccessor.ReadDouble( Offset + datum.Offset + index * 4 );
				default: throw new Exception( "Unexpected type!" );
			};
		}

		[Conditional( "DEBUG" )]
		private void Validate( string name, int index, IRacingSdkEnum.VarType? type )
		{
			if ( !TelemetryDataProperties.TryGetValue( name, out var iRacingSdkDatum ) )
			{
				throw new Exception( $"{name}, {index}: TelemetryDataProperty[ name ] does not exist!" );
			}

			if ( index >= iRacingSdkDatum.Count )
			{
				throw new Exception( $"{name}, {index}: index >= TelemetryDataProperties[ name ].count" );
			}

			if ( ( type != null ) && ( iRacingSdkDatum.VarType != type ) )
			{
				throw new Exception( $"{name}, {index}: TelemetryDataProperties[ name ].VarType != {type}" );
			}
		}

		[Conditional( "DEBUG" )]
		private static void Validate( IRacingSdkDatum datum, int index, IRacingSdkEnum.VarType? type )
		{
			if ( index >= datum.Count )
			{
				throw new Exception( $"{datum.Name}, {index}: index >= TelemetryDataProperties[ name ].count" );
			}

			if ( ( type != null ) && ( datum.VarType != type ) )
			{
				throw new Exception( $"{datum.Name}, {index}: TelemetryDataProperties[ name ].VarType != {type}" );
			}
		}

		private string ReadString( int offset, byte[] buffer )
		{
			Debug.Assert( memoryMappedViewAccessor != null );

			memoryMappedViewAccessor.ReadArray( offset, buffer, 0, buffer.Length );

			return encoding.GetString( buffer ).TrimEnd( '\0' );
		}

		internal class YamlKeyTracker
		{
			public string keyToFix = string.Empty;
			public int counter = 0;
			public bool ignoreUntilNextLine = false;
			public bool addFirstQuote = false;
			public bool addSecondQuote = false;
		}

		private static string FixInvalidYaml( byte[] yaml )
		{
			const int MaxNumDrivers = 64;
			const int MaxNumAdditionalBytesPerFixedKey = 2;

			var keysToFix = new string[]
			{
				"AbbrevName:", "TeamName:", "UserName:", "Initials:", "DriverSetupName:"
			};

			var keyTrackers = new YamlKeyTracker[ keysToFix.Length ];

			for ( var i = 0; i < keyTrackers.Length; i++ )
			{
				keyTrackers[ i ] = new YamlKeyTracker()
				{
					keyToFix = keysToFix[ i ]
				};
			}

			var keyTrackersIgnoringUntilNextLine = 0;

			var stringBuilder = new StringBuilder( yaml.Length + keysToFix.Length * MaxNumAdditionalBytesPerFixedKey * MaxNumDrivers );

			foreach ( char ch in yaml )
			{
				if ( keyTrackersIgnoringUntilNextLine == keyTrackers.Length )
				{
					if ( ch == '\n' )
					{
						keyTrackersIgnoringUntilNextLine = 0;

						foreach ( var keyTracker in keyTrackers )
						{
							keyTracker.counter = 0;
							keyTracker.ignoreUntilNextLine = false;
						}
					}
				}
				else
				{
					foreach ( var keyTracker in keyTrackers )
					{
						if ( keyTracker.ignoreUntilNextLine )
						{
							if ( ch == '\n' )
							{
								keyTracker.counter = 0;
								keyTracker.ignoreUntilNextLine = false;

								keyTrackersIgnoringUntilNextLine--;
							}
						}
						else if ( keyTracker.addFirstQuote )
						{
							if ( ch == '\n' )
							{
								keyTracker.counter = 0;
								keyTracker.addFirstQuote = false;
							}
							else if ( ch != ' ' )
							{
								stringBuilder.Append( '\'' );

								keyTracker.addFirstQuote = false;
								keyTracker.addSecondQuote = true;
							}
						}
						else if ( keyTracker.addSecondQuote )
						{
							if ( ch == '\n' )
							{
								stringBuilder.Append( '\'' );

								keyTracker.counter = 0;
								keyTracker.addSecondQuote = false;
							}
							else if ( ch == '\'' )
							{
								stringBuilder.Append( '\'' );
							}
						}
						else
						{
							if ( ch == keyTracker.keyToFix[ keyTracker.counter ] )
							{
								keyTracker.counter++;

								if ( keyTracker.counter == keyTracker.keyToFix.Length )
								{
									keyTracker.addFirstQuote = true;
								}
							}
							else if ( ch != ' ' )
							{
								keyTracker.ignoreUntilNextLine = true;

								keyTrackersIgnoringUntilNextLine++;
							}
						}
					}
				}

				stringBuilder.Append( ch );
			}

			return stringBuilder.ToString();
		}
	}
}
