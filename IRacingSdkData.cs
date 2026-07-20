
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
	/// <summary>
	/// Represents the current iRacing shared memory snapshot and typed accessors for its values.
	/// </summary>
	public class IRacingSdkData
	{
		public bool TelemetryDataPropertiesReady { get; private set; } = false;
		public readonly Dictionary<string, IRacingSdkDatum> TelemetryDataProperties = new();
		public string SessionInfoYaml { get; private set; } = string.Empty;
		public IRacingSdkSessionInfo SessionInfo { get; private set; } = null;

		public int Version => dataSource?.ReadInt32( 0 ) ?? 0;
		public int Status => dataSource?.ReadInt32( 4 ) ?? 0;
		public int TickRate => dataSource?.ReadInt32( 8 ) ?? 0;
		public int SessionInfoUpdate => dataSource?.ReadInt32( 12 ) ?? 0;
		public int SessionInfoLength => dataSource?.ReadInt32( 16 ) ?? 0;
		public int SessionInfoOffset => dataSource?.ReadInt32( 20 ) ?? 0;
		public int VarCount => dataSource?.ReadInt32( 24 ) ?? 0;
		public int VarHeaderOffset => dataSource?.ReadInt32( 28 ) ?? 0;
		public int BufferCount => dataSource?.ReadInt32( 32 ) ?? 0;
		public int BufferLength => dataSource?.ReadInt32( 36 ) ?? 0;

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

		private IRacingSdkDataSource dataSource = null;

		/// <summary>
		/// Initializes a new instance of the <see cref="IRacingSdkData"/> class.
		/// </summary>
		/// <param name="throwYamlExceptions"><see langword="true"/> to rethrow YAML parsing failures instead of scheduling a retry.</param>
		/// <param name="ignoreUnmatchedYamlProperties"><see langword="true"/> to ignore unmatched properties during YAML deserialization; otherwise unmatched properties will cause parsing failures.</param>
		public IRacingSdkData( bool throwYamlExceptions, bool ignoreUnmatchedYamlProperties )
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

			if ( ignoreUnmatchedYamlProperties )
			{
				deserializerBuilder.IgnoreUnmatchedProperties();
			}

			deserializer = deserializerBuilder.Build();
		}

		/// <summary>
		/// Sets the data source used to read iRacing-formatted data.
		/// </summary>
		/// <param name="dataSource">The data source to read from.</param>
		public void SetDataSource( IRacingSdkDataSource dataSource )
		{
			this.dataSource = dataSource;
		}

		/// <summary>
		/// Sets the memory-mapped view accessor used to read simulator data.
		/// </summary>
		/// <param name="memoryMappedViewAccessor">The view accessor for the iRacing shared memory block.</param>
		[Obsolete( "Use SetDataSource instead." )]
		public void SetMemoryMappedViewAccessor( MemoryMappedViewAccessor memoryMappedViewAccessor )
		{
			dataSource = new IRacingSdkMemoryMappedDataSource( memoryMappedViewAccessor );
		}

		/// <summary>
		/// Clears cached telemetry metadata and parsed session information.
		/// </summary>
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

		/// <summary>
		/// Refreshes header information and telemetry variable descriptors from shared memory.
		/// </summary>
		public void Update()
		{
			Debug.Assert( dataSource != null );

			var lastTickCount = TickCount;

			TickCount = -1;
			Offset = 0;

			for ( var i = 0; i < BufferCount; i++ )
			{
				var tickCount = dataSource.ReadInt32( 48 + ( i * 16 ) );

				if ( tickCount > TickCount )
				{
					TickCount = tickCount;
					Offset = dataSource.ReadInt32( 48 + ( i * 16 ) + 4 );
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

					var type = dataSource.ReadInt32( VarHeaderOffset + varOffset );
					var offset = dataSource.ReadInt32( VarHeaderOffset + varOffset + 4 );
					var count = dataSource.ReadInt32( VarHeaderOffset + varOffset + 8 );
					var countAsTime = dataSource.ReadBoolean( VarHeaderOffset + varOffset + 12 );
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
					}

					switch ( unit )
					{
						case "irsdk_CarLeftRight": type = (int) IRacingSdkEnum.VarType.Int; break;
						case "irsdk_PaceFlags": type = (int) IRacingSdkEnum.VarType.BitField; break;
					}

					#endregion

					TelemetryDataProperties[ name ] = new IRacingSdkDatum( (IRacingSdkEnum.VarType) type, offset, count, countAsTime, name, desc, unit );
				}

				TelemetryDataPropertiesReady = true;
			}
		}

		/// <summary>
		/// Reads and parses the latest session info YAML payload.
		/// </summary>
		/// <returns><see langword="true"/> if a new payload was parsed successfully; otherwise, <see langword="false"/>.</returns>
		public bool UpdateSessionInfo()
		{
			Debug.Assert( dataSource != null );

			var sessionInfoLength = SessionInfoLength;

			if ( sessionInfoLength > 0 )
			{
				var bytes = new byte[ sessionInfoLength ];

				dataSource.ReadArray( SessionInfoOffset, bytes, 0, sessionInfoLength );

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

		/// <summary>
		/// Reads a <see cref="char"/> telemetry value by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="char"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public char GetChar( string name, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index, IRacingSdkEnum.VarType.Char );

			return dataSource.ReadChar( Offset + TelemetryDataProperties[ name ].Offset + index );
		}

		/// <summary>
		/// Reads a <see cref="char"/> telemetry value using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="char"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public char GetChar( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Char );

			return dataSource.ReadChar( Offset + datum.Offset + index );
		}

		/// <summary>
		/// Reads multiple <see cref="char"/> telemetry values into an array.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetCharArray( string name, char[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Char );

			return dataSource.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		/// <summary>
		/// Reads a <see cref="bool"/> telemetry value by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="bool"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public bool GetBool( string name, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index, IRacingSdkEnum.VarType.Bool );

			return dataSource.ReadBoolean( Offset + TelemetryDataProperties[ name ].Offset + index );
		}

		/// <summary>
		/// Reads a <see cref="bool"/> telemetry value using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="bool"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public bool GetBool( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Bool );

			return dataSource.ReadBoolean( Offset + datum.Offset + index );
		}

		/// <summary>
		/// Reads multiple <see cref="bool"/> telemetry values into an array by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetBoolArray( string name, bool[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Bool );

			return dataSource.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		/// <summary>
		/// Reads multiple <see cref="bool"/> telemetry values into an array using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetBoolArray( IRacingSdkDatum datum, bool[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.Bool );

			return dataSource.ReadArray( Offset + datum.Offset, array, index, count );
		}

		/// <summary>
		/// Reads an <see cref="int"/> telemetry value by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="int"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetInt( string name, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index, IRacingSdkEnum.VarType.Int );

			return dataSource.ReadInt32( Offset + TelemetryDataProperties[ name ].Offset + index * 4 );
		}

		/// <summary>
		/// Reads an <see cref="int"/> telemetry value using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="int"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetInt( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Int );

			return dataSource.ReadInt32( Offset + datum.Offset + index * 4 );
		}

		/// <summary>
		/// Reads multiple <see cref="int"/> telemetry values into an array by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetIntArray( string name, int[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Int );

			return dataSource.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		/// <summary>
		/// Reads multiple <see cref="int"/> telemetry values into an array using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetIntArray( IRacingSdkDatum datum, int[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.Int );

			return dataSource.ReadArray( Offset + datum.Offset, array, index, count );
		}

		/// <summary>
		/// Reads an unsigned bit field telemetry value by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested bit field value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public uint GetBitField( string name, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index, IRacingSdkEnum.VarType.BitField );

			return dataSource.ReadUInt32( Offset + TelemetryDataProperties[ name ].Offset + index * 4 );
		}

		/// <summary>
		/// Reads an unsigned bit field telemetry value using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested bit field value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public uint GetBitField( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index, IRacingSdkEnum.VarType.BitField );

			return dataSource.ReadUInt32( Offset + datum.Offset + index * 4 );
		}

		/// <summary>
		/// Reads multiple bit field telemetry values into an array by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetBitFieldArray( string name, uint[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.BitField );

			return dataSource.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		/// <summary>
		/// Reads multiple bit field telemetry values into an array using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetBitFieldArray( IRacingSdkDatum datum, uint[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.BitField );

			return dataSource.ReadArray( Offset + datum.Offset, array, index, count );
		}

		/// <summary>
		/// Reads a <see cref="float"/> telemetry value by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="float"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public float GetFloat( string name, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index, IRacingSdkEnum.VarType.Float );

			return dataSource.ReadSingle( Offset + TelemetryDataProperties[ name ].Offset + index * 4 );
		}

		/// <summary>
		/// Reads a <see cref="float"/> telemetry value using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="float"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public float GetFloat( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Float );

			return dataSource.ReadSingle( Offset + datum.Offset + index * 4 );
		}

		/// <summary>
		/// Reads multiple <see cref="float"/> telemetry values into an array by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetFloatArray( string name, float[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Float );

			return dataSource.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		/// <summary>
		/// Reads multiple <see cref="float"/> telemetry values into an array using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetFloatArray( IRacingSdkDatum datum, float[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.Float );

			return dataSource.ReadArray( Offset + datum.Offset, array, index, count );
		}

		/// <summary>
		/// Reads a <see cref="double"/> telemetry value by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="double"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public double GetDouble( string name, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index, IRacingSdkEnum.VarType.Double );

			return dataSource.ReadDouble( Offset + TelemetryDataProperties[ name ].Offset + index * 8 );
		}

		/// <summary>
		/// Reads a <see cref="double"/> telemetry value using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested <see cref="double"/> value.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public double GetDouble( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index, IRacingSdkEnum.VarType.Double );

			return dataSource.ReadDouble( Offset + datum.Offset + index * 8 );
		}

		/// <summary>
		/// Reads multiple <see cref="double"/> telemetry values into an array by variable name.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetDoubleArray( string name, double[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index + count - 1, IRacingSdkEnum.VarType.Double );

			return dataSource.ReadArray( Offset + TelemetryDataProperties[ name ].Offset, array, index, count );
		}

		/// <summary>
		/// Reads multiple <see cref="double"/> telemetry values into an array using a cached datum definition.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public int GetDoubleArray( IRacingSdkDatum datum, double[] array, int index, int count )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index + count - 1, IRacingSdkEnum.VarType.Double );

			return dataSource.ReadArray( Offset + datum.Offset, array, index, count );
		}

		/// <summary>
		/// Reads a telemetry value by variable name and returns it as its runtime type.
		/// </summary>
		/// <param name="name">The telemetry variable name.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested value boxed as its native telemetry type.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public object GetValue( string name, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( name, index, null );

			var iRacingSdkDatum = TelemetryDataProperties[ name ];

			switch ( iRacingSdkDatum.VarType )
			{
				case IRacingSdkEnum.VarType.Char: return dataSource.ReadChar( Offset + iRacingSdkDatum.Offset + index );
				case IRacingSdkEnum.VarType.Bool: return dataSource.ReadBoolean( Offset + iRacingSdkDatum.Offset + index );
				case IRacingSdkEnum.VarType.Int: return dataSource.ReadInt32( Offset + iRacingSdkDatum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.BitField: return dataSource.ReadUInt32( Offset + iRacingSdkDatum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.Float: return dataSource.ReadSingle( Offset + iRacingSdkDatum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.Double: return dataSource.ReadDouble( Offset + iRacingSdkDatum.Offset + index * 4 );
				default: throw new Exception( "Unexpected type!" );
			}
		}

		/// <summary>
		/// Reads a telemetry value using a cached datum definition and returns it as its runtime type.
		/// </summary>
		/// <param name="datum">The telemetry datum to read.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		/// <returns>The requested value boxed as its native telemetry type.</returns>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public object GetValue( IRacingSdkDatum datum, int index = 0 )
		{
			Debug.Assert( dataSource != null );

			Validate( datum, index, null );

			switch ( datum.VarType )
			{
				case IRacingSdkEnum.VarType.Char: return dataSource.ReadChar( Offset + datum.Offset + index );
				case IRacingSdkEnum.VarType.Bool: return dataSource.ReadBoolean( Offset + datum.Offset + index );
				case IRacingSdkEnum.VarType.Int: return dataSource.ReadInt32( Offset + datum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.BitField: return dataSource.ReadUInt32( Offset + datum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.Float: return dataSource.ReadSingle( Offset + datum.Offset + index * 4 );
				case IRacingSdkEnum.VarType.Double: return dataSource.ReadDouble( Offset + datum.Offset + index * 4 );
				default: throw new Exception( "Unexpected type!" );
			}
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
			Debug.Assert( dataSource != null );

			dataSource.ReadArray( offset, buffer, 0, buffer.Length );

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

		private string FixInvalidYaml( byte[] yaml )
		{
			const int MaxNumDrivers = 64;
			const int MaxNumAdditionalBytesPerFixedKey = 2;

			// iRacing declares the session info encoding as ISO-8859-1 (WeekendInfo.Encoding) - decoding it as UTF-8 would turn extended characters into U+FFFD
			var yamlText = encoding.GetString( yaml );

			var keysToFix = new string[]
			{
				"AbbrevName:", "TeamName:", "UserName:", "Initials:", "DriverSetupName:", "CarDesignStr:"
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

			var stringBuilder = new StringBuilder( yamlText.Length + keysToFix.Length * MaxNumAdditionalBytesPerFixedKey * MaxNumDrivers );

			foreach ( var ch in yamlText )
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
								keyTracker.addFirstQuote = false;

								if ( ( ch == '\'' ) || ( ch == '"' ) )
								{
									keyTracker.ignoreUntilNextLine = true;

									keyTrackersIgnoringUntilNextLine++;
								}
								else
								{
									stringBuilder.Append( '\'' );

									keyTracker.addSecondQuote = true;
								}
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
