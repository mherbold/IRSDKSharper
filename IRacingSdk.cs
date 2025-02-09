﻿
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;

namespace IRSDKSharper
{
	public class IRacingSdk
	{
		private const string SimulatorMemoryMappedFileName = "Local\\IRSDKMemMapFileName";
		private const string SimulatorDataValidEventName = "Local\\IRSDKDataValidEvent";
		private const string SimulatorBroadcastMessageName = "IRSDK_BROADCASTMSG";

		public readonly IRacingSdkData Data;

		public bool IsStarted { get; private set; } = false;
		public bool IsConnected { get; private set; } = false;

		public int UpdateInterval { get; set; } = 1;

		public bool PauseSessionInfoUpdates
		{
			get => pauseSessionInfoUpdates;

			set
			{
				if ( pauseSessionInfoUpdates != value )
				{
					Log( $"PauseSessionInfoUpdates = {value}" );

					pauseSessionInfoUpdates = value;

					if ( pauseSessionInfoUpdates != false )
					{
						lastSessionInfoUpdate = 0;
					}
				}
			}
		}

		public EventSystem EventSystem { get; private set; }

		public event Action<Exception> OnException = null;
		public event Action OnConnected = null;
		public event Action OnDisconnected = null;
		public event Action OnSessionInfo = null;
		public event Action OnTelemetryData = null;
		public event Action OnEventSystemDataReset = null;
		public event Action OnEventSystemDataLoaded = null;
		public event Action OnStopped = null;
		public event Action<string> OnDebugLog = null;

		private int keepThreadsAlive = 0;

		private bool connectionLoopRunning = false;
		private bool telemetryDataLoopRunning = false;
		private bool sessionInfoLoopRunning = false;

		private MemoryMappedFile simulatorMemoryMappedFile = null;
		private MemoryMappedViewAccessor simulatorMemoryMappedFileViewAccessor = null;

		private AutoResetEvent simulatorAutoResetEvent = null;
		private AutoResetEvent sessionInfoAutoResetEvent = null;

		private int lastTelemetryDataUpdate = -1;

		private int lastSessionInfoUpdate = 0;
		private int sessionInfoUpdateReady = 0;
		private bool pauseSessionInfoUpdates = false;

		private readonly uint simulatorBroadcastWindowMessage = WinApi.RegisterWindowMessage( SimulatorBroadcastMessageName );

		/// <summary>
		/// <para>Welcome to IRSDKSharper!</para>
		/// This is the basic process to start it up:
		/// <code>
		/// var irsdk = new IRacingSDK();
		/// 
		/// irsdk.OnException += OnException;
		/// irsdk.OnConnected += OnConnected;
		/// irsdk.OnDisconnected += OnDisconnected;
		/// irsdk.OnSessionInfo += OnSessionInfo;
		/// irsdk.OnTelemetryData += OnTelemetryData;
		/// irsdk.OnStopped += OnStopped;
		/// 
		/// irsdk.Start();
		/// </code>
		/// </summary>
		/// <param name="throwYamlExceptions">Set this to true to throw exceptions when our IRacingSdkSessionInfo class is missing properties that exist in the YAML data string.</param>
		public IRacingSdk( bool throwYamlExceptions = false, bool enableEventSystem = false )
		{
			Data = new IRacingSdkData( throwYamlExceptions );

			if ( enableEventSystem )
			{
				EventSystem = new EventSystem( this );
			}
		}

		public void Start()
		{
			if ( Interlocked.Exchange( ref keepThreadsAlive, 1 ) == 1 )
			{
				Log( "IRSDKSharper has already been started or is starting." );
			}
			else
			{
				Log( "IRSDKSharper is starting." );

				var thread = new Thread( ConnectionLoop );

				thread.Start();

				while ( !connectionLoopRunning )
				{
					Thread.Sleep( 0 );
				}

				IsStarted = true;

				Log( "IRSDKSharper has been started." );
			}
		}

