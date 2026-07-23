// Servo.WebView.cs - Servo GTK3 web view widget wrapper
//
// Hand-written binding for servo-gtk3-view.h (libservogtk3), a GtkDrawingArea
// subclass exposed by Servo's GTK3 FFI shell.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the Lesser GNU General
// Public License as published by the Free Software Foundation.

using System;
using System.Runtime.InteropServices;

namespace Servo {

	public class WebView : Gtk.DrawingArea {

		public WebView (IntPtr raw) : base (raw) {}

		[GLib.Signal ("uri-changed")]
		public event UriChangedHandler UriChanged {
			add {
				this.AddSignalHandler ("uri-changed", value, typeof (UriChangedArgs));
			}
			remove {
				this.RemoveSignalHandler ("uri-changed", value);
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_servo_gtk_web_view_new ();
		static d_servo_gtk_web_view_new servo_gtk_web_view_new = FuncLoader.LoadFunction<d_servo_gtk_web_view_new> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_web_view_new"));

		public WebView () : base (IntPtr.Zero)
		{
			if (GetType () != typeof (WebView)) {
				CreateNativeObject (Array.Empty<string> (), Array.Empty<GLib.Value> ());
				return;
			}
			Raw = servo_gtk_web_view_new ();
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_servo_gtk_web_view_get_type ();
		static d_servo_gtk_web_view_get_type servo_gtk_web_view_get_type = FuncLoader.LoadFunction<d_servo_gtk_web_view_get_type> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_web_view_get_type"));

		public static new GLib.GType GType {
			get {
				IntPtr raw_ret = servo_gtk_web_view_get_type ();
				GLib.GType ret = new GLib.GType (raw_ret);
				return ret;
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void d_servo_gtk_web_view_load_uri (IntPtr raw, IntPtr uri);
		static d_servo_gtk_web_view_load_uri servo_gtk_web_view_load_uri = FuncLoader.LoadFunction<d_servo_gtk_web_view_load_uri> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_web_view_load_uri"));

		public void LoadUri (string uri)
		{
			IntPtr native_uri = GLib.Marshaller.StringToPtrGStrdup (uri);
			servo_gtk_web_view_load_uri (Handle, native_uri);
			GLib.Marshaller.Free (native_uri);
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_servo_gtk_web_view_get_uri (IntPtr raw);
		static d_servo_gtk_web_view_get_uri servo_gtk_web_view_get_uri = FuncLoader.LoadFunction<d_servo_gtk_web_view_get_uri> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_web_view_get_uri"));

		public string Uri {
			get {
				IntPtr raw_ret = servo_gtk_web_view_get_uri (Handle);
				// (transfer none) - owned by the widget, do not free.
				string ret = GLib.Marshaller.Utf8PtrToString (raw_ret);
				return ret;
			}
		}
	}
}
