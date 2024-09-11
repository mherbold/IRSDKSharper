
using System;
using System.Collections;

namespace IRSDKSharper
{
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
