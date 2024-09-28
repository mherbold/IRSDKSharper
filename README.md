# IRSDKSharper
Alternative C# implementation of the iRacing SDK.
I created this project because I was frustrated with the performance and features of IRSDKSharp.
If you find any bugs or have any questions or feedback or ideas or whatever, please feel free to open up a new issue on Github!

https://github.com/mherbold/IRSDKSharper

# Requirements
Memory based telemetry must be enabled in the iRacing Simulator for the features of this SDK to work.
This setting can be found in the iRacing Simulator app.ini file.
```
irsdkEnableMem=1
```

# Notes
When migrating to version 1.1.0 from a previous version, there are two changes you need to make -
* The namespace was changed from HerboldRacing to IRSDKSharper
* The IRSDKSharper class name was changed to IRacingSdk
* That's it - everything else should work as it did before

# How to use it
Here is an example basic project to demonstrate how to set up and use this library.
The IRacingSdk class can be found within the IRSDKSharper namespace.
```cs
using IRSDKSharper;

public partial class MainWindow : Window
{
    private IRacingSdk irsdk;

    public MainWindow()
    {
        InitializeComponent();

        // create an instance of IRacingSdk
        irsdk = new IRacingSdk();

        // hook up our event handlers
        irsdk.OnException += OnException;
        irsdk.OnConnected += OnConnected;
        irsdk.OnDisconnected += OnDisconnected;
        irsdk.OnSessionInfo += OnSessionInfo;
        irsdk.OnTelemetryData += OnTelemetryData;
        irsdk.OnStopped += OnStopped;
        irsdk.OnDebugLog += OnDebugLog;

        // this means fire the OnTelemetryData event every 30 data frames (2 times a second)
        irsdk.UpdateInterval = 30; 

        // lets go!
        irsdk.Start();
    }

    private void Window_Closing( object sender, CancelEventArgs e )
    {
        irsdk.Stop();
    }

    private void OnException( Exception exception )
    {
        Debug.WriteLine( "OnException() fired!" );
    }

    private void OnConnected()
    {
        Debug.WriteLine( "OnConnected() fired!" );
    }

    private void OnDisconnected()
    {
        Debug.WriteLine( "OnDisconnected() fired!" );
    }

    private void OnSessionInfo()
    {
        var trackName = irsdk.Data.SessionInfo.WeekendInfo.TrackName;

        Debug.WriteLine( $"OnSessionInfo fired! Track name is {trackName}." );
    }

    private void OnTelemetryData()
    {
        var lapDistPct = irsdk.Data.GetFloat( "CarIdxLapDistPct", 5 );

        Debug.WriteLine( $"OnTelemetryData fired! Lap dist pct for the 6th car in the array is {lapDistPct}." );
    }

    private void OnStopped()
    {
        Debug.WriteLine( "OnStopped() fired!" );
    }

    private void OnDebugLog( string message )
    {
        Debug.WriteLine( message )
    }
```

# IRSDKSharper Class

## Methods

### IRacingSdk( bool throwYamlExceptions = false )
When you create a new instance of IRacingSdk you can pass in true to turn on the throwing of exceptions whenever the YAML parser detects that the IRacingSdkSessionInfo is missing some properties.
This would normally be left off in your projects.
This is really just a way for me to quickly figure out what is missing as iRacing is continually adding new session information properties.

### void Start()
IRacingSdk will create a new connection loop background task.
This connection loop will wait for the iRacing simulator to load and start broadcasting telemetry data.
Once telemetry data starts pouring in, IRacingSdk will terminate the connection loop background task, and create two new background tasks<sup>1</sup>.
The first new background task handles session information updates, and the second new background task handles telemetry data updates<sup>2</sup>.

### void Stop()
IRacingSdk will terminate all background tasks and clean up everything.
This is done asynchronously, and it is safe to call `Stop()` from within any of your event handlers.
The `IsStarted` property will become false and the `OnStopped` event will be fired when IRacingSdk has completely stopped.
Please call `Stop()` before setting your IRacingSdk object to null to terminate all of the background tasks.

