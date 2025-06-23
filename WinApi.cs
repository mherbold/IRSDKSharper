
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace IRSDKSharper
{
	/// <summary>
	/// Provides a collection of Windows API functions and constants used by the application.
	/// </summary>
	/// <remarks>
	/// The WinApi class is a utility class that exposes commonly used Windows API methods
	/// and constants using PInvoke (Platform Invocation Services). This class includes
	/// methods that allow interaction with system-level Windows functionality, such as
	/// event handling, window messaging, and low-level interprocess communication.
	/// - This class is intended for internal use only.
	/// - Some of the methods employ unmanaged code and require security considerations.
	/// </remarks>
	internal class WinApi
	{
		public const int ERROR_FILE_NOT_FOUND = 2;

		public const uint EVENT_ALL_ACCESS = 0x1F0003;

		[ DllImport( "Kernel32.dll", SetLastError = true )]
		public static extern IntPtr OpenEvent( uint dwDesiredAccess, bool bInheritHandle, string lpName );

		[DllImport( "user32.dll", SetLastError = true, CharSet = CharSet.Auto )]
		public static extern uint RegisterWindowMessage( string lpString );

		[DllImport( "user32.dll", SetLastError = true, CharSet = CharSet.Auto )]
		[SuppressUnmanagedCodeSecurity]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static extern bool PostMessage( IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam );

		public static IntPtr MakeLong( short lowPart, short highPart )
		{
			return (IntPtr) ( ( (ushort) lowPart ) | (uint) ( highPart << 16 ) );
		}
	}
}
