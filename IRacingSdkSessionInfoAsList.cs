
using System;
using System.Collections;

namespace IRSDKSharper
{
	/// <summary>
	/// Represents a custom implementation of the <see cref="IList"/> interface that provides access to session information
	/// from the iRacing SDK data source in a list-like format.
	/// </summary>
	public class IRacingSdkSessionInfoAsList : IList
	{
		private readonly IRacingSdkData data;

		private int count;

		private int lastIndex;
		private Datum lastDatum;

		public IRacingSdkSessionInfoAsList( IRacingSdkData data )
		{
			this.data = data;

			Reset();
		}

		/// <summary>
		/// Resets the internal state of the `IRacingSdkSessionInfoAsList` class to its initial state.
		/// </summary>
		/// <remarks>
		/// This method is typically used to clear or reinitialize certain internal fields, such as resetting
		/// indexing or counters, to allow for a fresh state when interacting with the session information data.
		/// After calling this method, the object behaves as if it has no processed or loaded session data.
		/// </remarks>
		public void Reset()
		{
			count = -1;
			lastIndex = -1;
		}

		/// <summary>
		/// Provides indexed access to session information from the iRacing SDK.
		/// The index retrieves session properties dynamically and ensures caching
		/// for improved performance when accessing repeated data.
		/// </summary>
		/// <param name="index">The zero-based index of the session information item.</param>
		/// <returns>An object containing session information for the specified index.</returns>
		/// <exception cref="NotImplementedException">Thrown if a setter is used, as this implementation is read-only.</exception>
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

		/// <summary>
		/// Retrieves a specific data point from the session information based on a target offset and its hierarchical
		/// position within the data structure.
		/// </summary>
		/// <param name="currentOffset">
		/// Represents the current offset being traversed within the session information. This is updated as the method
		/// iterates through the data.
		/// </param>
		/// <param name="targetOffset">
		/// Specifies the target offset to locate the desired data point within the session information structure.
		/// </param>
		/// <param name="trackName">
		/// The name of the current data node or hierarchy being processed, such as the name of a property or array element
		/// in the session info.
		/// </param>
		/// <param name="valueAsObject">
		/// The value of the data point being processed, provided as an object. This could represent a scalar, list, or
		/// complex object.
		/// </param>
		/// <param name="datum">
		/// Output parameter that receives the located data point upon successful extraction. The datum consists of a key
		/// and value representing the data point.
		/// </param>
		/// <returns>
		/// Returns a boolean indicating whether the targeted data point was successfully located and retrieved.
		/// If true, the `datum` parameter will contain the extracted data.
		/// </returns>
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

		/// <summary>
		/// Recursively counts the total number of properties within the specified object or its nested objects.
		/// </summary>
		/// <remarks>
		/// This method is used to traverse a potentially complex object structure, identifying and counting
		/// all individual properties, including those in nested lists or sub-objects. It differentiates between
		/// simple value types (e.g., strings, integers) and complex objects or collections, recursively iterating
		/// through the properties of the latter.
		/// </remarks>
		/// <param name="valueAsObject">The object whose properties need to be counted. It can be a primitive type, a list, or a complex object.</param>
		/// <returns>The total count of properties in the input object and its nested structures.</returns>
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
		/// Represents a data element with a key-value pair structure, used to store and access specific pieces of session-related information.
		/// </summary>
		public class Datum
		{
			public string key;
			public string value;

			public Datum( string key, string value )
			{
				this.key = key;
				this.value = value;
			}
		}
	}
}
