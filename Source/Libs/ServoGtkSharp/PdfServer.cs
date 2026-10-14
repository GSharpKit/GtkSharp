// Servo.PdfServer.cs - Servo GTK PDF.js server wrapper
//
// Hand-written binding for servo-gtk-pdf-server.h (libservogtk3): a loopback
// HTTP server hosting the PDF.js viewer and the documents it shows, since
// PDF.js cannot run from file://.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the Lesser GNU General
// Public License as published by the Free Software Foundation.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Servo {

	public class PdfServer : GLib.Object {

		// Sub directory of a system data dir (<prefix>/share) holding the
		// PDF.js release installed with servo-gtk.
		const string PdfjsDataDir = "servogtk";
		const string PdfjsMarker = "build";

		public PdfServer (IntPtr raw) : base (raw) {}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_servo_gtk_pdf_server_new (IntPtr pdfjs_dir, IntPtr documents_dir, out IntPtr error);
		static d_servo_gtk_pdf_server_new servo_gtk_pdf_server_new = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_new> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_new"));

		// Starts the server with the PDF.js release installed with servo-gtk.
		public PdfServer () : this (FindPdfjsDirectory () ?? throw new DirectoryNotFoundException ("No PDF.js release found in the system data directories"), null) {}

		// pdfjs_dir is the PDF.js release directory (containing build/ and
		// web/). documents_dir exposes a whole directory, or is null to
		// publish files one at a time with AddDocument.
		public PdfServer (string pdfjs_dir, string documents_dir) : base (IntPtr.Zero)
		{
			if (pdfjs_dir == null)
				throw new ArgumentNullException (nameof (pdfjs_dir));

			IntPtr native_pdfjs_dir = GLib.Marshaller.StringToFilenamePtr (pdfjs_dir);
			IntPtr native_documents_dir = GLib.Marshaller.StringToFilenamePtr (documents_dir);
			IntPtr error = IntPtr.Zero;
			Raw = servo_gtk_pdf_server_new (native_pdfjs_dir, native_documents_dir, out error);
			GLib.Marshaller.Free (native_pdfjs_dir);
			GLib.Marshaller.Free (native_documents_dir);
			if (error != IntPtr.Zero) throw new GLib.GException (error);
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_servo_gtk_pdf_server_get_type ();
		static d_servo_gtk_pdf_server_get_type servo_gtk_pdf_server_get_type = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_get_type> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_get_type"));

		public static new GLib.GType GType {
			get {
				IntPtr raw_ret = servo_gtk_pdf_server_get_type ();
				GLib.GType ret = new GLib.GType (raw_ret);
				return ret;
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_g_get_system_data_dirs ();
		static d_g_get_system_data_dirs g_get_system_data_dirs = FuncLoader.LoadFunction<d_g_get_system_data_dirs> (FuncLoader.GetProcAddress (GLibrary.Load (Library.GLib), "g_get_system_data_dirs"));

		// The system data dirs are relative to the GLib installation on
		// Windows and XDG_DATA_DIRS elsewhere, so this finds the PDF.js release
		// wherever servo-gtk was installed. Returns null if there is none.
		public static string FindPdfjsDirectory ()
		{
			// (transfer none) - owned by GLib, do not free.
			string[] data_dirs = GLib.Marshaller.NullTermPtrToStringArray (g_get_system_data_dirs (), false);

			foreach (string data_dir in data_dirs) {
				string dir = Path.Combine (data_dir, PdfjsDataDir);
				if (Directory.Exists (Path.Combine (dir, PdfjsMarker)))
					return dir;
			}

			return null;
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_servo_gtk_pdf_server_get_origin (IntPtr raw);
		static d_servo_gtk_pdf_server_get_origin servo_gtk_pdf_server_get_origin = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_get_origin> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_get_origin"));

		// e.g. http://127.0.0.1:41235. Not a usable URL on its own: it carries
		// no capability token.
		public string Origin {
			get {
				// (transfer none) - owned by the server, do not free.
				return GLib.Marshaller.Utf8PtrToString (servo_gtk_pdf_server_get_origin (Handle));
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate uint d_servo_gtk_pdf_server_get_port (IntPtr raw);
		static d_servo_gtk_pdf_server_get_port servo_gtk_pdf_server_get_port = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_get_port> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_get_port"));

		public uint Port {
			get {
				return servo_gtk_pdf_server_get_port (Handle);
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_servo_gtk_pdf_server_get_url (IntPtr raw, IntPtr path);
		static d_servo_gtk_pdf_server_get_url servo_gtk_pdf_server_get_url = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_get_url> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_get_url"));

		// Absolute, token-scoped URL for a server-relative path such as
		// /pdfjs/web/viewer.html. Returns null if path is not server-relative.
		public string GetUrl (string path)
		{
			IntPtr native_path = GLib.Marshaller.StringToPtrGStrdup (path);
			IntPtr raw_ret = servo_gtk_pdf_server_get_url (Handle, native_path);
			GLib.Marshaller.Free (native_path);
			return GLib.Marshaller.PtrToStringGFree (raw_ret);
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_servo_gtk_pdf_server_add_document (IntPtr raw, IntPtr file_path, IntPtr name, out IntPtr error);
		static d_servo_gtk_pdf_server_add_document servo_gtk_pdf_server_add_document = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_add_document> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_add_document"));

		// Publishes a single file at /documents/<name>; name defaults to the
		// file's base name and must be a single path segment. Publishing over
		// an existing name replaces it. Returns the server-relative path, for
		// GetViewerUrl.
		public string AddDocument (string file_path, string name = null)
		{
			if (file_path == null)
				throw new ArgumentNullException (nameof (file_path));

			IntPtr native_file_path = GLib.Marshaller.StringToFilenamePtr (file_path);
			IntPtr native_name = GLib.Marshaller.StringToPtrGStrdup (name);
			IntPtr error = IntPtr.Zero;
			IntPtr raw_ret = servo_gtk_pdf_server_add_document (Handle, native_file_path, native_name, out error);
			GLib.Marshaller.Free (native_file_path);
			GLib.Marshaller.Free (native_name);
			if (error != IntPtr.Zero) throw new GLib.GException (error);
			return GLib.Marshaller.PtrToStringGFree (raw_ret);
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate bool d_servo_gtk_pdf_server_remove_document (IntPtr raw, IntPtr name);
		static d_servo_gtk_pdf_server_remove_document servo_gtk_pdf_server_remove_document = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_remove_document> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_remove_document"));

		// Withdraws a document by bare name or by the path AddDocument
		// returned. Returns true if something was withdrawn.
		public bool RemoveDocument (string name)
		{
			IntPtr native_name = GLib.Marshaller.StringToPtrGStrdup (name);
			bool ret = servo_gtk_pdf_server_remove_document (Handle, native_name);
			GLib.Marshaller.Free (native_name);
			return ret;
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_servo_gtk_pdf_server_get_viewer_url (IntPtr raw, IntPtr document_path, IntPtr viewer_hash);
		static d_servo_gtk_pdf_server_get_viewer_url servo_gtk_pdf_server_get_viewer_url = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_get_viewer_url> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_get_viewer_url"));

		// The PDF.js viewer URL for a document, to hand to WebView.LoadUri.
		// viewer_hash holds PDF.js hash parameters without the leading #,
		// e.g. "page=3&zoom=page-width". Returns null on failure.
		public string GetViewerUrl (string document_path, string viewer_hash = null)
		{
			IntPtr native_document_path = GLib.Marshaller.StringToPtrGStrdup (document_path);
			IntPtr native_viewer_hash = GLib.Marshaller.StringToPtrGStrdup (viewer_hash);
			IntPtr raw_ret = servo_gtk_pdf_server_get_viewer_url (Handle, native_document_path, native_viewer_hash);
			GLib.Marshaller.Free (native_document_path);
			GLib.Marshaller.Free (native_viewer_hash);
			return GLib.Marshaller.PtrToStringGFree (raw_ret);
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate bool d_servo_gtk_pdf_server_set_viewer_preferences (IntPtr raw, IntPtr preferences_json, out IntPtr error);
		static d_servo_gtk_pdf_server_set_viewer_preferences servo_gtk_pdf_server_set_viewer_preferences = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_set_viewer_preferences> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_set_viewer_preferences"));

		// Overrides PDF.js viewer options with a JSON object of option names
		// to values (e.g. {"defaultZoomValue": "page-width"}), or null to
		// clear. Throws GLib.GException if the JSON is not an object, in
		// which case nothing changes.
		public void SetViewerPreferences (string preferences_json)
		{
			IntPtr native_preferences_json = GLib.Marshaller.StringToPtrGStrdup (preferences_json);
			IntPtr error = IntPtr.Zero;
			servo_gtk_pdf_server_set_viewer_preferences (Handle, native_preferences_json, out error);
			GLib.Marshaller.Free (native_preferences_json);
			if (error != IntPtr.Zero) throw new GLib.GException (error);
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate bool d_servo_gtk_pdf_server_get_toolbar_visible (IntPtr raw);
		static d_servo_gtk_pdf_server_get_toolbar_visible servo_gtk_pdf_server_get_toolbar_visible = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_get_toolbar_visible> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_get_toolbar_visible"));

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void d_servo_gtk_pdf_server_set_toolbar_visible (IntPtr raw, bool visible);
		static d_servo_gtk_pdf_server_set_toolbar_visible servo_gtk_pdf_server_set_toolbar_visible = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_set_toolbar_visible> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_set_toolbar_visible"));

		// Whether the PDF.js viewer shows its own toolbar (and sidebar).
		// Hide it when the host application provides its own controls.
		public bool ToolbarVisible {
			get {
				return servo_gtk_pdf_server_get_toolbar_visible (Handle);
			}
			set {
				servo_gtk_pdf_server_set_toolbar_visible (Handle, value);
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate bool d_servo_gtk_pdf_server_get_relax_style_csp (IntPtr raw);
		static d_servo_gtk_pdf_server_get_relax_style_csp servo_gtk_pdf_server_get_relax_style_csp = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_get_relax_style_csp> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_get_relax_style_csp"));

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void d_servo_gtk_pdf_server_set_relax_style_csp (IntPtr raw, bool relax);
		static d_servo_gtk_pdf_server_set_relax_style_csp servo_gtk_pdf_server_set_relax_style_csp = FuncLoader.LoadFunction<d_servo_gtk_pdf_server_set_relax_style_csp> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_pdf_server_set_relax_style_csp"));

		// Adds 'unsafe-inline' to the style-src of PDF.js's own CSP. This
		// relaxes a security control and is off by default; it only works
		// around a Servo restyle stall, so leave it off unless measured.
		public bool RelaxStyleCsp {
			get {
				return servo_gtk_pdf_server_get_relax_style_csp (Handle);
			}
			set {
				servo_gtk_pdf_server_set_relax_style_csp (Handle, value);
			}
		}
	}
}
