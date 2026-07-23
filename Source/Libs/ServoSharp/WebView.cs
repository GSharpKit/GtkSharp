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
using System.Threading.Tasks;

namespace Servo {

	// Receives the outcome of WebView.EvaluateScript. Exactly one of the two
	// arguments is non-null: on success resultJson holds the script's return
	// value serialized as JSON; on failure error holds a message.
	public delegate void ScriptResultHandler (string resultJson, string error);

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

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void ServoGtkScriptResultCallback (IntPtr web_view, IntPtr result_json, IntPtr error, IntPtr user_data);

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void d_servo_gtk_web_view_evaluate_script (IntPtr raw, IntPtr script, ServoGtkScriptResultCallback callback, IntPtr user_data);
		static d_servo_gtk_web_view_evaluate_script servo_gtk_web_view_evaluate_script = FuncLoader.LoadFunction<d_servo_gtk_web_view_evaluate_script> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_web_view_evaluate_script"));

		// Stateless trampoline shared by every call; kept alive for the lifetime
		// of the process so the native side always has a valid function pointer.
		static readonly ServoGtkScriptResultCallback script_result_dispatch = ScriptResultDispatch;

		static void ScriptResultDispatch (IntPtr web_view, IntPtr result_json, IntPtr error, IntPtr user_data)
		{
			// The per-call managed handler was pinned in EvaluateScript; the
			// native callback fires exactly once, so release it here.
			GCHandle gch = (GCHandle) user_data;
			ScriptResultHandler callback = gch.Target as ScriptResultHandler;
			gch.Free ();

			try {
				string result = GLib.Marshaller.Utf8PtrToString (result_json);
				string err = GLib.Marshaller.Utf8PtrToString (error);
				callback?.Invoke (result, err);
			} catch (Exception e) {
				GLib.ExceptionManager.RaiseUnhandledException (e, false);
			}
		}

		// Asynchronously evaluates a JavaScript snippet in the view's top-level
		// browsing context. The callback is invoked exactly once, later, from
		// the GTK main loop.
		public void EvaluateScript (string script, ScriptResultHandler callback)
		{
			if (callback == null)
				throw new ArgumentNullException (nameof (callback));

			GCHandle gch = GCHandle.Alloc (callback);
			IntPtr native_script = GLib.Marshaller.StringToPtrGStrdup (script);
			servo_gtk_web_view_evaluate_script (Handle, native_script, script_result_dispatch, (IntPtr) gch);
			GLib.Marshaller.Free (native_script);
		}

		// Task-based convenience wrapper. The task completes with the script's
		// JSON result, or faults with a ScriptException on failure.
		public Task<string> EvaluateScriptAsync (string script)
		{
			TaskCompletionSource<string> tcs = new TaskCompletionSource<string> ();
			EvaluateScript (script, (result, error) => {
				if (error != null)
					tcs.SetException (new ScriptException (error));
				else
					tcs.SetResult (result);
			});
			return tcs.Task;
		}
	}

	// Thrown when an asynchronous script evaluation reports a failure.
	public class ScriptException : Exception {
		public ScriptException (string message) : base (message) {}
	}
}
