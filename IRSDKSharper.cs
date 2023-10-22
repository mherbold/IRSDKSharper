
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace HerboldRacing
{
	public class IRSDKSharper
	{
		private const string MapName = "Local\\IRSDKMemMapFileName";
		private const string EventName = "Local\\IRSDKDataValidEvent";
		private const string BroadcastMessageName = "IRSDK_BROADCASTMSG";

		public readonly IRacingSdkData Data = new();

		public int UpdateInterval { get; set; } = 1;

		public bool IsStarted { get; private set; } = false;
		public bool IsConnected { get; private set; } = false;

		public event Action<Exception>? OnException = null;
		public event Action? OnConnected = null;
		public event Action? OnDisconnected = null;
		public event Action? OnTelemetryData = null;
		public event Action? OnSessionInfo = null;

		private bool stopNow = false;

		private bool connectionLoopRunning = false;
		private bool telemetryDataLoopRunning = false;
		private bool sessionInfoLoopRunning = false;

		private MemoryMappedFile? memoryMappedFile = null;
		private MemoryMappedViewAccessor? memoryMappedViewAccessor = null;

		private AutoResetEvent? simulatorAutoResetEvent = null;
		private AutoResetEvent? sessionInfoAutoResetEvent = null;

		private int lastTelemetryDataUpdate = -1;

		private int lastSessionInfoUpdate = -1;
		private int sessionInfoUpdateChangedCount = 0;
		private int sessionInfoUpdateReady = 0;

		private readonly int broadcastWindowMessage = Windows.RegisterWindowMessage( BroadcastMessageName ).ToInt32();

		public void Start()
		{
			Debug.WriteLine( "IRSDKSharper starting..." );

			if ( IsStarted )
			{
				throw new Exception( "IRSDKSharper has already been started." );
			}

			Task.Run( ConnectionLoop );

			IsStarted = true;

			Debug.WriteLine( "IRSDKSharper started." );
		}

		public void Stop()
		{
			Debug.WriteLine( "IRSDKSharper stopping..." );

			if ( !IsStarted )
			{
				throw new Exception( "IRSDKSharper has not been started." );
			}

			Debug.WriteLine( "Setting stopNow = true." );

			stopNow = true;

			if ( sessionInfoLoopRunning )
			{
				Debug.WriteLine( "Waiting for session info loop to stop..." );

				sessionInfoAutoResetEvent?.Set();

				while ( sessionInfoLoopRunning )
				{
					Thread.Sleep( 0 );
				}
			}

			if ( telemetryDataLoopRunning )
			{
				Debug.WriteLine( "Waiting for telemetry data loop to stop..." );

				while ( telemetryDataLoopRunning )
				{
					Thread.Sleep( 0 );
				}
			}

			Data.Reset();

			if ( connectionLoopRunning )
			{
				Debug.WriteLine( "Waiting for connection loop to stop..." );

				while ( connectionLoopRunning )
				{
					Thread.Sleep( 0 );
				}
			}

			IsStarted = false;
			IsConnected = false;

			stopNow = false;

			memoryMappedFile = null;
			memoryMappedViewAccessor = null;

			simulatorAutoResetEvent = null;
			sessionInfoAutoResetEvent = null;

			lastTelemetryDataUpdate = -1;

			lastSessionInfoUpdate = -1;
			sessionInfoUpdateChangedCount = 0;
			sessionInfoUpdateReady = 0;

			Debug.WriteLine( "IRSDKSharper stopped." );
		}

		#region simulator remote control

		public void CamSwitchPos( IRacingSdkEnum.CamSwitchMode camSwitchMode, int carPosition, int group, int camera )
		{
			if ( camSwitchMode != IRacingSdkEnum.CamSwitchMode.FocusAtDriver )
			{
				carPosition = (int) camSwitchMode;
			}

			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.CamSwitchPos, (short) carPosition, (short) group, (short) camera );
		}

		public void CamSwitchNum( IRacingSdkEnum.CamSwitchMode camSwitchMode, int carNumberRaw, int group, int camera )
		{
			if ( camSwitchMode != IRacingSdkEnum.CamSwitchMode.FocusAtDriver )
			{
				carNumberRaw = (int) camSwitchMode;
			}

			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.CamSwitchNum, (short) carNumberRaw, (short) group, (short) camera );
		}

		public void CamSetState( IRacingSdkEnum.CameraState cameraState )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.CamSetState, (short) cameraState );
		}

		public void ReplaySetPlaySpeed( int speed, bool slowMotion )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySetPlaySpeed, (short) speed, slowMotion ? 1 : 0 );
		}

		public void ReplaySetPlayPosition( IRacingSdkEnum.RpyPosMode rpyPosMode, int frameNumber )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySetPlayPosition, (short) rpyPosMode, frameNumber );
		}

		public void ReplaySearch( IRacingSdkEnum.RpySrchMode rpySrchMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySearch, (short) rpySrchMode );
		}

		public void ReplaySetState( IRacingSdkEnum.RpyStateMode rpyStateMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySetState, (short) rpyStateMode );
		}

		public void ReloadTextures( IRacingSdkEnum.ReloadTexturesMode reloadTexturesMode, int carIdx )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReloadTextures, (short) reloadTexturesMode, carIdx );
		}

		public void ChatComand( IRacingSdkEnum.ChatCommandMode chatCommandMode, int subCommand )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ChatComand, (short) chatCommandMode, subCommand );
		}

		public void PitCommand( IRacingSdkEnum.PitCommandMode pitCommandMode, int parameter )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.PitCommand, (short) pitCommandMode, parameter );
		}

		public void TelemCommand( IRacingSdkEnum.TelemCommandMode telemCommandMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.TelemCommand, (short) telemCommandMode );
		}

		public void FFBCommand( IRacingSdkEnum.FFBCommandMode ffbCommandMode, float value )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.FFBCommand, (short) ffbCommandMode, value );
		}

		public void ReplaySearchSessionTime( int sessionNum, int sessionTimeMS )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySearchSessionTime, (short) sessionNum, sessionTimeMS );
		}

		public void VideoCapture( IRacingSdkEnum.VideoCaptureMode videoCaptureMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.VideoCapture, (short) videoCaptureMode );
		}

		#endregion

		#region broadcast message functions

		private void BroadcastMessage( IRacingSdkEnum.BroadcastMsg msg, short var1, int var2 = 0 )
		{
			// TODO handle exceptions - get info when pinvoke site comes back up
			Windows.PostMessage( (IntPtr) 0xFFFF, broadcastWindowMessage, Windows.MakeLong( (short) msg, var1 ), var2 );
		}

		private void BroadcastMessage( IRacingSdkEnum.BroadcastMsg msg, short var1, float var2 )
		{
			// TODO handle exceptions - get info when pinvoke site comes back up
			Windows.PostMessage( (IntPtr) 0xFFFF, broadcastWindowMessage, Windows.MakeLong( (short) msg, var1 ), (int) ( var2 * 65536.0f ) );
		}

		private void BroadcastMessage( IRacingSdkEnum.BroadcastMsg msg, short var1, short var2, short var3 )
		{
			// TODO handle exceptions - get info when pinvoke site comes back up
			Windows.PostMessage( (IntPtr) 0xFFFF, broadcastWindowMessage, Windows.MakeLong( (short) msg, var1 ), Windows.MakeLong( var2, var3 ) );
		}

		#endregion

		#region background tasks

		private void ConnectionLoop()
		{
			Debug.WriteLine( "Connection loop started." );

			try
			{
				connectionLoopRunning = true;

				while ( !stopNow )
				{
					if ( memoryMappedFile == null )
					{
						try
						{
							memoryMappedFile = MemoryMappedFile.OpenExisting( MapName );
						}
						catch ( FileNotFoundException )
						{
						}
					}

					if ( memoryMappedFile != null )
					{
						Debug.WriteLine( "memoryMappedFile != null" );

						memoryMappedViewAccessor = memoryMappedFile.CreateViewAccessor();

						if ( memoryMappedViewAccessor == null )
						{
							throw new Exception( "Failed to create memory mapped view accessor." );
						}

						Data.SetMemoryMappedViewAccessor( memoryMappedViewAccessor );

						var hEvent = Windows.OpenEvent( Windows.EVENT_ALL_ACCESS, false, EventName );

						if ( hEvent == (IntPtr) null )
						{
							int errorCode = Marshal.GetLastWin32Error();

							Marshal.ThrowExceptionForHR( errorCode, IntPtr.Zero );
						}
						else
						{
							simulatorAutoResetEvent = new AutoResetEvent( false )
							{
								SafeWaitHandle = new SafeWaitHandle( hEvent, true )
							};

							sessionInfoAutoResetEvent = new AutoResetEvent( false );

							Task.Run( TelemetryDataLoop );
							Task.Run( SessionInfoLoop );
						}

						break;
					}
					else
					{
						Thread.Sleep( 250 );
					}
				}

				connectionLoopRunning = false;
			}
			catch ( Exception exception )
			{
				Debug.WriteLine( "Connection loop exception caught." );

				connectionLoopRunning = false;

				OnException?.Invoke( exception );
			}

			Debug.WriteLine( "Connection loop stopped." );
		}

		private void TelemetryDataLoop()
		{
			Debug.WriteLine( "Telemetry data loop starting." );

			try
			{
				telemetryDataLoopRunning = true;

				while ( !stopNow )
				{
					var signalReceived = simulatorAutoResetEvent?.WaitOne( 250 ) ?? false;

					if ( signalReceived )
					{
						if ( !IsConnected )
						{
							Debug.WriteLine( "Connected to iRacing simulator." );

							IsConnected = true;

							lastSessionInfoUpdate = -1;
							sessionInfoUpdateReady = 0;

							OnConnected?.Invoke();
						}

						Data.Update();

						if ( lastSessionInfoUpdate != Data.SessionInfoUpdate )
						{
							Debug.WriteLine( "iRacingSdkData.SessionInfoUpdate changed." );

							lastSessionInfoUpdate = Data.SessionInfoUpdate;

							Interlocked.Increment( ref sessionInfoUpdateChangedCount );

							sessionInfoAutoResetEvent?.Set();
						}

						if ( Interlocked.Exchange( ref sessionInfoUpdateReady, 0 ) == 1 )
						{
							Debug.WriteLine( "Invoking OnSessionInfo..." );

							OnSessionInfo?.Invoke();
						}

						if ( ( Data.TickCount - lastTelemetryDataUpdate ) >= UpdateInterval )
						{
							lastTelemetryDataUpdate = Data.TickCount;

							OnTelemetryData?.Invoke();
						}
					}
					else
					{
						if ( IsConnected )
						{
							Debug.WriteLine( "Disconnected from iRacing simulator." );

							if ( sessionInfoUpdateChangedCount > 0 )
							{
								Debug.WriteLine( "Draining sessionInfoUpdateChangedCount..." );

								while ( sessionInfoUpdateChangedCount > 0 )
								{
									Thread.Sleep( 0 );
								}
							}

							IsConnected = false;

							Debug.WriteLine( "Invoking OnDisconnected..." );

							OnDisconnected?.Invoke();

							Data.Reset();
						}
					}
				}

				telemetryDataLoopRunning = false;
			}
			catch ( Exception exception )
			{
				Debug.WriteLine( "Telemetry data loop exception caught." );

				telemetryDataLoopRunning = false;

				OnException?.Invoke( exception );
			}

			Debug.WriteLine( "Telemetry data loop stopped." );
		}

		private void SessionInfoLoop()
		{
			Debug.WriteLine( "Session info loop started." );

			try
			{
				sessionInfoLoopRunning = true;

				while ( !stopNow )
				{
					Debug.WriteLine( "Waiting for session info event." );

					sessionInfoAutoResetEvent?.WaitOne();

					while ( sessionInfoUpdateChangedCount > 0 )
					{
						if ( !stopNow )
						{
							Debug.WriteLine( "Updating session info..." );

							Data?.UpdateSessionInfo();

							sessionInfoUpdateReady = 1;
						}

						Interlocked.Decrement( ref sessionInfoUpdateChangedCount );
					}
				}

				sessionInfoLoopRunning = false;
			}
			catch ( Exception exception )
			{
				Debug.WriteLine( "Session info loop exception caught." );

				sessionInfoLoopRunning = false;

				OnException?.Invoke( exception );
			}

			Debug.WriteLine( "Session info loop stopped." );
		}

		#endregion
	}
}
