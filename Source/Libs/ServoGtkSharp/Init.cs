// Servo.Init.cs - Servo GTK engine lifetime
//
// Hand-written binding for servo_gtk_init() and servo_gtk_release() in
// servo-gtk3-view.h (libservogtk3).
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the Lesser GNU General
// Public License as published by the Free Software Foundation.

using System;
using System.Runtime.InteropServices;

namespace Servo {

	public static class Functions {

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate bool d_servo_gtk_init (IntPtr data_dir);
		static d_servo_gtk_init servo_gtk_init = FuncLoader.LoadFunction<d_servo_gtk_init> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_init"));

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void d_servo_gtk_release ();
		static d_servo_gtk_release servo_gtk_release = FuncLoader.LoadFunction<d_servo_gtk_release> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_release"));

		// Makes Servo available to WebView. Call it before the first web view
		// is created - until then web views stay blank - and call Release when
		// done, typically around Application.Run. Servo keeps running while
		// there is no web view, so they can be closed and opened at any time
		// in between; it cannot be started again once it has shut down.
		//
		// Cookies are kept in cookie_jar.json in dataDir, or in memory only if
		// dataDir is null. Servo also keeps hsts_list.json, localstorage.json
		// and auth_cache.json there; the last holds HTTP credentials in plain
		// text, so use a directory only the user can read.
		//
		// Calls are counted and each needs its own Release. Only the first
		// sets the data directory. Returns false if Servo has already been
		// shut down or dataDir cannot be used, in which case Release must not
		// be called for it.
		public static bool Init (string dataDir = null)
		{
			IntPtr native_data_dir = GLib.Marshaller.StringToFilenamePtr (dataDir);
			bool ret = servo_gtk_init (native_data_dir);
			GLib.Marshaller.Free (native_data_dir);
			return ret;
		}

		// Undoes one Init. The last one shuts Servo down and writes the
		// cookies - or, if web views still exist, the last of them to be
		// freed does. Destroy every web view first: a Servo still running
		// when the process exits crashes it.
		public static void Release ()
		{
			servo_gtk_release ();
		}
	}
}
