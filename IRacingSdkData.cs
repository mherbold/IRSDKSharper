
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Text;

using YamlDotNet.Serialization;

namespace HerboldRacing
{
	public class IRacingSdkData
	{
		public Dictionary<string, IRacingSdkDatum>? TelemetryData { get; private set; } = null;

		public string? SessionInfoYaml { get; private set; } = null;

		public IRacingSdkSessionInfo? SessionInfo { get; private set; } = null;

		public int Version => memoryMappedViewAccessor.ReadInt32( 0 );
		public int Status => memoryMappedViewAccessor.ReadInt32( 4 );
		public int TickRate => memoryMappedViewAccessor.ReadInt32( 8 );
		public int SessionInfoUpdate => memoryMappedViewAccessor.ReadInt32( 12 );
		public int SessionInfoLength => memoryMappedViewAccessor.ReadInt32( 16 );
		public int SessionInfoOffset => memoryMappedViewAccessor.ReadInt32( 20 );
		public int VarCount => memoryMappedViewAccessor.ReadInt32( 24 );
		public int VarHeaderOffset => memoryMappedViewAccessor.ReadInt32( 28 );
		public int BufferCount => memoryMappedViewAccessor.ReadInt32( 32 );
		public int BufferLength => memoryMappedViewAccessor.ReadInt32( 36 );

		public int TickCount { get; private set; } = -1;
		public int Offset { get; private set; } = 0;
		public int FramesDropped { get; private set; } = 0;

		private readonly Encoding encoding;
		private readonly MemoryMappedViewAccessor memoryMappedViewAccessor;
		private readonly IDeserializer deserializer;

		public IRacingSdkData( MemoryMappedViewAccessor? memoryMappedViewAccessor )
		{
			if ( memoryMappedViewAccessor == null )
			{
				throw new Exception( "memoryMappedViewAccessor is null." );
			}

			this.memoryMappedViewAccessor = memoryMappedViewAccessor;

			Encoding.RegisterProvider( CodePagesEncodingProvider.Instance );

			encoding = Encoding.GetEncoding( 1252 );

			deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
		}

		public void Update()
		{
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

			if ( TelemetryData == null )
			{
				TelemetryData = new Dictionary<string, IRacingSdkDatum>( VarCount );

				var nameArray = new byte[ IRacingSdkDatum.MaxNameLength ];
				var descArray = new byte[ IRacingSdkDatum.MaxDescLength ];
				var unitArray = new byte[ IRacingSdkDatum.MaxUnitLength ];

				for ( var i = 0; i < VarCount; i++ )
				{
					var varOffset = i * IRacingSdkDatum.Size;

					var type = memoryMappedViewAccessor.ReadInt32( VarHeaderOffset + varOffset );
					var offset = memoryMappedViewAccessor.ReadInt32( VarHeaderOffset + varOffset + 4 );
					var count = memoryMappedViewAccessor.ReadInt32( VarHeaderOffset + varOffset + 8 );
					var name = ReadString( VarHeaderOffset + varOffset + 16, nameArray );
					var desc = ReadString( VarHeaderOffset + varOffset + 48, descArray );
					var unit = ReadString( VarHeaderOffset + varOffset + 112, unitArray );

					// iRacing SDK Bug - The header says irsdk_CarLeftRight is a bit field - it is not, it is a normal integer.

					if ( unit == "irsdk_CarLeftRight" )
					{
						type = (int) IRacingSdkEnum.VarType.Int;
					}

					// iRacing SDK Bug - The header says irsdk_PaceFlags is a normal integer - it is not, it is a bit field.

					else if ( unit == "irsdk_PaceFlags" )
					{
						type = (int) IRacingSdkEnum.VarType.BitField;
					}

					TelemetryData[ name ] = new IRacingSdkDatum( (IRacingSdkEnum.VarType) type, offset, count, name, desc, unit );
				}
			}
		}

