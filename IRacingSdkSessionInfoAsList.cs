
using System;
using System.Collections;

namespace IRSDKSharper
{
	/// <summary>
	/// Exposes parsed session info values as a flat <see cref="IList"/>.
	/// </summary>
	public class IRacingSdkSessionInfoAsList : IList
	{
		private readonly IRacingSdkData data;

		private int count;

		private int lastIndex;
		private Datum lastDatum;

		/// <summary>
		/// Initializes a new instance of the <see cref="IRacingSdkSessionInfoAsList"/> class.
		/// </summary>
		/// <param name="data">The data source to expose.</param>
		public IRacingSdkSessionInfoAsList( IRacingSdkData data )
		{
			this.data = data;

			Reset();
		}

		/// <summary>
		/// Clears cached indexing state so the list reflects the latest session info snapshot.
		/// </summary>
		public void Reset()
		{
			count = -1;
			lastIndex = -1;
		}

		public object this[ int index ]
		{
			get
			{
				if ( index != lastIndex )
				{
					Datum datum = new Datum( "???", "???" );

					var sessionInfo = data.SessionInfo;

					if ( sessionInfo != null )
					{
						var currentOffset = 0;

						foreach ( var propertyInfo in sessionInfo.GetType().GetProperties() )
						{
							if ( GetDatum( ref currentOffset, index, propertyInfo.Name, propertyInfo.GetValue( sessionInfo ), out datum ) )
							{
								break;
							}
						}
					}

					lastIndex = index;
					lastDatum = datum;
				}

				return lastDatum;
			}

			set => throw new NotImplementedException();
		}

		private bool GetDatum( ref int currentOffset, int targetOffset, string trackName, object valueAsObject, out Datum datum )
		{
			if ( valueAsObject != null )
			{
				var isSimpleValue = ( ( valueAsObject is string ) || ( valueAsObject is int ) || ( valueAsObject is float ) || ( valueAsObject is double ) );

				if ( isSimpleValue )
				{
					if ( currentOffset == targetOffset )
					{
						datum = new Datum( trackName, valueAsObject.ToString() );

						return true;
					}

					currentOffset++;
				}
				else if ( valueAsObject is IList list )
				{
					var index = 0;

					foreach ( var item in list )
					{
						if ( GetDatum( ref currentOffset, targetOffset, $"{trackName}[{index}]", item, out datum ) )
						{
							return true;
						}

						index++;
					}
				}
				else
				{
					foreach ( var propertyInfo in valueAsObject.GetType().GetProperties() )
					{
						if ( GetDatum( ref currentOffset, targetOffset, $"{trackName}.{propertyInfo.Name}", propertyInfo.GetValue( valueAsObject ), out datum ) )
						{
							return true;
						}
					}
				}
			}

			datum = new Datum( "???", "???" );

			return false;
		}

		public bool IsFixedSize => true;

		public bool IsReadOnly => true;

		public int Count
		{
			get
			{
				if ( count == -1 )
				{
					count = 0;

					var sessionInfo = data.SessionInfo;

					if ( sessionInfo != null )
					{
						foreach ( var propertyInfo in sessionInfo.GetType().GetProperties() )
						{
							count += CountSessionInfoProperties( propertyInfo.GetValue( sessionInfo ) );
						}
					}
				}

				return count;
			}

			set => throw new NotImplementedException();
		}

		private int CountSessionInfoProperties( object valueAsObject )
		{
			var count = 0;

			if ( valueAsObject != null )
			{
				var isSimpleValue = ( ( valueAsObject is string ) || ( valueAsObject is int ) || ( valueAsObject is float ) || ( valueAsObject is double ) );

				if ( isSimpleValue )
				{
					count++;
				}
				else if ( valueAsObject is IList list )
				{
					foreach ( var item in list )
					{
						count += CountSessionInfoProperties( item );
					}
				}
				else
				{
					foreach ( var propertyInfo in valueAsObject.GetType().GetProperties() )
					{
						count += CountSessionInfoProperties( propertyInfo.GetValue( valueAsObject ) );
					}
				}
			}

			return count;
		}

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
		/// Represents a single flattened session info key/value pair.
		/// </summary>
		public class Datum
		{
			public string key;
			public string value;

			/// <summary>
			/// Initializes a new instance of the <see cref="Datum"/> class.
			/// </summary>
			/// <param name="key">The session info field path.</param>
			/// <param name="value">The formatted field value.</param>
			public Datum( string key, string value )
			{
				this.key = key;
				this.value = value;
			}
		}
	}
}
