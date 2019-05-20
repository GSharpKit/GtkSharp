// GLib.ToggleRef.cs - GLib ToggleRef class implementation
//
// Author: Mike Kestner <mkestner@novell.com>
//
// Copyright <c> 2007, 2011 Novell, Inc.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the Lesser GNU General 
// Public License as published by the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this program; if not, write to the
// Free Software Foundation, Inc., 59 Temple Place - Suite 330,
// Boston, MA 02111-1307, USA.


using System;
using System.Runtime.InteropServices;

namespace GLib {

	internal class ToggleRef {

		bool hardened;
		IntPtr handle;
		object reference;
		GCHandle gch;

		public ToggleRef (GLib.Object target, IntPtr handle)
		{
			this.handle = handle;
			gch = GCHandle.Alloc (this);
			reference = target;
			g_object_add_toggle_ref (handle, ToggleNotifyCallback, (IntPtr) gch);
			if (target.owned && !(target is InitiallyUnowned))
				g_object_unref (handle);
		}

		public IntPtr Handle {
			get { return handle; }
		}

		public GLib.Object Target {
			get {
				if (reference == null)
					return null;
				else if (reference is GLib.Object)
					return reference as GLib.Object;

				WeakReference weak = (WeakReference)reference;
				return weak.Target as GLib.Object;
			}
		}

		public void Free ()
  		{
			//If Free is called multiple times.
			if (Target == null)
				return;

			Target.FreeSignals ();

			if (hardened)
				g_object_unref (handle);
			else
				g_object_remove_toggle_ref (handle, ToggleNotifyCallback, (IntPtr) gch);
			reference = null;
//			Console.WriteLine ("GCHandler FREE " + handle.ToInt64());
			gch.Free ();
		}

		internal void Harden ()
		{
			// Added for the benefit of GnomeProgram.  It releases a final ref in
			// an atexit handler which causes toggle ref notifications to occur after 
			// our delegates are gone, so we need a mechanism to override the 
			// notifications.  This method effectively leaks all objects which invoke it, 
			// but since it is only used by Gnome.Program, which is a singleton object 
			// with program duration persistence, who cares.

			g_object_ref (handle);
			g_object_remove_toggle_ref (handle, ToggleNotifyCallback, (IntPtr) gch);
			if (reference is WeakReference)
				reference = (reference as WeakReference).Target;
			hardened = true;
		}

		void Toggle (bool is_last_ref)
		{
			if (is_last_ref && reference is GLib.Object)
				reference = new WeakReference (reference);
			else if (!is_last_ref && reference is WeakReference) {
				WeakReference weak = reference as WeakReference;
				reference = weak.Target;
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void ToggleNotifyHandler (IntPtr data, IntPtr handle, bool is_last_ref);

		static void RefToggled (IntPtr data, IntPtr handle, bool is_last_ref)
		{
			try {
				GCHandle gch = (GCHandle) data;
				ToggleRef tref = (ToggleRef)gch.Target;
				tref.Toggle (is_last_ref);
			} catch (Exception e) {
				ExceptionManager.RaiseUnhandledException (e, false);
			}
		}

		static ToggleNotifyHandler toggle_notify_callback;
		static ToggleNotifyHandler ToggleNotifyCallback {
			get {
				if (toggle_notify_callback == null)
					toggle_notify_callback = new ToggleNotifyHandler (RefToggled);
				return toggle_notify_callback;
			}
		}

		delegate void d_g_object_add_toggle_ref(IntPtr raw, ToggleNotifyHandler notify_cb, IntPtr data);
		static d_g_object_add_toggle_ref g_object_add_toggle_ref = FuncLoader.LoadFunction<d_g_object_add_toggle_ref>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_add_toggle_ref"));

		delegate void d_g_object_remove_toggle_ref(IntPtr raw, ToggleNotifyHandler notify_cb, IntPtr data);
		static d_g_object_remove_toggle_ref g_object_remove_toggle_ref = FuncLoader.LoadFunction<d_g_object_remove_toggle_ref>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_remove_toggle_ref"));

		delegate IntPtr d_g_object_ref(IntPtr raw);
		static d_g_object_ref g_object_ref = FuncLoader.LoadFunction<d_g_object_ref>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_ref"));

		delegate void d_g_object_unref(IntPtr raw);
		static d_g_object_unref g_object_unref = FuncLoader.LoadFunction<d_g_object_unref>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_unref"));

	}
}
