
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Win32.SafeHandles;

namespace IRSDKSharper
{
	/// <summary>
	/// The default <see cref="IRacingSdkDataSource"/> - reads the live iRacing simulator's shared memory and waits on
	/// the simulator's data valid event.
	/// </summary>
	public class IRacingSdkMemoryMappedDataSource : IRacingSdkDataSource
	{
		private const string SimulatorMemoryMappedFileName = "Local\\IRSDKMemMapFileName";
		private const string SimulatorDataValidEventName = "Local\\IRSDKDataValidEvent";

		private MemoryMappedFile memoryMappedFile = null;
		private MemoryMappedViewAccessor viewAccessor = null;
		private AutoResetEvent dataValidEvent = null;

		/// <summary>
		/// Initializes a new instance of the <see cref="IRacingSdkMemoryMappedDataSource"/> class.
		/// </summary>
		public IRacingSdkMemoryMappedDataSource()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IRacingSdkMemoryMappedDataSource"/> class using an
		/// externally-created view accessor. Only the read methods are usable on instances created this way -
		/// <see cref="WaitForDataReady"/> will always time out.
		/// </summary>
		/// <param name="viewAccessor">The view accessor for the iRacing shared memory block.</param>
		public IRacingSdkMemoryMappedDataSource( MemoryMappedViewAccessor viewAccessor )
		{
			this.viewAccessor = viewAccessor;
		}

		public override bool TryOpen()
		{
			if ( memoryMappedFile == null )
			{
				try
				{
					memoryMappedFile = MemoryMappedFile.OpenExisting( SimulatorMemoryMappedFileName );
				}
				catch ( FileNotFoundException )
				{
				}
			}

			if ( ( memoryMappedFile != null ) && ( viewAccessor == null ) )
			{
				viewAccessor = memoryMappedFile.CreateViewAccessor();

				if ( viewAccessor == null )
				{
					throw new Exception( "Failed to create memory mapped view accessor." );
				}
			}

			if ( ( viewAccessor != null ) && ( dataValidEvent == null ) )
			{
				var dataValidEventHandle = WinApi.OpenEvent( WinApi.EVENT_ALL_ACCESS, false, SimulatorDataValidEventName );

				if ( dataValidEventHandle == (IntPtr) null )
				{
					var errorCode = Marshal.GetLastWin32Error();

					if ( errorCode != WinApi.ERROR_FILE_NOT_FOUND )
					{
						Marshal.ThrowExceptionForHR( errorCode, IntPtr.Zero );
					}
				}
				else
				{
					dataValidEvent = new AutoResetEvent( false )
					{
						SafeWaitHandle = new SafeWaitHandle( dataValidEventHandle, true )
					};
				}
			}

			return dataValidEvent != null;
		}

		public override void Close()
		{
			dataValidEvent?.Dispose();
			viewAccessor?.Dispose();
			memoryMappedFile?.Dispose();

			dataValidEvent = null;
			viewAccessor = null;
			memoryMappedFile = null;
		}

		public override bool WaitForDataReady( int timeoutInMS )
		{
			return dataValidEvent?.WaitOne( timeoutInMS ) ?? false;
		}

		public override int ReadInt32( int offset )
		{
			return viewAccessor?.ReadInt32( offset ) ?? 0;
		}

		public override uint ReadUInt32( int offset )
		{
			return viewAccessor?.ReadUInt32( offset ) ?? 0;
		}

		public override float ReadSingle( int offset )
		{
			return viewAccessor?.ReadSingle( offset ) ?? 0.0f;
		}

		public override double ReadDouble( int offset )
		{
			return viewAccessor?.ReadDouble( offset ) ?? 0.0;
		}

		public override bool ReadBoolean( int offset )
		{
			return viewAccessor?.ReadBoolean( offset ) ?? false;
		}

		public override char ReadChar( int offset )
		{
			return viewAccessor?.ReadChar( offset ) ?? '\0';
		}

		public override int ReadArray<T>( int offset, T[] array, int index, int count )
		{
			return viewAccessor?.ReadArray( offset, array, index, count ) ?? 0;
		}
	}
}