### Simulator Remote Control
There are several functions you can use to remotely control the iRacing simulator.
These functions control the iRacing simulator by posting broadcast messages to the iRacing simulator window.
```
CamSwitchPos( IRacingSdkEnum.CamSwitchMode camSwitchMode, int carPosition, int group, int camera )
CamSwitchNum( IRacingSdkEnum.CamSwitchMode camSwitchMode, int carNumberRaw, int group, int camera )
CamSetState( IRacingSdkEnum.CameraState cameraState )
ReplaySetPlaySpeed( int speed, bool slowMotion )
ReplaySetPlayPosition( IRacingSdkEnum.RpyPosMode rpyPosMode, int frameNumber )
ReplaySearch( IRacingSdkEnum.RpySrchMode rpySrchMode )
ReplaySetState( IRacingSdkEnum.RpyStateMode rpyStateMode )
ReloadTextures( IRacingSdkEnum.ReloadTexturesMode reloadTexturesMode, int carIdx )
ChatComand( IRacingSdkEnum.ChatCommandMode chatCommandMode, int subCommand )
PitCommand( IRacingSdkEnum.PitCommandMode pitCommandMode, int parameter )
TelemCommand( IRacingSdkEnum.TelemCommandMode telemCommandMode )
FFBCommand( IRacingSdkEnum.FFBCommandMode ffbCommandMode, float value )
ReplaySearchSessionTime( int sessionNum, int sessionTimeMS )
VideoCapture( IRacingSdkEnum.VideoCaptureMode videoCaptureMode )
```

## Properties

### public readonly IRacingSdkData Data
This is where you can find all of the data coming from the iRacing simulator.
More on this in the *IRacingSdkData Class* chapter below.

### public int UpdateInterval { get; set; } = 1 (default)
You can set `UpdateInterval` to any integer.
Data frames are normally received from the iRacing simulator 60 times per second.
A value of 1 or less will result in the `OnTelemetryData` event being fired for every data frame.
Values above 1 means means you want to discard data frames to reduce how often the event is fired.
For example, setting this to 2 means to fire the event every second data frame.

### public bool IsStarted { get; private set; }
This property indicates whether or not this instance of IRacingSdk is in the started state or is in the stopped state.

### public bool IsConnected { get; private set; }
This property indicates whether or not the iRacing simulator is running and broadcasting data frames.

### public event Action<Exception>? OnException
Exceptions caught within any of the three background tasks will be passed into `OnException`.
You will likely want to copy the exception information somewhere and handle it from within your main thread.
At this point, the background task that fired the exception will terminate.
You can call `Stop` then `Start` to clean up and restart IRacingSdk.

### public event Action? OnConnected
The `OnConnected` event is fired when IRacingSdk has detected that the iRacing simulator has started up and is broadcasting telemetry data.

### public event Action? OnDisconnected
The `OnDisconnected` event is fired when IRacingSdk has detected that the iRacing simulator has exited.
IRacingSdk will start waiting for the iRacing simulator to come back and start broadcasting telemetry data again.
If and when it does, the `OnConnected` event will be fired again at that time.

### public event Action? OnSessionInfo
The `OnSessionInfo` event is fired after IRacingSdk has received and fully processed the YAML session information data from the iRacing simulator.
IRacingSdk will not process any more frames of data until your event handler completes.
For this reason, it is important that you do things quickly in your event handler in order to avoid dropping frames of data.

### public event Action? OnTelemetryData
The `OnTelemetryData` event is fired whenever we receive a new frame of telemetry data from the iRacing simulator.
IRacingSdk will not process any more frames of data until your event handler completes.
For this reason, it is important that you do things quickly in your event handler in order to avoid dropping frames of data.

### public event Action? OnStopped
The `OnStopped` event is fired when IRacingSdk has fully stopped.

### public event Action? OnDebugLog
The `OnDebugLog` event is fired when IRacingSdk wants to give you a debug message.
You can do whatever you want with this message, such as send it to Debug.WriteLine (or Debug.Log in Unity!), or write it to file, or just discard it.
This event does not fire in release builds.

# IRacingSdkData Class
All iRacing simulator data can be accessed through the `IRacingSdk.Data` property.

