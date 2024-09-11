
using System;
using System.Collections;

namespace IRSDKSharper
{
	public class IRacingSdkHeaderDataAsList : IList
	{
		private readonly IRacingSdkData data;

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
