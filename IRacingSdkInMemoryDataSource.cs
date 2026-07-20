
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace IRSDKSharper
{
	/// <summary>
	/// An <see cref="IRacingSdkDataSource"/> that is fed iRacing-formatted data generated in-process instead of read
	/// from the live simulator. This is the foundation for bridging telemetry from other racing games into the
	/// iRacing data model - a bridge declares its telemetry variables, then writes values and session info YAML,
	/// and consumers of <see cref="IRacingSdk"/> see a normal iRacing-shaped data feed.
	/// <para>
	/// Usage - all writer methods must be called from a single thread:
	/// <code>
	/// var dataSource = new IRacingSdkInMemoryDataSource();
	///
	/// var speedDatum = dataSource.AddVar( "Speed", IRacingSdkEnum.VarType.Float, 1, "m/s", "GPS vehicle speed" );
	///
	/// dataSource.Initialize();
	///
	/// dataSource.SetSessionInfo( sessionInfoYaml );
	///
	/// // then, at every simulated tick:
	/// dataSource.SetFloat( speedDatum, speed );
	/// dataSource.CommitFrame();
	/// </code>
	/// </para>
	/// </summary>
	public class IRacingSdkInMemoryDataSource : IRacingSdkDataSource
	{
		private const int HeaderVersion = 2;
		private const int ConnectedStatus = 1;
		private const int HeaderLength = 112;
		private const int BufferRecordsOffset = 48;
		private const int BufferRecordLength = 16;

		/// <summary>
		/// Gets the tick rate, in frames per second, declared in the header.
		/// </summary>
		public int TickRate { get; }

		/// <summary>
		/// Gets the tick count of the most recently committed frame.
		/// </summary>
		public int TickCount { get; private set; } = 0;

		/// <summary>
		/// Gets a value indicating whether <see cref="Initialize"/> has been called.
		/// </summary>
		public bool IsInitialized => initialized;

		private readonly int maxSessionInfoLength;
		private readonly int bufferCount;

		private readonly Encoding encoding;

		private IRacingSdkDatum[] varHeaders = new IRacingSdkDatum[ 64 ];
		private int varCount = 0;
		private int bufferLength = 0;

		private int sessionInfoOffset = 0;
		private int buffersOffset = 0;
		private int sessionInfoUpdate = 0;

		private byte[] data = null;
		private byte[] stagingBuffer = null;

		private volatile bool initialized = false;

		private readonly AutoResetEvent dataReadyEvent = new AutoResetEvent( false );

		[StructLayout( LayoutKind.Explicit )]
		private struct FloatIntUnion
		{
			[FieldOffset( 0 )] public float floatValue;
			[FieldOffset( 0 )] public int intValue;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IRacingSdkInMemoryDataSource"/> class.
		/// </summary>
		/// <param name="tickRate">The simulated tick rate, in frames per second.</param>
		/// <param name="maxSessionInfoLength">The maximum size, in bytes, of the session info YAML region.</param>
		/// <param name="bufferCount">The number of rotating telemetry buffers (2 to 4).</param>
		public IRacingSdkInMemoryDataSource( int tickRate = 60, int maxSessionInfoLength = 512 * 1024, int bufferCount = 3 )
		{
			if ( ( bufferCount < 2 ) || ( bufferCount > 4 ) )
			{
				throw new ArgumentOutOfRangeException( nameof( bufferCount ) );
			}

			TickRate = tickRate;

			this.maxSessionInfoLength = maxSessionInfoLength;
			this.bufferCount = bufferCount;

#if !NET471 && !NET_UNITY_4_8

			Encoding.RegisterProvider( CodePagesEncodingProvider.Instance );

#endif

			encoding = Encoding.GetEncoding( 1252 );
		}

		#region writer API - variable declaration

		/// <summary>
		/// Declares a telemetry variable. All variables must be declared before <see cref="Initialize"/> is called.
		/// </summary>
		/// <param name="name">The telemetry variable name (max 32 characters).</param>
		/// <param name="varType">The value type of the telemetry variable.</param>
		/// <param name="count">The number of values (1 for scalars, more for arrays).</param>
		/// <param name="unit">The unit string (max 32 characters).</param>
		/// <param name="desc">The description string (max 64 characters).</param>
		/// <param name="countAsTime">Indicates whether the count should be interpreted as a time value (used for _ST sub-tick sample arrays).</param>
		/// <returns>The datum describing the declared variable - pass this to the value setter methods.</returns>
		public IRacingSdkDatum AddVar( string name, IRacingSdkEnum.VarType varType, int count, string unit, string desc, bool countAsTime = false )
		{
			if ( initialized )
			{
				throw new InvalidOperationException( "Variables cannot be added after the data source has been initialized." );
			}

			if ( count < 1 )
			{
				throw new ArgumentOutOfRangeException( nameof( count ) );
			}

			var datum = new IRacingSdkDatum( varType, bufferLength, count, countAsTime, name, desc, unit );

			if ( varCount == varHeaders.Length )
			{
				Array.Resize( ref varHeaders, varHeaders.Length * 2 );
			}

			varHeaders[ varCount ] = datum;

			varCount++;

			bufferLength += IRacingSdkConst.VarTypeBytes[ (int) varType ] * count;

			return datum;
		}

		/// <summary>
		/// Computes the memory layout and writes the header and variable headers. After this call the data source
		/// reports itself as ready to be opened, and telemetry values can be written.
		/// </summary>
		public void Initialize()
		{
			if ( initialized )
			{
				throw new InvalidOperationException( "The data source has already been initialized." );
			}

			var varHeadersOffset = HeaderLength;
			var varHeadersLength = varCount * IRacingSdkDatum.Size;

			sessionInfoOffset = varHeadersOffset + varHeadersLength;
			buffersOffset = sessionInfoOffset + maxSessionInfoLength;

			data = new byte[ buffersOffset + bufferLength * bufferCount + 8 /* padding so ReadChar near the end cannot overflow */ ];
			stagingBuffer = new byte[ bufferLength ];

			WriteInt32( data, 0, HeaderVersion );
			WriteInt32( data, 4, ConnectedStatus );
			WriteInt32( data, 8, TickRate );
			WriteInt32( data, 12, 0 ); // session info update
			WriteInt32( data, 16, 0 ); // session info length
			WriteInt32( data, 20, sessionInfoOffset );
			WriteInt32( data, 24, varCount );
			WriteInt32( data, 28, varHeadersOffset );
			WriteInt32( data, 32, bufferCount );
			WriteInt32( data, 36, bufferLength );

			for ( var i = 0; i < bufferCount; i++ )
			{
				WriteInt32( data, BufferRecordsOffset + i * BufferRecordLength, 0 );
				WriteInt32( data, BufferRecordsOffset + i * BufferRecordLength + 4, buffersOffset + i * bufferLength );
			}

			for ( var i = 0; i < varCount; i++ )
			{
				var datum = varHeaders[ i ];
				var varHeaderOffset = varHeadersOffset + i * IRacingSdkDatum.Size;

				WriteInt32( data, varHeaderOffset, (int) datum.VarType );
				WriteInt32( data, varHeaderOffset + 4, datum.Offset );
				WriteInt32( data, varHeaderOffset + 8, datum.Count );

				data[ varHeaderOffset + 12 ] = datum.CountAsTime ? (byte) 1 : (byte) 0;

				WriteString( data, varHeaderOffset + 16, IRacingSdkDatum.MaxNameLength, datum.Name );
				WriteString( data, varHeaderOffset + 48, IRacingSdkDatum.MaxDescLength, datum.Desc );
				WriteString( data, varHeaderOffset + 112, IRacingSdkDatum.MaxUnitLength, datum.Unit );
			}

			initialized = true;
		}

		#endregion

		#region writer API - session info and telemetry values

		/// <summary>
		/// Replaces the session info YAML payload and marks it as updated so consumers will re-parse it.
		/// </summary>
		/// <param name="sessionInfoYaml">The new session info YAML document.</param>
		public void SetSessionInfo( string sessionInfoYaml )
		{
			ThrowIfNotInitialized();

			var bytes = encoding.GetBytes( sessionInfoYaml );

			if ( bytes.Length > maxSessionInfoLength )
			{
				throw new ArgumentException( $"The session info YAML is {bytes.Length} bytes but the maximum is {maxSessionInfoLength} bytes." );
			}

			Buffer.BlockCopy( bytes, 0, data, sessionInfoOffset, bytes.Length );

			WriteInt32( data, 16, bytes.Length );

			sessionInfoUpdate++;

			WriteInt32( data, 12, sessionInfoUpdate );
		}

		/// <summary>
		/// Sets a boolean telemetry value in the staging frame.
		/// </summary>
		/// <param name="datum">The datum returned by <see cref="AddVar"/>.</param>
		/// <param name="value">The value to set.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		public void SetBool( IRacingSdkDatum datum, bool value, int index = 0 )
		{
			Debug.Assert( ( index >= 0 ) && ( index < datum.Count ) );

			stagingBuffer[ datum.Offset + index ] = value ? (byte) 1 : (byte) 0;
		}

		/// <summary>
		/// Sets a character telemetry value in the staging frame.
		/// </summary>
		/// <param name="datum">The datum returned by <see cref="AddVar"/>.</param>
		/// <param name="value">The value to set.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		public void SetChar( IRacingSdkDatum datum, char value, int index = 0 )
		{
			Debug.Assert( ( index >= 0 ) && ( index < datum.Count ) );

			stagingBuffer[ datum.Offset + index ] = (byte) value;
		}

		/// <summary>
		/// Sets a signed 32-bit integer telemetry value in the staging frame.
		/// </summary>
		/// <param name="datum">The datum returned by <see cref="AddVar"/>.</param>
		/// <param name="value">The value to set.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		public void SetInt( IRacingSdkDatum datum, int value, int index = 0 )
		{
			Debug.Assert( ( index >= 0 ) && ( index < datum.Count ) );

			WriteInt32( stagingBuffer, datum.Offset + index * 4, value );
		}

		/// <summary>
		/// Sets an unsigned bit field telemetry value in the staging frame.
		/// </summary>
		/// <param name="datum">The datum returned by <see cref="AddVar"/>.</param>
		/// <param name="value">The value to set.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		public void SetBitField( IRacingSdkDatum datum, uint value, int index = 0 )
		{
			Debug.Assert( ( index >= 0 ) && ( index < datum.Count ) );

			WriteInt32( stagingBuffer, datum.Offset + index * 4, (int) value );
		}

		/// <summary>
		/// Sets a 32-bit floating point telemetry value in the staging frame.
		/// </summary>
		/// <param name="datum">The datum returned by <see cref="AddVar"/>.</param>
		/// <param name="value">The value to set.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		public void SetFloat( IRacingSdkDatum datum, float value, int index = 0 )
		{
			Debug.Assert( ( index >= 0 ) && ( index < datum.Count ) );

			var union = new FloatIntUnion { floatValue = value };

			WriteInt32( stagingBuffer, datum.Offset + index * 4, union.intValue );
		}

		/// <summary>
		/// Sets a 64-bit floating point telemetry value in the staging frame.
		/// </summary>
		/// <param name="datum">The datum returned by <see cref="AddVar"/>.</param>
		/// <param name="value">The value to set.</param>
		/// <param name="index">The zero-based element index for array values.</param>
		public void SetDouble( IRacingSdkDatum datum, double value, int index = 0 )
		{
			Debug.Assert( ( index >= 0 ) && ( index < datum.Count ) );

			var bits = BitConverter.DoubleToInt64Bits( value );

			var offset = datum.Offset + index * 8;

			WriteInt32( stagingBuffer, offset, (int) bits );
			WriteInt32( stagingBuffer, offset + 4, (int) ( bits >> 32 ) );
		}

		/// <summary>
		/// Sets multiple 32-bit floating point telemetry values in the staging frame.
		/// </summary>
		/// <param name="datum">The datum returned by <see cref="AddVar"/>.</param>
		/// <param name="values">The values to set.</param>
		/// <param name="sourceIndex">The source array index to start copying from.</param>
		/// <param name="count">The number of values to copy.</param>
		public void SetFloatArray( IRacingSdkDatum datum, float[] values, int sourceIndex, int count )
		{
			Debug.Assert( count <= datum.Count );

			Buffer.BlockCopy( values, sourceIndex * 4, stagingBuffer, datum.Offset, count * 4 );
		}

		/// <summary>
		/// Sets multiple signed 32-bit integer telemetry values in the staging frame.
		/// </summary>
		/// <param name="datum">The datum returned by <see cref="AddVar"/>.</param>
		/// <param name="values">The values to set.</param>
		/// <param name="sourceIndex">The source array index to start copying from.</param>
		/// <param name="count">The number of values to copy.</param>
		public void SetIntArray( IRacingSdkDatum datum, int[] values, int sourceIndex, int count )
		{
			Debug.Assert( count <= datum.Count );

			Buffer.BlockCopy( values, sourceIndex * 4, stagingBuffer, datum.Offset, count * 4 );
		}

		/// <summary>
		/// Publishes the staging frame as the next telemetry frame and signals waiting consumers. Values not written
		/// since the last commit keep their previous values.
		/// </summary>
		public void CommitFrame()
		{
			ThrowIfNotInitialized();

			TickCount++;

			var bufferIndex = TickCount % bufferCount;

			Buffer.BlockCopy( stagingBuffer, 0, data, buffersOffset + bufferIndex * bufferLength, bufferLength );

			WriteInt32( data, BufferRecordsOffset + bufferIndex * BufferRecordLength, TickCount );

			dataReadyEvent.Set();
		}

		#endregion

		#region IRacingSdkDataSource implementation

		public override bool TryOpen()
		{
			return initialized;
		}

		public override void Close()
		{
			// the writer-side state is deliberately kept so that the data source can be re-opened after the SDK is
			// stopped and restarted - create a new instance instead to declare a different set of variables
			dataReadyEvent.Reset();
		}

		public override bool WaitForDataReady( int timeoutInMS )
		{
			return dataReadyEvent.WaitOne( timeoutInMS );
		}

		public override int ReadInt32( int offset )
		{
			return BitConverter.ToInt32( data, offset );
		}

		public override uint ReadUInt32( int offset )
		{
			return BitConverter.ToUInt32( data, offset );
		}

		public override float ReadSingle( int offset )
		{
			return BitConverter.ToSingle( data, offset );
		}

		public override double ReadDouble( int offset )
		{
			return BitConverter.ToDouble( data, offset );
		}

		public override bool ReadBoolean( int offset )
		{
			return data[ offset ] != 0;
		}

		public override char ReadChar( int offset )
		{
			return BitConverter.ToChar( data, offset );
		}

		public override int ReadArray<T>( int offset, T[] array, int index, int count )
		{
			// mirrors MemoryMappedViewAccessor.ReadArray element sizes - bool and byte are 1 byte, char is 2 bytes
			int elementSize;

			if ( ( typeof( T ) == typeof( bool ) ) || ( typeof( T ) == typeof( byte ) ) || ( typeof( T ) == typeof( sbyte ) ) )
			{
				elementSize = 1;
			}
			else if ( ( typeof( T ) == typeof( char ) ) || ( typeof( T ) == typeof( short ) ) || ( typeof( T ) == typeof( ushort ) ) )
			{
				elementSize = 2;
			}
			else if ( ( typeof( T ) == typeof( double ) ) || ( typeof( T ) == typeof( long ) ) || ( typeof( T ) == typeof( ulong ) ) )
			{
				elementSize = 8;
			}
			else
			{
				elementSize = 4;
			}

			Buffer.BlockCopy( data, offset, array, index * elementSize, count * elementSize );

			return count;
		}

		#endregion

		#region helper functions

		private void ThrowIfNotInitialized()
		{
			if ( !initialized )
			{
				throw new InvalidOperationException( "The data source has not been initialized." );
			}
		}

		private static void WriteInt32( byte[] buffer, int offset, int value )
		{
			buffer[ offset ] = (byte) value;
			buffer[ offset + 1 ] = (byte) ( value >> 8 );
			buffer[ offset + 2 ] = (byte) ( value >> 16 );
			buffer[ offset + 3 ] = (byte) ( value >> 24 );
		}

		private void WriteString( byte[] buffer, int offset, int maxLength, string value )
		{
			var bytes = encoding.GetBytes( value ?? string.Empty );

			var length = Math.Min( bytes.Length, maxLength );

			Buffer.BlockCopy( bytes, 0, buffer, offset, length );
		}

		#endregion
	}
}