## Properties

### Data.SessionInfo
This is the session information data (the decoded YAML string).

### Data.SessionInfoYaml
This is the raw un-decoded YAML string that we received from the iRacing simulator.

### Data.TelemetryDataProperties
This is a dictionary describing each of the available iRacing simulator telemetry variables that is being broadcast.
Each item in this dictionary has the following properties -
```
item.VarType
item.Offset
item.Bytes
item.Count
item.Name
item.Desc
item.Unit
```

### iRacing Simulator Data Header
You can access the various properties of the iRacing simulator data header if you need them.
```
Data.Version
Data.Status
Data.TickRate
Data.SessionInfoUpdate
Data.SessionInfoLength
Data.SessionInfoOffset
Data.VarCount
Data.VarHeaderOffset
Data.BufferCount
Data.BufferLength
```

### Computed
There are also the following calculated properties available as well.
```
Data.TickCount
Data.Offset
Data.FramesDropped
```
The `FramesDropped` property is a useful one - you can check it to see if we are dropping telemetry data frames.
Usually just a couple of frames are dropped at the beginning when the iRacing simulator starts up.
Ideally there should be no frames dropped after that point.

## Methods
To access the actual values of this telemetry data you would use one of the following methods<sup>3</sup>.
```
char GetChar( string name, int index = 0 )
bool GetBool( string name, int index = 0 )
int GetInt( string name, int index = 0 )
uint GetBitField( string name, int index = 0 )
float GetFloat( string name, int index = 0 )
double GetDouble( string name, int index = 0 )
```
You may also use this generic method as well, which would return the data value as a generic object -
```
object GetValue( string name, int index = 0 )
```
You may also use array versions of these functions as well, which would return the data values as arrays -
```
int GetCharArray( string name, char[] array, int index, int count )
int GetBoolArray( string name, bool[] array, int index, int count )
int GetIntArray( string name, int[] array, int index, int count )
int GetBitFieldArray( string name, uint[] array, int index, int count )
int GetFloatArray( string name, float[] array, int index, int count )
int GetDoubleArray( string name, double[] array, int index, int count )
```
The above array functions return the number of elements retrieved.

There are now even faster versions of the telemetry data functions that you can use that skips the dictionary lookup -
```
char GetChar( IRacingSdkDatum datum, int index = 0 )
bool GetBool( IRacingSdkDatum datum, int index = 0 )
int GetInt( IRacingSdkDatum datum, int index = 0 )
uint GetBitField( IRacingSdkDatum datum, int index = 0 )
float GetFloat( IRacingSdkDatum datum, int index = 0 )
double GetDouble( IRacingSdkDatum datum, int index = 0 )

int GetCharArray( IRacingSdkDatum datum, char[] array, int index, int count )
int GetBoolArray( IRacingSdkDatum datum, bool[] array, int index, int count )
int GetIntArray( IRacingSdkDatum datum, int[] array, int index, int count )
int GetBitFieldArray( IRacingSdkDatum datum, uint[] array, int index, int count )
int GetFloatArray( IRacingSdkDatum datum, float[] array, int index, int count )
int GetDoubleArray( IRacingSdkDatum datum, double[] array, int index, int count )
```
To use these you could do something like:
```cs
// you would do this once and save it somewhere
var carIdxLapDistPctDatum = irsdk.Data.TelemetryDataProperties[ "CarIdxLapDistPct" ];

// and then now you can repeatedly call this for the most blisteringly fastest speed possible
var lapDistPct = irsdk.Data.GetFloat( carIdxLapDistPctDatum, 5 );
```

# Event system (experimental)
There is a new event system that is designed to fill in the missing holes in the telemetry data when replaying a session, and to give you an easy way to find various events without needing to scan the entire replay.
There are also a extra calculated events that will give you information that isn't readily available in the raw telemetry data.

This section will be expanded in the future with an in-depth guide on using the event system, but for now here's something to get you started.

## Overview
The event system is basically a List\<Event\> of events stored in Dictionary\<string, EventTrack\> tracks.
The keys of the tracks dictionary are strings that corresponds directly to the telemetry data name.

