
using System.Runtime.InteropServices;

namespace HerboldRacing
{
	internal class Windows
	{
		public const uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
		public const uint SYNCHRONIZE = 0x00100000;
		public const uint EVENT_ALL_ACCESS = ( STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0x3 );

		[DllImport( "Kernel32.dll", SetLastError = true )]
		public static extern IntPtr OpenEvent( uint dwDesiredAccess, bool bInheritHandle, string lpName );

		[DllImport( "kernel32.dll", SetLastError = true )]
		public static extern bool CloseHandle( IntPtr hObject );

		// TODO confirm signature with pinvoke.net when site is back up
		[DllImport( "user32.dll" )]
		public static extern IntPtr RegisterWindowMessage( string lpProcName );

		// TODO confirm signature with pinvoke.net when site is back up
		[DllImport( "user32.dll" )]
		public static extern IntPtr PostMessage( IntPtr hWnd, int Msg, int wParam, int lParam );

		public static int MakeLong( short lowPart, short highPart )
		{
			return (int) ( ( (ushort) lowPart ) | (uint) ( highPart << 16 ) );
		}
	}
}