		public void UpdateSessionInfo()
		{
			var bytes = new byte[ SessionInfoLength ];

			memoryMappedViewAccessor.ReadArray( SessionInfoOffset, bytes, 0, SessionInfoLength );

			SessionInfoYaml = FixInvalidYaml( bytes );

			var stringReader = new StringReader( SessionInfoYaml );

			SessionInfo = deserializer.Deserialize<IRacingSdkSessionInfo>( stringReader );
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public char GetChar( string name, int index )
		{
			Validate( name, index, IRacingSdkEnum.VarType.Char );

#pragma warning disable CS8602
			return memoryMappedViewAccessor.ReadChar( Offset + TelemetryData[ name ].Offset + index );
#pragma warning restore CS8602
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public bool GetBool( string name, int index )
		{
			Validate( name, index, IRacingSdkEnum.VarType.Bool );

#pragma warning disable CS8602
			return memoryMappedViewAccessor.ReadBoolean( Offset + TelemetryData[ name ].Offset + index );
#pragma warning restore CS8602
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetInt( string name, int index = 0 )
		{
			Validate( name, index, IRacingSdkEnum.VarType.Int );

#pragma warning disable CS8602
			return memoryMappedViewAccessor.ReadInt32( Offset + TelemetryData[ name ].Offset + index * 4 );
#pragma warning restore CS8602
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public uint GetBitField( string name, int index = 0 )
		{
			Validate( name, index, IRacingSdkEnum.VarType.BitField );

#pragma warning disable CS8602
			return memoryMappedViewAccessor.ReadUInt32( Offset + TelemetryData[ name ].Offset + index * 4 );
#pragma warning restore CS8602
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public float GetFloat( string name, int index = 0 )
		{

			Validate( name, index, IRacingSdkEnum.VarType.Float );

#pragma warning disable CS8602
			return memoryMappedViewAccessor.ReadSingle( Offset + TelemetryData[ name ].Offset + index * 4 );
#pragma warning restore CS8602
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public double GetDouble( string name, int index = 0 )
		{
			Validate( name, index, IRacingSdkEnum.VarType.Double );

#pragma warning disable CS8602
			return memoryMappedViewAccessor.ReadDouble( Offset + TelemetryData[ name ].Offset + index * 4 );
#pragma warning restore CS8602
		}

		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public object GetValue( string name, int index = 0 )
		{
			Validate( name, index, null );

#pragma warning disable CS8602
			var iRacingSdkDatum = TelemetryData[ name ];
#pragma warning restore CS8602

			return iRacingSdkDatum.VarType switch
			{
				IRacingSdkEnum.VarType.Char => memoryMappedViewAccessor.ReadChar( Offset + iRacingSdkDatum.Offset + index ),
				IRacingSdkEnum.VarType.Bool => memoryMappedViewAccessor.ReadBoolean( Offset + iRacingSdkDatum.Offset + index ),
				IRacingSdkEnum.VarType.Int => memoryMappedViewAccessor.ReadInt32( Offset + iRacingSdkDatum.Offset + index * 4 ),
				IRacingSdkEnum.VarType.BitField => memoryMappedViewAccessor.ReadUInt32( Offset + iRacingSdkDatum.Offset + index * 4 ),
				IRacingSdkEnum.VarType.Float => memoryMappedViewAccessor.ReadSingle( Offset + iRacingSdkDatum.Offset + index * 4 ),
				IRacingSdkEnum.VarType.Double => memoryMappedViewAccessor.ReadDouble( Offset + iRacingSdkDatum.Offset + index * 4 ),
				_ => throw new Exception( "Unexpected type!" ),
			};
		}

		[Conditional( "DEBUG" )]
		private void Validate( string name, int index, IRacingSdkEnum.VarType? type )
		{
			if ( TelemetryData == null )
			{
				throw new Exception( $"{name}, {index}: TelemetryData == null!" );
			}

			var iRacingSdkDatum = TelemetryData[ name ];

			if ( index >= iRacingSdkDatum.Count )
			{
				throw new Exception( $"{name}, {index}: index >= iRacingSdkDatum.count" );
			}

			if ( ( type != null ) && ( iRacingSdkDatum.VarType != type ) )
			{
				throw new Exception( $"{name}, {index}: iRacingSdkDatum.VarType != {type}" );
			}
		}

		private string ReadString( int offset, byte[] buffer )
		{
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
