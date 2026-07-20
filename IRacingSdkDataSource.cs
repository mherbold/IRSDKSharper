
using System;

namespace IRSDKSharper
{
	/// <summary>
	/// Abstracts the source of iRacing-formatted data consumed by <see cref="IRacingSdk"/> and <see cref="IRacingSdkData"/>.
	/// <para>
	/// The data exposed through this class always uses the iRacing shared memory layout - a header at offset zero,
	/// followed by variable headers, session info YAML, and one or more telemetry buffers. The default implementation,
	/// <see cref="IRacingSdkMemoryMappedDataSource"/>, reads the live iRacing simulator's shared memory. Alternative
	/// implementations such as <see cref="IRacingSdkInMemoryDataSource"/> can supply iRacing-formatted data generated
	/// from other sources (for example, telemetry translated from other racing games).
	/// </para>
	/// </summary>
	public abstract class IRacingSdkDataSource : IDisposable
	{
		/// <summary>
		/// Attempts to open the underlying data provider.
		/// <para>
		/// This is called repeatedly (with a delay between attempts) by the <see cref="IRacingSdk"/> connection loop,
		/// so implementations may make partial progress across calls. Once this returns <see langword="true"/> the
		/// read methods must be usable.
		/// </para>
		/// </summary>
		/// <returns><see langword="true"/> when the data source is ready to be read from; otherwise, <see langword="false"/>.</returns>
		public abstract bool TryOpen();

		/// <summary>
		/// Closes the underlying data provider and releases its resources. The data source must be re-openable via
		/// <see cref="TryOpen"/> after being closed.
		/// </summary>
		public abstract void Close();

		/// <summary>
		/// Blocks until the next data-valid signal arrives, or the timeout elapses.
		/// </summary>
		/// <param name="timeoutInMS">The maximum time to wait, in milliseconds.</param>
		/// <returns><see langword="true"/> if a signal was received; <see langword="false"/> if the wait timed out.</returns>
		public abstract bool WaitForDataReady( int timeoutInMS );

		/// <summary>
		/// Reads a signed 32-bit integer at the given byte offset.
		/// </summary>
		/// <param name="offset">The byte offset within the data block.</param>
		/// <returns>The value read.</returns>
		public abstract int ReadInt32( int offset );

		/// <summary>
		/// Reads an unsigned 32-bit integer at the given byte offset.
		/// </summary>
		/// <param name="offset">The byte offset within the data block.</param>
		/// <returns>The value read.</returns>
		public abstract uint ReadUInt32( int offset );

		/// <summary>
		/// Reads a 32-bit floating point value at the given byte offset.
		/// </summary>
		/// <param name="offset">The byte offset within the data block.</param>
		/// <returns>The value read.</returns>
		public abstract float ReadSingle( int offset );

		/// <summary>
		/// Reads a 64-bit floating point value at the given byte offset.
		/// </summary>
		/// <param name="offset">The byte offset within the data block.</param>
		/// <returns>The value read.</returns>
		public abstract double ReadDouble( int offset );

		/// <summary>
		/// Reads a boolean value at the given byte offset.
		/// </summary>
		/// <param name="offset">The byte offset within the data block.</param>
		/// <returns>The value read.</returns>
		public abstract bool ReadBoolean( int offset );

		/// <summary>
		/// Reads a character value at the given byte offset.
		/// </summary>
		/// <param name="offset">The byte offset within the data block.</param>
		/// <returns>The value read.</returns>
		public abstract char ReadChar( int offset );

		/// <summary>
		/// Reads multiple values into an array, starting at the given byte offset.
		/// </summary>
		/// <typeparam name="T">The element type to read.</typeparam>
		/// <param name="offset">The byte offset within the data block.</param>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The destination array index.</param>
		/// <param name="count">The number of elements to read.</param>
		/// <returns>The number of elements copied into <paramref name="array"/>.</returns>
		public abstract int ReadArray<T>( int offset, T[] array, int index, int count ) where T : struct;

		/// <summary>
		/// Closes the data source and suppresses finalization.
		/// </summary>
		public void Dispose()
		{
			Close();

			GC.SuppressFinalize( this );
		}
	}
}