		public void Stop()
		{
			if ( Interlocked.Exchange( ref keepThreadsAlive, 0 ) == 0 )
			{
				Log( "IRSDKSharper has already been stopped or is stopping." );
			}
			else
			{
				Log( "IRSDKSharper is stopping." );

				Task.Run( () =>
				{
					if ( sessionInfoLoopRunning )
					{
						Log( "Waiting for session info loop to stop." );

						sessionInfoAutoResetEvent?.Set();

						while ( sessionInfoLoopRunning )
						{
							Thread.Sleep( 0 );
						}
					}

					if ( telemetryDataLoopRunning )
					{
						Log( "Waiting for telemetry data loop to stop." );

						while ( telemetryDataLoopRunning )
						{
							Thread.Sleep( 0 );
						}
					}

					Data.Reset();

					if ( connectionLoopRunning )
					{
						Log( "Waiting for connection loop to stop." );

						while ( connectionLoopRunning )
						{
							Thread.Sleep( 0 );
						}
					}

					IsStarted = false;
					IsConnected = false;

					simulatorMemoryMappedFile = null;
					simulatorMemoryMappedFileViewAccessor = null;

					simulatorAutoResetEvent = null;
					sessionInfoAutoResetEvent = null;

					lastTelemetryDataUpdate = -1;

					lastSessionInfoUpdate = 0;
					sessionInfoUpdateReady = 0;

					EventSystem?.Reset();

					Log( "IRSDKSharper has been stopped." );

					OnStopped?.Invoke();
				} );
			}
		}

		/// <summary>
		/// This event system feature is currently experimental.
		/// </summary>
		public void SetEventSystemDirectory( string directory )
		{
			EventSystem?.SetDirectory( directory ?? string.Empty );
		}

		public void FireOnEventSystemDataReset()
		{
			OnEventSystemDataReset?.Invoke();
		}

		public void FireOnEventSystemDataLoaded()
		{
			OnEventSystemDataLoaded?.Invoke();
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
			if ( !WinApi.PostMessage( (IntPtr) 0xFFFF, simulatorBroadcastWindowMessage, WinApi.MakeLong( (short) msg, var1 ), (IntPtr) var2 ) )
			{
				var errorCode = Marshal.GetLastWin32Error();

				Marshal.ThrowExceptionForHR( errorCode, IntPtr.Zero );
			}
		}

		private void BroadcastMessage( IRacingSdkEnum.BroadcastMsg msg, short var1, float var2 )
		{
			if ( !WinApi.PostMessage( (IntPtr) 0xFFFF, simulatorBroadcastWindowMessage, WinApi.MakeLong( (short) msg, var1 ), (IntPtr) ( var2 * 65536.0f ) ) )
			{
				var errorCode = Marshal.GetLastWin32Error();

				Marshal.ThrowExceptionForHR( errorCode, IntPtr.Zero );
			}
		}

		private void BroadcastMessage( IRacingSdkEnum.BroadcastMsg msg, short var1, short var2, short var3 )
		{
			if ( !WinApi.PostMessage( (IntPtr) 0xFFFF, simulatorBroadcastWindowMessage, WinApi.MakeLong( (short) msg, var1 ), WinApi.MakeLong( var2, var3 ) ) )
			{
				var errorCode = Marshal.GetLastWin32Error();

				Marshal.ThrowExceptionForHR( errorCode, IntPtr.Zero );
			}
		}

		#endregion

		#region background tasks