For example if you wanted to get to the events for the air density you would do -

```cs
var airDensityTrack = irsdk.EventSystem.Tracks[ "AirDensity" ]
```

Or to get the gear changes for carIdx=3 you would do -

```cs
var gearChangesTrack = irsdk.EventSystem.Tracks[ "CarIdxGear[3]" ]
```

Each event in the track is basically -
```cs
int SessionNum
double SessionTime
T Value
```
Where T is the data type of the event (int, float, etc.)

## Initializing
Call irsdk.EnableEventSystem( directory ) at some point before calling irsdkSharper.Start().
The directory parameter is where you want the event system to create its files in.

Hook up some callbacks 
```cs
irsdk.OnEventSystemDataReset += OnEventSystemDataReset;
irsdk.OnEventSystemDataLoaded += OnEventSystemDataLoaded;
```

The OnEventSystemDataReset is called whenever the events in the event system has been cleared out.
The OnEventSystemDataLoaded is called whenevr the events in the event system has been loaded from file.
These callbacks are useful if you have data from the event system being displayed via UI and need to know when to refresh the display.

## Calculated events
There is currently one calculated event that gives you data that is not readily available via the iRacing telemetry data, and that is the G force on each car in the direction along the track.
Only G forces greater than +/-2.0g are recorded.
To access it you would do something like -
```cs
var gForceTrack = irsdk.EventSystem.Tracks[ "CarIdxGForce[3]" ]
```
Using this information you can very easily tell exactly when a car experienced a collision of some sort.

# Example projects
There are several projects that I have written, of varying complexity, that uses this library.
They are all on GitHub and you can refer to these to understand better how to use this stuff.

## IRSDKSharperTest

https://github.com/mherbold/IRSDKSharperTest

This is a basic no-frills WPF app that creates a window, connects to the iRacing simulator, and displays the header, telemetry data, and session info in real time.

## iRacingStages

https://github.com/mherbold/iRacingStages

This is another basic WPF app that creates a window, connects to the iRacing simulator, and throws the yellow flag on specified laps.

## IRWindSim

https://github.com/mherbold/IRWindSim

This is another basic WPF app that creates a window, and controls my SRS "Hurricane" Power Wind Kit using a custom Arduino setup I have created.

## iRacing-TV 2

https://github.com/mherbold/IRacing-TV-2

This is a monster Unity based app that is a work in progress.
This is the next iteration of my iRacing-TV app, which was originally based on IRSDKSharp.
This app uses all of the features of IRSDKSharper, and would serve as the most comprehensive example.

# Differences to IRSDKSharp
1. The connection loop is never terminated in IRSDKSharp even though it is not needed any more after the iRacing simulator starts up.
2. IRSDKSharp handles both session information updates and telemetry data in the same background task. Since the processing of the YAML session information string can take several frames, this causes an undesired stutter or frame drops in the telemetry data. IRSDKSharper does not suffer from this issue.
3. The methods used to retrieve telemetry data runs many times faster in IRSDKSharper compared to IRSDKSharp, and in release builds they resolve to very fast inline direct memory access calls.
4. IRSDKSharper fixes known issues with the iRacing SDK (see below).
5. IRSDKSharp has some library dependencies that prevents it from working with Unity.

# Fixes to IRSDK
Unfortunately, the iRacing SDK has some bugs and errors. Most of these bugs are known to the developers at iRacing, but they will not be fixed due to the likelihood of breaking compatibility with existing apps. IRSDKSharper fixes all of these known issues -
1. The CarsLeftRight telemetry data is incorrectly declared to be a bit field by the iRacing simulator. IRSDKSharper corrects this property to be an integer.
2. The PaceFlags telemetry data is incorrectly declared to be an integer by the iRacing simulator. IRSDKSharper corrects this property to be a bit field.
3. The iRacing simulator session information YAML data is improperly formatted and will crash deserializers under certain conditions. IRSDKSharper patches the YAML so it is properly formatted.

# Roadmap
1. Add automatic recording / playback of telemetry data that is missing in iRacing's replay files.
2. Add useful  additional telemetry derived from normalization / corrections to iRacing telemetry and session information data.
