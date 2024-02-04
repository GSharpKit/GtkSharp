using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

class GLibrary
{

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetDllDirectory(string lpPathName);

	private static Dictionary<Library, IntPtr> _libraries;
	private static HashSet<Library> _librariesNotFound;
	private static Dictionary<string, IntPtr> _customlibraries;
	private static Dictionary<Library, string[]> _libraryDefinitions;

	private static string framewokePath;

	static GLibrary()
	{
		framewokePath = Environment.GetEnvironmentVariable("GTK_SHARP_FRAMEWORK_PATH") ?? string.Empty;

		_customlibraries = new Dictionary<string, IntPtr>();
		_librariesNotFound = new HashSet<Library>();
		_libraries = new Dictionary<Library, IntPtr>();
		_libraryDefinitions = new Dictionary<Library, string[]>();
		_libraryDefinitions[Library.GLib] = new[] {"libglib-2.0-0.dll", "libglib-2.0.so.0", "libglib-2.0.0.dylib", "glib-2.dll"};
		_libraryDefinitions[Library.GObject] = new[] {"libgobject-2.0-0.dll", "libgobject-2.0.so.0", "libgobject-2.0.0.dylib", "gobject-2.dll"};
		_libraryDefinitions[Library.Cairo] = new[] {"libcairo-2.dll", "libcairo.so.2", "libcairo.2.dylib", "cairo.dll"};
		_libraryDefinitions[Library.Gio] = new[] {"libgio-2.0-0.dll", "libgio-2.0.so.0", "libgio-2.0.0.dylib", "gio-2.dll"};
		_libraryDefinitions[Library.Atk] = new[] {"libatk-1.0-0.dll", "libatk-1.0.so.0", "libatk-1.0.0.dylib", "atk-1.dll"};
		_libraryDefinitions[Library.Pango] = new[] {"libpango-1.0-0.dll", "libpango-1.0.so.0", "libpango-1.0.0.dylib", "pango-1.dll"};
		_libraryDefinitions[Library.Gdk] = new[] {"libgdk-3-0.dll", "libgdk-3.so.0", "libgdk-3.0.dylib", "gdk-3.dll"};
		_libraryDefinitions[Library.GdkPixbuf] = new[] {"libgdk_pixbuf-2.0-0.dll", "libgdk_pixbuf-2.0.so.0", "libgdk_pixbuf-2.0.dylib", "gdk_pixbuf-2.dll"};
		_libraryDefinitions[Library.Gtk] = new[] {"libgtk-3-0.dll", "libgtk-3.so.0", "libgtk-3.0.dylib", "gtk-3.dll"};
		_libraryDefinitions[Library.PangoCairo] = new[] {"libpangocairo-1.0-0.dll", "libpangocairo-1.0.so.0", "libpangocairo-1.0.0.dylib", "pangocairo-1.dll"};
		_libraryDefinitions[Library.GtkSource] = new[] {"libgtksourceview-4-0.dll", "libgtksourceview-4.so.0", "libgtksourceview-4.0.dylib", "gtksourceview-4.dll"};
	        _libraryDefinitions[Library.Webkit] = new[] { "libwebkitgtk-3.0-0.dll", "libwebkitgtk-3.0.so.0", "libwebkitgtk-3.0.dylib", "libwebkitgtk-3.0.dll" };
	        _libraryDefinitions[Library.Gdl] = new[] { "libgdl-3-5.dll", "libgdl-3.so.5", "libgdl-3.5.dylib", "libgdl-3.dll" };
	        _libraryDefinitions[Library.Gst] = new[] { "libgstreamer-1.0-0.dll", "libgstreamer-1.0.so.0", "libgstreamer-1.0.dylib", "libgstreamer-1.0.dll" };
	        _libraryDefinitions[Library.GstApp] = new[] { "libgstapp-1.0-0.dll", "libgstapp-1.0.so.0", "libgstapp-1.0.dylib", "libgstapp-1.0.dll" };
	        _libraryDefinitions[Library.GstAudio] = new[] { "libgstaudio-1.0-0.dll", "libgstaudio-1.0.so.0", "libgstaudio-1.0.dylib", "libgstaudio-1.0.dll" };
	        _libraryDefinitions[Library.GstBase] = new[] { "libgstbase-1.0-0.dll", "libgstbase-1.0.so.0", "libgstbase-1.0.dylib", "libgstbase-1.0.dll" };
	        _libraryDefinitions[Library.GstController] = new[] { "libgstcontroller-1.0-0.dll", "libgstcontroller-1.0.so.0", "libgstcontroller-1.0.dylib", "libgstcontroller-1.0.dll" };
	        _libraryDefinitions[Library.GstNet] = new[] { "libgstnet-1.0-0.dll", "libgstnet-1.0.so.0", "libgstnet-1.0.dylib", "libgstnet-1.0.dll" };
	        _libraryDefinitions[Library.GstPbutils] = new[] { "libgstpbutils-1.0-0.dll", "libgstpbutils-1.0.so.0", "libgstpbutils-1.0.dylib", "libgstpbutils-1.0.dll" };
	        _libraryDefinitions[Library.GstRtp] = new[] { "libgstrtp-1.0-0.dll", "libgstrtp-1.0.so.0", "libgstrtp-1.0.dylib", "libgstrtp-1.0.dll" };
	        _libraryDefinitions[Library.GstRtsp] = new[] { "libgstrtsp-1.0-0.dll", "libgstrtsp-1.0.so.0", "libgstrtsp-1.0.dylib", "libgstrtsp-1.0.dll" };
	        _libraryDefinitions[Library.GstSdp] = new[] { "libgstsdp-1.0-0.dll", "libgstsdp-1.0.so.0", "libgstsdp-1.0.dylib", "libgstsdp-1.0.dll" };
	        _libraryDefinitions[Library.GstTag] = new[] { "libgsttag-1.0-0.dll", "libgsttag-1.0.so.0", "libgsttag-1.0.dylib", "libgsttag-1.0.dll" };
	        _libraryDefinitions[Library.GstVideo] = new[] { "libgstvideo-1.0-0.dll", "libgstvideo-1.0.so.0", "libgstvideo-1.0.dylib", "libgstvideo-1.0.dll" };
	        _libraryDefinitions[Library.GstWebRTC] = new[] { "libgstwebrtc-1.0-0.dll", "libgstwebrtc-1.0.so.0", "libgstwebrtc-1.0.dylib", "libgstwebrtc-1.0.dll" };
	        _libraryDefinitions[Library.GtkMacIntegration] = new[] { "libgtkmacintegration-gtk3.4.dylib", "libgtkmacintegration-gtk3.4.dylib", "libgtkmacintegration-gtk3.4.dylib", "libgtkmacintegration-gtk3.4.dylib" };
	}

