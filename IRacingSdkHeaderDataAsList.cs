
using System;
using System.Collections;

namespace IRSDKSharper
{
	/// <summary>
	/// Exposes the shared memory header values as a flat <see cref="IList"/>.
	/// </summary>
	public class IRacingSdkHeaderDataAsList : IList
	{
		private readonly IRacingSdkData data;

		/// <summary>
		/// Initializes a new instance of the <see cref="IRacingSdkHeaderDataAsList"/> class.
		/// </summary>
		/// <param name="data">The data source to expose.</param>
		public IRacingSdkHeaderDataAsList( IRacingSdkData data )
		{
			this.data = data;
		}

		public object this[ int index ]
		{
			get
			{
				switch ( index )
				{
					case 0: return new Datum( "Version", $"{data.Version}" );
					case 1: return new Datum( "Status", $"{data.Status}" );
					case 2: return new Datum( "TickRate", $"{data.TickRate}" );
					case 3: return new Datum( "SessionInfoUpdate", $"{data.SessionInfoUpdate}" );
					case 4: return new Datum( "SessionInfoLength", $"{data.SessionInfoLength}" );
					case 5: return new Datum( "SessionInfoOffset", $"{data.SessionInfoOffset}" );
					case 6: return new Datum( "VarCount", $"{data.VarCount}" );
					case 7: return new Datum( "VarHeaderOffset", $"{data.VarHeaderOffset}" );
					case 8: return new Datum( "BufferCount", $"{data.BufferCount}" );
					case 9: return new Datum( "BufferLength", $"{data.BufferLength}" );
					case 10: return new Datum( "TickCount", $"{data.TickCount}" );
					case 11: return new Datum( "Offset", $"{data.Offset}" );
					case 12: return new Datum( "FramesDropped", $"{data.FramesDropped}" );

					default: throw new IndexOutOfRangeException();
				}
			}

			set => throw new NotImplementedException();
		}

		public bool IsFixedSize => true;

		public bool IsReadOnly => true;

		public int Count => 13;

		public bool IsSynchronized => false;

		public object SyncRoot => throw new NotImplementedException();

		/// <summary>
		/// Adds an item to the list.
		/// </summary>
		/// <param name="value">The value to add.</param>
		/// <returns>The position into which the new element was inserted.</returns>
		public int Add( object value )
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Removes all items from the list.
		/// </summary>
		public void Clear()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Determines whether the list contains a specific value.
		/// </summary>
		/// <param name="value">The value to locate.</param>
		/// <returns><see langword="true"/> if the value is present; otherwise, <see langword="false"/>.</returns>
		public bool Contains( object value )
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Copies the list elements to an array.
		/// </summary>
		/// <param name="array">The destination array.</param>
		/// <param name="index">The zero-based array index at which copying begins.</param>
		public void CopyTo( Array array, int index )
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Returns an enumerator for the list.
		/// </summary>
		/// <returns>An enumerator for the current list.</returns>
		public IEnumerator GetEnumerator()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Returns the index of a value in the list.
		/// </summary>
		/// <param name="value">The value to locate.</param>
		/// <returns>The zero-based index of the value if found; otherwise, -1.</returns>
		public int IndexOf( object value )
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Inserts a value into the list.
		/// </summary>
		/// <param name="index">The zero-based insertion index.</param>
		/// <param name="value">The value to insert.</param>
		public void Insert( int index, object value )
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Removes the first occurrence of a value from the list.
		/// </summary>
		/// <param name="value">The value to remove.</param>
		public void Remove( object value )
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Removes the value at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index of the element to remove.</param>
		public void RemoveAt( int index )
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Represents a single header key/value pair.
		/// </summary>
		public class Datum
		{
			public string key;
			public string value;

			/// <summary>
			/// Initializes a new instance of the <see cref="Datum"/> class.
			/// </summary>
			/// <param name="key">The header field name.</param>
			/// <param name="value">The formatted header field value.</param>
			public Datum( string key, string value )
			{
				this.key = key;
				this.value = value;
			}
		}
	}
}
