
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

		/// <summary>
		/// Starts the IRacingSdk's main process to establish a connection with the iRacing simulator.
		/// Once initiated, a connection thread is started, and the SDK enters its operational state.
		/// This method ensures the SDK is not started multiple times simultaneously.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the SDK is already started or in the process of starting.
		/// </exception>
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
		/// Stops the IRacing SDK, ensuring all loops, threads, and connections are properly terminated.
		/// This method resets the internal state of the SDK, including session info updates, telemetry loops,
		/// and connection monitoring. The <c>OnStopped</c> event is invoked upon successful completion.
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
		/// Sets the directory for the event system. This feature is experimental and may change in future updates.
		/// </summary>
		/// <param name="directory">The path to the directory to be set for the event system. If null or empty, it resets the directory.</param>
		public void SetEventSystemDirectory(string directory)
		{
			EventSystem?.SetDirectory( directory ?? string.Empty );
		}

		/// <summary>
		/// Triggers the `OnEventSystemDataReset` event for the IRacingSdk instance.
		/// This event is invoked to signal that the event system's data has been reset,
		/// allowing any subscribed handlers to respond to the data reset operation.
		/// </summary>
		public void FireOnEventSystemDataReset()
		{
			OnEventSystemDataReset?.Invoke();
		}

		/// <summary>
		/// Triggers the <see cref="IRacingSdk.OnEventSystemDataLoaded"/> event if there are any subscribers.
		/// This method is typically used internally to notify the event system that event data has been successfully loaded
		/// and is now available for use.
		/// </summary>
		public void FireOnEventSystemDataLoaded()
		{
			OnEventSystemDataLoaded?.Invoke();
		}

		#region simulator remote control

		/// <summary>
		/// Switches the camera to a specified car position, camera group, and camera index.
		/// </summary>
		/// <param name="camSwitchMode">The mode that determines how to switch the camera, such as focusing on a specific driver or based on car position.</param>
		/// <param name="carPosition">The position of the car to focus on. If the camSwitchMode is not FocusAtDriver, this is overridden by the mode value.</param>
		/// <param name="group">The camera group index to switch to.</param>
		/// <param name="camera">The index of the camera to activate within the specified group.</param>
		public void CamSwitchPos( IRacingSdkEnum.CamSwitchMode camSwitchMode, int carPosition, int group, int camera )
		{
			if ( camSwitchMode != IRacingSdkEnum.CamSwitchMode.FocusAtDriver )
			{
				carPosition = (int) camSwitchMode;
			}

			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.CamSwitchPos, (short) carPosition, (short) group, (short) camera );
		}

		/// <summary>
		/// Sends a command to switch the camera focus to a specific car or predefined perspective.
		/// </summary>
		/// <param name="camSwitchMode">The camera switching mode, determining focus such as specific driver or a predefined view (e.g., leader or incident).</param>
		/// <param name="carNumberRaw">The raw car number to focus on, used when the mode is not a predefined perspective.</param>
		/// <param name="group">The camera group to use, referring to different sets of camera angles or types (e.g., trackside or cockpit).</param>
		/// <param name="camera">The specific camera within the selected group to activate.</param>
		public void CamSwitchNum( IRacingSdkEnum.CamSwitchMode camSwitchMode, int carNumberRaw, int group, int camera )
		{
			if ( camSwitchMode != IRacingSdkEnum.CamSwitchMode.FocusAtDriver )
			{
				carNumberRaw = (int) camSwitchMode;
			}

			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.CamSwitchNum, (short) carNumberRaw, (short) group, (short) camera );
		}

		/// <summary>
		/// Sets the camera state based on the provided CameraState flags. This allows control over various camera behaviors,
		/// such as toggling UI visibility, enabling auto shot selection, activating the scenic camera mode, and more.
		/// </summary>
		/// <param name="cameraState">Flags representing the desired camera state. Combine multiple flags from the CameraState enum to specify the camera behaviors.</param>
		public void CamSetState( IRacingSdkEnum.CameraState cameraState )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.CamSetState, (short) cameraState );
		}

		/// <summary>
		/// Sets the playback speed of the replay.
		/// </summary>
		/// <param name="speed">The playback speed to set. Positive values increase playback speed, while negative values reverse it.</param>
		/// <param name="slowMotion">If true, enables slow motion mode. Otherwise, normal speed adjustments are applied.</param>
		public void ReplaySetPlaySpeed( int speed, bool slowMotion )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySetPlaySpeed, (short) speed, slowMotion ? 1 : 0 );
		}

		/// <summary>
		/// Sets the play position of the replay based on the specified position mode and frame number.
		/// </summary>
		/// <param name="rpyPosMode">Defines the position within the replay to move to, such as beginning, current, or end.</param>
		/// <param name="frameNumber">The specific frame number to set the replay position to.</param>
		public void ReplaySetPlayPosition( IRacingSdkEnum.RpyPosMode rpyPosMode, int frameNumber )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySetPlayPosition, (short) rpyPosMode, frameNumber );
		}

		/// <summary>
		/// Initiates a replay search operation based on the specified search mode.
		/// </summary>
		/// <param name="rpySrchMode">Specifies the mode of the replay search, determining the target of the search operation (e.g., start, end, next lap).</param>
		public void ReplaySearch( IRacingSdkEnum.RpySrchMode rpySrchMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySearch, (short) rpySrchMode );
		}

		/// <summary>
		/// Sets the replay state within the iRacing simulator.
		/// </summary>
		/// <param name="rpyStateMode">The desired replay state mode to set, represented as a value of type <see cref="IRacingSdkEnum.RpyStateMode"/>.</param>
		public void ReplaySetState( IRacingSdkEnum.RpyStateMode rpyStateMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySetState, (short) rpyStateMode );
		}

		/// <summary>
		/// Reloads textures in iRacing. This can be used to update the appearance of the specified car or all cars.
		/// </summary>
		/// <param name="reloadTexturesMode">Specifies the mode for reloading textures. Use <see cref="IRacingSdkEnum.ReloadTexturesMode.All"/> to reload all textures or <see cref="IRacingSdkEnum.ReloadTexturesMode.CarIdx"/> to reload textures for a specific car.</param>
		/// <param name="carIdx">The index of the car whose textures should be reloaded. This parameter is used only when <paramref name="reloadTexturesMode"/> is set to <see cref="IRacingSdkEnum.ReloadTexturesMode.CarIdx"/>.</param>
		public void ReloadTextures( IRacingSdkEnum.ReloadTexturesMode reloadTexturesMode, int carIdx )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReloadTextures, (short) reloadTexturesMode, carIdx );
		}

		/// <summary>
		/// Sends a chat command within the iRacing simulator. This method allows broadcasting various chat commands, such as starting a macro, beginning a chat, replying, or canceling.
		/// </summary>
		/// <param name="chatCommandMode">The mode of the chat command to execute (e.g., Macro, BeginChat, Reply, Cancel).</param>
		/// <param name="subCommand">An integer specifying additional parameters or subcommands related to the chat operation.</param>
		public void ChatComand( IRacingSdkEnum.ChatCommandMode chatCommandMode, int subCommand )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ChatComand, (short) chatCommandMode, subCommand );
		}

		/// <summary>
		/// Sends a pit command to the iRacing simulator, allowing adjustments to pit options like fueling, tires, or windshield tear-offs.
		/// </summary>
		/// <param name="pitCommandMode">Specifies the type of pit command to execute, such as adding fuel, changing tires, or clearing specific settings, defined in the PitCommandMode enumeration.</param>
		/// <param name="parameter">An optional parameter for the command, if required by the specified pitCommandMode.</param>
		public void PitCommand( IRacingSdkEnum.PitCommandMode pitCommandMode, int parameter )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.PitCommand, (short) pitCommandMode, parameter );
		}

		/// <summary>
		/// Sends a telemetry command to the iRacing simulator.
		/// </summary>
		/// <param name="telemCommandMode">The mode of the telemetry command to be broadcasted.
		/// Refer to <see cref="IRacingSdkEnum.TelemCommandMode"/> for supported modes, such as Start, Stop, and Restart.</param>
		public void TelemCommand( IRacingSdkEnum.TelemCommandMode telemCommandMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.TelemCommand, (short) telemCommandMode );
		}

		/// <summary>
		/// Sends a Force Feedback (FFB) command to adjust specific FFB settings, such as maximum force.
		/// </summary>
		/// <param name="ffbCommandMode">The specific FFB command to execute, defined by the <see cref="IRacingSdkEnum.FFBCommandMode"/> enumeration (e.g., MaxForce).</param>
		/// <param name="value">The numeric value associated with the FFB command, such as the maximum force setting.</param>
		public void FFBCommand( IRacingSdkEnum.FFBCommandMode ffbCommandMode, float value )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.FFBCommand, (short) ffbCommandMode, value );
		}

		/// <summary>
		/// Searches the replay to a specific session time in the specified session.
		/// </summary>
		/// <param name="sessionNum">The session number to search within.</param>
		/// <param name="sessionTimeMS">The session time in milliseconds to search for.</param>
		public void ReplaySearchSessionTime( int sessionNum, int sessionTimeMS )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.ReplaySearchSessionTime, (short) sessionNum, sessionTimeMS );
		}

		/// <summary>
		/// Controls the video capture functionality in iRacing, allowing actions such as starting, stopping, toggling, and managing video or screenshot capture operations.
		/// </summary>
		/// <param name="videoCaptureMode">The mode specifying the video capture action to perform. Possible values are defined in the <see cref="IRacingSdkEnum.VideoCaptureMode"/> enumeration.</param>
		public void VideoCapture( IRacingSdkEnum.VideoCaptureMode videoCaptureMode )
		{
			BroadcastMessage( IRacingSdkEnum.BroadcastMsg.VideoCapture, (short) videoCaptureMode );
		}

		#endregion

		#region broadcast message functions

		/// <summary>
		/// Sends a broadcast message to the simulator using the specified message type and parameters.
		/// </summary>
		/// <param name="msg">The type of broadcast message to send, as defined in the <see cref="IRacingSdkEnum.BroadcastMsg"/> enumeration.</param>
		/// <param name="var1">The first parameter for the broadcast message, typically used to specify a mode or state.</param>
		/// <param name="var2">An optional second parameter for the broadcast message, defaulting to 0 if not provided.</param>
		private void BroadcastMessage( IRacingSdkEnum.BroadcastMsg msg, short var1, int var2 = 0 )
		{
			if ( !WinApi.PostMessage( (IntPtr) 0xFFFF, simulatorBroadcastWindowMessage, WinApi.MakeLong( (short) msg, var1 ), (IntPtr) var2 ) )
			{
				var errorCode = Marshal.GetLastWin32Error();

				Marshal.ThrowExceptionForHR( errorCode, IntPtr.Zero );
			}
		}

		/// <summary>
		/// Sends a message to iRacing's simulator via a broadcast window message with specified parameters.
		/// </summary>
		/// <param name="msg">The type of broadcast message to send, as defined in the BroadcastMsg enum.</param>
		/// <param name="var1">An additional parameter associated with the specific broadcast message.</param>
		/// <param name="var2">A floating-point parameter associated with the specific broadcast message, scaled before being sent.</param>
		private void BroadcastMessage( IRacingSdkEnum.BroadcastMsg msg, short var1, float var2 )
		{
			if ( !WinApi.PostMessage( (IntPtr) 0xFFFF, simulatorBroadcastWindowMessage, WinApi.MakeLong( (short) msg, var1 ), (IntPtr) ( var2 * 65536.0f ) ) )
			{
				var errorCode = Marshal.GetLastWin32Error();

				Marshal.ThrowExceptionForHR( errorCode, IntPtr.Zero );
			}
		}

		/// <summary>
		/// Sends a message to the simulator's broadcast window to perform specific actions.
		/// </summary>
		/// <param name="msg">The type of broadcast message to send, defined by the <see cref="IRacingSdkEnum.BroadcastMsg"/> enumeration.</param>
		/// <param name="var1">The first parameter associated with the broadcast message.</param>
		/// <param name="var2">The second parameter associated with the broadcast message.</param>
		/// <param name="var3">The third parameter associated with the broadcast message.</param>
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

		/// <summary>
		/// Handles the connection loop that continuously monitors the state of the simulator.
		/// It attempts to establish a connection to the simulator's memory-mapped file,
		/// validates the event handle, and manages thread execution for maintaining the connection.
		/// </summary>
		/// <remarks>
		/// This method runs as a separate thread. It ensures the simulator memory-mapped file
		/// and view accessor are properly initialized and monitors their state during the application's runtime.
		/// If any exceptions occur during the execution of the connection loop, they are logged,
		/// and the exception is passed to the OnException event for handling.
		/// </remarks>
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

		/// <summary>
		/// Manages a dedicated thread for handling session information updates from the iRacing SDK.
		/// This loop waits for session info events and triggers session information updates when appropriate.
		/// It ensures continuous processing while threads are alive and the connection remains active.
		/// </summary>
		/// <remarks>
		/// The method runs indefinitely in a separate thread until instructed to stop.
		/// It listens for session info events, validates the connection, and attempts to update the session data.
		/// Exceptions encountered during the execution are logged and forwarded to subscribed handlers.
		/// </remarks>
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

		/// <summary>
		/// Represents the main loop responsible for handling telemetry data updates from the iRacing simulator.
		/// This internal method processes incoming telemetry data, maintains the connection state, and invokes relevant events based on the data received or simulator status changes.
		/// </summary>
		/// <remarks>
		/// This method continuously runs in its own thread while the simulator is active. It monitors signal events to process telemetry updates, manages connection status, and synchronizes telemetry and session information updates. If an exception occurs, the method ensures the loop stops safely and raises an exception event.
		/// </remarks>
		/// <exception cref="Exception">
		/// Thrown when an error occurs during the execution of the telemetry data loop.
		/// </exception>
		/// <event cref="IRacingSdk.OnConnected">Raised when the simulator connection is established.</event>
		/// <event cref="IRacingSdk.OnDisconnected">Raised when the simulator connection is lost.</event>
		/// <event cref="IRacingSdk.OnSessionInfo">Raised when session information updates are ready.</event>
		/// <event cref="IRacingSdk.OnTelemetryData">Raised when telemetry data updates are available.</event>
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