		private void ConnectionLoop()
		{
			Log( "Connection loop has been started." );

			try
			{
				connectionLoopRunning = true;

				while ( keepThreadsAlive == 1 )
				{
					if ( simulatorMemoryMappedFile == null )
					{
						try
						{
							simulatorMemoryMappedFile = MemoryMappedFile.OpenExisting( SimulatorMemoryMappedFileName );
						}
						catch ( FileNotFoundException )
						{
						}
					}

					if ( simulatorMemoryMappedFile != null )
					{
						Log( "simulatorMemoryMappedFile != null" );

						if ( simulatorMemoryMappedFileViewAccessor == null )
						{
							simulatorMemoryMappedFileViewAccessor = simulatorMemoryMappedFile.CreateViewAccessor();

							if ( simulatorMemoryMappedFileViewAccessor == null )
							{
								throw new Exception( "Failed to create memory mapped view accessor." );
							}
							else
							{
								Data.SetMemoryMappedViewAccessor( simulatorMemoryMappedFileViewAccessor );
							}
						}
					}

					if ( simulatorMemoryMappedFileViewAccessor != null )
					{
						Log( "simulatorMemoryMappedFileViewAccessor != null" );

						var simulatorDataValidEventHandle = WinApi.OpenEvent( WinApi.EVENT_ALL_ACCESS, false, SimulatorDataValidEventName );

						if ( simulatorDataValidEventHandle == (IntPtr) null )
						{
							var errorCode = Marshal.GetLastWin32Error();

							if ( errorCode != WinApi.ERROR_FILE_NOT_FOUND )
							{
								Marshal.ThrowExceptionForHR( errorCode, IntPtr.Zero );
							}
						}
						else
						{
							Log( "hSimulatorDataValidEvent != null" );

							simulatorAutoResetEvent = new AutoResetEvent( false )
							{
								SafeWaitHandle = new SafeWaitHandle( simulatorDataValidEventHandle, true )
							};

							sessionInfoAutoResetEvent = new AutoResetEvent( false );

							var thread = new Thread( SessionInfoLoop );

							thread.Start();

							while ( !sessionInfoLoopRunning )
							{
								Thread.Sleep( 0 );
							}

							thread = new Thread( TelemetryDataLoop );

							thread.Start();

							while ( !telemetryDataLoopRunning )
							{
								Thread.Sleep( 0 );
							}

							break;
						}
					}

					Thread.Sleep( 500 );
				}

				connectionLoopRunning = false;
			}
			catch ( Exception exception )
			{
				Log( "Exception caught inside the connection loop." );

				connectionLoopRunning = false;

				OnException?.Invoke( exception );
			}

			Log( "Connection loop has been stopped." );
		}

		private void SessionInfoLoop()
		{
			Log( "Session info loop has been started." );

			try
			{
				sessionInfoLoopRunning = true;

				while ( keepThreadsAlive == 1 )
				{
					// Debug.Log( "Waiting for session info event." );

					sessionInfoAutoResetEvent?.WaitOne();

					if ( ( keepThreadsAlive == 1 ) && IsConnected )
					{
						// Debug.Log( "Updating session info." );

						if ( Data.UpdateSessionInfo() )
						{
							sessionInfoUpdateReady = 1;
						}
					}
				}

				sessionInfoLoopRunning = false;
			}
			catch ( Exception exception )
			{
				Log( "Exception caught inside the session info loop." );

				sessionInfoLoopRunning = false;

				OnException?.Invoke( exception );
			}

			Log( "Session info loop has been stopped." );
		}

		private void TelemetryDataLoop()
		{
			Log( "Telemetry data loop has been started." );

			try
			{
				telemetryDataLoopRunning = true;

				while ( keepThreadsAlive == 1 )
				{
					var signalReceived = simulatorAutoResetEvent?.WaitOne( 1000 ) ?? false;

					if ( signalReceived )
					{
						if ( !IsConnected )
						{
							Log( "The iRacing simulator is running." );

							IsConnected = true;

							lastTelemetryDataUpdate = -1;

							lastSessionInfoUpdate = 0;
							sessionInfoUpdateReady = 0;

							OnConnected?.Invoke();
						}

						Data.Update();

						if ( ( lastSessionInfoUpdate != Data.SessionInfoUpdate ) || ( Data.TickCount >= Data.retryUpdateSessionInfoAfterTickCount ) )
						{
							// Debug.Log( "Data.SessionInfoUpdate has changed." );

							lastSessionInfoUpdate = Data.SessionInfoUpdate;

							Data.retryUpdateSessionInfoAfterTickCount = int.MaxValue;

							if ( !pauseSessionInfoUpdates )
							{
								sessionInfoAutoResetEvent?.Set();
							}
						}

						EventSystem?.Update( Data );

						if ( Interlocked.Exchange( ref sessionInfoUpdateReady, 0 ) == 1 )
						{
							// Debug.Log( "Invoking OnSessionInfo." );

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
							Log( "The iRacing simulator is no longer running." );

							IsConnected = false;

							Log( "Invoking OnDisconnected." );

							OnDisconnected?.Invoke();

							Data.Reset();

							EventSystem?.Reset();
						}
					}
				}

				telemetryDataLoopRunning = false;
			}
			catch ( Exception exception )
			{
				Log( "Exception caught inside the telemetry data loop." );

				telemetryDataLoopRunning = false;

				OnException?.Invoke( exception );
			}

			Log( "Telemetry data loop has been stopped." );
		}

		#endregion

		#region Debug logging

		[Conditional( "DEBUG" )]
		public void Log( string message )
		{
			OnDebugLog?.Invoke( message );
		}

		#endregion
	}
}