	public static IntPtr Load(Library library)
	{
		if (_libraries.TryGetValue(library, out var ret))
			return ret;

		if (TryGet(library, out ret)) return ret;

		var err = library + ": " + string.Join(", ", _libraryDefinitions[library]);

		throw new DllNotFoundException(err);

	}

	public static bool IsSupported(Library library) => TryGet(library, out var __);

	static bool TryGet(Library library, out IntPtr ret)
	{
		ret = IntPtr.Zero;

		if (_libraries.TryGetValue(library, out ret)) {
			return true;
		}

		if (_librariesNotFound.Contains(library)) {
			return false;
		}

		if (FuncLoader.IsWindows) {
			ret = FuncLoader.LoadLibrary(_libraryDefinitions[library][0]);

			if (ret == IntPtr.Zero) {
				SetDllDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gtk", "3.24.24"));
				ret = FuncLoader.LoadLibrary(_libraryDefinitions[library][0]);
			}
		} else if (FuncLoader.IsOSX) {
			ret = FuncLoader.LoadLibrary(framewokePath + _libraryDefinitions[library][2]);

			if (ret == IntPtr.Zero) {
				ret = FuncLoader.LoadLibrary("/usr/local/lib/" + _libraryDefinitions[library][2]);
				if (ret == IntPtr.Zero) {
					ret = FuncLoader.LoadLibrary("/opt/homebrew/lib/" + _libraryDefinitions[library][2]);
				}
			}
		} else
			ret = FuncLoader.LoadLibrary(_libraryDefinitions[library][1]);

		if (ret == IntPtr.Zero) {
			for (var i = 0; i < _libraryDefinitions[library].Length; i++) {
				ret = FuncLoader.LoadLibrary(_libraryDefinitions[library][i]);

				if (ret != IntPtr.Zero)
					break;
			}
		}

		if (ret != IntPtr.Zero) {
			_libraries[library] = ret;
		} else {
			_librariesNotFound.Add(library);
		}

		return ret != IntPtr.Zero;
	}

}
