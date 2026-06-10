
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
	/// <summary>
	/// Provides access to the iRacing shared memory feed and broadcast message API.
	/// </summary>
	public class IRacingSdk
	{
		private const string SimulatorMemoryMappedFileName = "Local\\IRSDKMemMapFileName";
		private const string SimulatorDataValidEventName = "Local\\IRSDKDataValidEvent";
		private const string SimulatorBroadcastMessageName = "IRSDK_BROADCASTMSG";

		/// <summary>
		/// Gets the current telemetry and session data cache.
		/// </summary>
		public readonly IRacingSdkData Data;

		/// <summary>
		/// Gets a value indicating whether the background connection logic has been started.
		/// </summary>
		public bool IsStarted { get; private set; } = false;

		/// <summary>
		/// Gets a value indicating whether the library is currently receiving valid simulator updates.
		/// </summary>
		public bool IsConnected { get; private set; } = false;

		/// <summary>
		/// Gets or sets the minimum number of simulator ticks between <see cref="OnTelemetryData"/> callbacks.
		/// </summary>
		public int UpdateInterval { get; set; } = 1;

		/// <summary>
		/// Gets or sets the delay, in milliseconds, between connection attempts while waiting for iRacing to start.
		/// </summary>
		public int ConnectionCheckIntervalInMS { get; set; } = 500;

		/// <summary>
		/// Gets or sets a value indicating whether session info refreshes should be temporarily suppressed.
		/// </summary>
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

		/// <summary>
		/// Gets the optional event system used to record and replay tracked values.
		/// </summary>
		public EventSystem EventSystem { get; private set; }

		/// <summary>
		/// Occurs when a background processing loop throws an exception.
		/// </summary>
		public event Action<Exception> OnException = null;

		/// <summary>
		/// Occurs when the library detects that the simulator is sending valid data.
		/// </summary>
		public event Action OnConnected = null;

		/// <summary>
		/// Occurs when simulator updates stop arriving.
		/// </summary>
		public event Action OnDisconnected = null;

		/// <summary>
		/// Occurs after a new session info YAML payload has been parsed successfully.
		/// </summary>
		public event Action OnSessionInfo = null;

		/// <summary>
		/// Occurs when a telemetry update passes the <see cref="UpdateInterval"/> filter.
		/// </summary>
		public event Action OnTelemetryData = null;

		/// <summary>
		/// Occurs when the optional event system resets its cached state.
		/// </summary>
		public event Action OnEventSystemDataReset = null;

		/// <summary>
		/// Occurs after the optional event system loads recorded data from disk.
		/// </summary>
		public event Action OnEventSystemDataLoaded = null;

		/// <summary>
		/// Occurs after <see cref="Stop"/> finishes cleaning up background state.
		/// </summary>
		public event Action OnStopped = null;

		/// <summary>
		/// Occurs when a debug log message is emitted.
		/// </summary>
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
		/// Initializes a new instance of the <see cref="IRacingSdk"/> class.
		/// <para>Typical startup flow:</para>
		/// <code>
		/// var irsdk = new IRacingSdk();
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
		/// <param name="throwYamlExceptions">
		/// <see langword="true"/> to rethrow YAML parsing failures when the session info payload contains unmapped properties; otherwise unmatched properties are ignored.
		/// </param>
		/// <param name="enableEventSystem"><see langword="true"/> to create the optional <see cref="EventSystem"/> instance.</param>
		public IRacingSdk( bool throwYamlExceptions = false, bool enableEventSystem = false )
		{
			Data = new IRacingSdkData( throwYamlExceptions );

			if ( enableEventSystem )
			{
				EventSystem = new EventSystem( this );
			}
		}

		/// <summary>
		/// Starts the background threads that monitor the simulator connection and process telemetry.
		/// </summary>
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

		/// <summary>
		/// Stops the background threads and clears the current connection state.
		/// </summary>
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
		/// Sets the directory used by the optional event system to store or load recorded event data.
		/// </summary>
		/// <param name="directory">The target directory, or <see langword="null"/> to clear the current directory.</param>
		public void SetEventSystemDirectory( string directory )
		{
			EventSystem?.SetDirectory( directory ?? string.Empty );
		}

		/// <summary>
		/// Raises <see cref="OnEventSystemDataReset"/>.
		/// </summary>
		public void FireOnEventSystemDataReset()
		{
			OnEventSystemDataReset?.Invoke();
		}

		/// <summary>
		/// Raises <see cref="OnEventSystemDataLoaded"/>.
		/// </summary>
		public void FireOnEventSystemDataLoaded()
		{
			OnEventSystemDataLoaded?.Invoke();
		}

		#region simulator remote control

		/// <summary>
		/// Switches the active camera using a car position identifier.
		/// </summary>
		/// <param name="camSwitchMode">The camera switching mode to use.</param>
		/// <param name="carPosition">The target car position when <paramref name="camSwitchMode"/> is <see cref="IRacingSdkEnum.CamSwitchMode.FocusAtDriver"/>.</param>
		/// <param name="group">The camera group number.</param>
		/// <param name="camera">The camera number within the selected group.</param>
		public void CamSwitchPos( IRacingSdkEnum.CamSwitchMode camSwitchMode, int carPosition, int group, int camera )
		{
			if ( camSwitchMode != IRacingSdkEnum.CamSwitchMode.FocusAtDriver )
			{
				carPosition = (int) camSwitchMode;
			}

			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.CamSwitchPos, (short) carPosition, (short) group, (short) camera );
		}

		/// <summary>
		/// Switches the active camera using a raw car number identifier.
		/// </summary>
		/// <param name="camSwitchMode">The camera switching mode to use.</param>
		/// <param name="carNumberRaw">The target raw car number when <paramref name="camSwitchMode"/> is <see cref="IRacingSdkEnum.CamSwitchMode.FocusAtDriver"/>.</param>
		/// <param name="group">The camera group number.</param>
		/// <param name="camera">The camera number within the selected group.</param>
		public void CamSwitchNum( IRacingSdkEnum.CamSwitchMode camSwitchMode, int carNumberRaw, int group, int camera )
		{
			if ( camSwitchMode != IRacingSdkEnum.CamSwitchMode.FocusAtDriver )
			{
				carNumberRaw = (int) camSwitchMode;
			}

			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.CamSwitchNum, (short) carNumberRaw, (short) group, (short) camera );
		}

		/// <summary>
		/// Sets the current camera state flags.
		/// </summary>
		/// <param name="cameraState">The camera state flags to apply.</param>
		public void CamSetState( IRacingSdkEnum.CameraState cameraState )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.CamSetState, (short) cameraState );
		}

		/// <summary>
		/// Sets the replay playback speed.
		/// </summary>
		/// <param name="speed">The requested playback speed multiplier.</param>
		/// <param name="slowMotion"><see langword="true"/> to play in slow motion.</param>
		public void ReplaySetPlaySpeed( int speed, bool slowMotion )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySetPlaySpeed, (short) speed, slowMotion ? 1 : 0 );
		}

		/// <summary>
		/// Moves replay playback to a new frame position.
		/// </summary>
		/// <param name="rpyPosMode">The positioning mode to use.</param>
		/// <param name="frameNumber">The target frame number for modes that require it.</param>
		public void ReplaySetPlayPosition( IRacingSdkEnum.RpyPosMode rpyPosMode, int frameNumber )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySetPlayPosition, (short) rpyPosMode, frameNumber );
		}

		/// <summary>
		/// Searches the replay using one of the built-in navigation modes.
		/// </summary>
		/// <param name="rpySrchMode">The search mode to execute.</param>
		public void ReplaySearch( IRacingSdkEnum.RpySrchMode rpySrchMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySearch, (short) rpySrchMode );
		}

		/// <summary>
		/// Sets the replay state.
		/// </summary>
		/// <param name="rpyStateMode">The replay state command.</param>
		public void ReplaySetState( IRacingSdkEnum.RpyStateMode rpyStateMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySetState, (short) rpyStateMode );
		}

		/// <summary>
		/// Requests a texture reload.
		/// </summary>
		/// <param name="reloadTexturesMode">The texture reload mode.</param>
		/// <param name="carIdx">The car index used when reloading a specific car.</param>
		public void ReloadTextures( IRacingSdkEnum.ReloadTexturesMode reloadTexturesMode, int carIdx )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReloadTextures, (short) reloadTexturesMode, carIdx );
		}

		/// <summary>
		/// Sends a chat command to the simulator.
		/// </summary>
		/// <param name="chatCommandMode">The chat command mode.</param>
		/// <param name="subCommand">The chat sub-command value.</param>
		public void ChatComand( IRacingSdkEnum.ChatCommandMode chatCommandMode, int subCommand )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ChatComand, (short) chatCommandMode, subCommand );
		}

		/// <summary>
		/// Sends a pit command to the simulator.
		/// </summary>
		/// <param name="pitCommandMode">The pit command mode.</param>
		/// <param name="parameter">The command parameter value.</param>
		public void PitCommand( IRacingSdkEnum.PitCommandMode pitCommandMode, int parameter )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.PitCommand, (short) pitCommandMode, parameter );
		}

		/// <summary>
		/// Starts, stops, or restarts telemetry logging in the simulator.
		/// </summary>
		/// <param name="telemCommandMode">The telemetry command to send.</param>
		public void TelemCommand( IRacingSdkEnum.TelemCommandMode telemCommandMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.TelemCommand, (short) telemCommandMode );
		}

		/// <summary>
		/// Sends a force-feedback command to the simulator.
		/// </summary>
		/// <param name="ffbCommandMode">The force-feedback command mode.</param>
		/// <param name="value">The command value.</param>
		public void FFBCommand( IRacingSdkEnum.FFBCommandMode ffbCommandMode, float value )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.FFBCommand, (short) ffbCommandMode, value );
		}

		/// <summary>
		/// Searches replay data using session time.
		/// </summary>
		/// <param name="sessionNum">The session number to search within.</param>
		/// <param name="sessionTimeMS">The target session time, in milliseconds.</param>
		public void ReplaySearchSessionTime( int sessionNum, int sessionTimeMS )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySearchSessionTime, (short) sessionNum, sessionTimeMS );
		}

		/// <summary>
		/// Controls screenshot and video capture actions.
		/// </summary>
		/// <param name="videoCaptureMode">The capture command to send.</param>
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

					Thread.Sleep( ConnectionCheckIntervalInMS );
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
					var signalReceived = simulatorAutoResetEvent?.WaitOne( 3000 ) ?? false;

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

		/// <summary>
		/// Emits a debug log message when the library is built in <c>DEBUG</c> configuration.
		/// </summary>
		/// <param name="message">The message to publish.</param>
		[Conditional( "DEBUG" )]
		public void Log( string message )
		{
			OnDebugLog?.Invoke( message );
		}

		#endregion
	}
}
