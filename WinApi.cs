﻿
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace IRSDKSharper
{
	internal class WinApi
	{
		public const uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
		public const uint SYNCHRONIZE = 0x00100000;
		public const uint EVENT_ALL_ACCESS = ( STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0x3 );

		[DllImport( "Kernel32.dll", SetLastError = true )]
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
