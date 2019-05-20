// SignalClosure.cs - signal marshaling class
//
// Authors: Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2008 Novell, Inc.
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


namespace GLib {

	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;

	internal class ClosureInvokedArgs : EventArgs {

		EventArgs args;
		GLib.Object obj;

		public ClosureInvokedArgs (GLib.Object obj, EventArgs args)
		{
			this.obj = obj;
			this.args = args;
		}

		public EventArgs Args {
			get {
				return args;
			}
		}

		public GLib.Object Target {
			get {
				return obj;
			}
		}
	}

	struct GClosure {
		public long fields;
		public IntPtr marshaler;
		public IntPtr data;
		public IntPtr notifiers;
	}

	internal delegate void ClosureInvokedHandler (object o, ClosureInvokedArgs args);

	internal class SignalClosure : IDisposable {

		IntPtr handle;
		IntPtr raw_closure;
		string name;
		uint id = UInt32.MaxValue;
		System.Type args_type;
		GCHandle? gch;

//		static Hashtable closures = new Hashtable ();
		static Dictionary<IntPtr, SignalClosure> closures = new Dictionary<IntPtr, SignalClosure> (IntPtrEqualityComparer.Instance);

		public SignalClosure (IntPtr obj, string signal_name, System.Type args_type)
		{
			raw_closure = g_closure_new_simple (Marshal.SizeOf (typeof (GClosure)), IntPtr.Zero);
			g_closure_set_marshal (raw_closure, Marshaler);
			g_closure_add_finalize_notifier (raw_closure, IntPtr.Zero, Notify);
			closures [raw_closure] = this;
			handle = obj;
			name = signal_name;
			this.args_type = args_type;
		}

		public SignalClosure (IntPtr obj, string signal_name, Delegate custom_marshaler, Signal signal)
		{
			gch = GCHandle.Alloc (signal);
			raw_closure = g_cclosure_new (custom_marshaler, (IntPtr) gch, Notify);
			closures [raw_closure] = this;
			handle = obj;
			name = signal_name;
		}

		public event EventHandler Disposed;
		public event ClosureInvokedHandler Invoked;

		public void Connect (bool is_after)
		{
			IntPtr native_name = GLib.Marshaller.StringToPtrGStrdup (name);
			id = g_signal_connect_closure (handle, native_name, raw_closure, is_after);
			GLib.Marshaller.Free (native_name);
		}

		public void Disconnect ()
		{
			if (id != UInt32.MaxValue && g_signal_handler_is_connected (handle, id))
				g_signal_handler_disconnect (handle, id);
		}

		public void Dispose ()
		{
			Disconnect ();
			closures.Remove (raw_closure);
			gch?.Free ();

			if (Disposed != null)
				Disposed (this, EventArgs.Empty);
			GC.SuppressFinalize (this);
		}

		public void Invoke (ClosureInvokedArgs args)
		{
			if (Invoked == null)
				return;
			Invoked (this, args);
		}

		static ClosureMarshal marshaler;
		static ClosureMarshal Marshaler {
			get {
				if (marshaler == null)
					unsafe
					{
						marshaler = new ClosureMarshal (MarshalCallback);
					}
				return marshaler;
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		unsafe delegate void ClosureMarshal (IntPtr closure, Value* return_val, uint n_param_vals, Value* param_values, IntPtr invocation_hint, IntPtr marshal_data);

		static unsafe void MarshalCallback (IntPtr raw_closure, Value* return_val, uint n_param_vals, Value* param_values, IntPtr invocation_hint, IntPtr marshal_data)
		{
			SignalClosure closure = null;
			try {
				closure = closures [raw_closure] as SignalClosure;
				GLib.Object __obj = param_values [0].Val as GLib.Object;
				if (__obj == null)
					return;

				if (closure.args_type == typeof (EventArgs)) {
					closure.Invoke (new ClosureInvokedArgs (__obj, EventArgs.Empty));
					return;
				}

				SignalArgs args = FastActivator.CreateSignalArgs (closure.args_type);
				args.Args = new object [n_param_vals - 1];
				for (int i = 1; i < n_param_vals; i++) {
					args.Args [i - 1] = param_values [i].Val;
				}
				ClosureInvokedArgs ci_args = new ClosureInvokedArgs (__obj, args);
				closure.Invoke (ci_args);
				for (int i = 1; i < n_param_vals; i++) {
					param_values [i].Update (args.Args [i - 1]);
				}
				if (return_val == null || args.RetVal == null)
					return;

				return_val->Val = args.RetVal;
			} catch (Exception e) {
				if (closure != null)
					Console.WriteLine ("Marshaling {0} signal", closure.name);
				ExceptionManager.RaiseUnhandledException (e, false);
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void ClosureNotify (IntPtr data, IntPtr closure);

		static void NotifyCallback (IntPtr data, IntPtr raw_closure)
		{
			if (closures.TryGetValue (raw_closure, out SignalClosure closure))
				closure.Dispose ();

//			Console.WriteLine ("Closure finalized " + (closure?.name) + " [  "+ closure?.handle);
		}

		static ClosureNotify notify_handler;
		static ClosureNotify Notify {
			get {
				if (notify_handler == null)
					notify_handler = new ClosureNotify (NotifyCallback);
				return notify_handler;
			}
		}

		delegate IntPtr d_g_cclosure_new(Delegate cb, IntPtr user_data, ClosureNotify notify);
		static d_g_cclosure_new g_cclosure_new = FuncLoader.LoadFunction<d_g_cclosure_new>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_cclosure_new"));

		delegate IntPtr d_g_closure_new_simple(int closure_size, IntPtr dummy);
		static d_g_closure_new_simple g_closure_new_simple = FuncLoader.LoadFunction<d_g_closure_new_simple>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_closure_new_simple"));

		delegate void d_g_closure_set_marshal(IntPtr closure, ClosureMarshal marshaler);
		static d_g_closure_set_marshal g_closure_set_marshal = FuncLoader.LoadFunction<d_g_closure_set_marshal>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_closure_set_marshal"));

		delegate void d_g_closure_add_finalize_notifier(IntPtr closure, IntPtr dummy, ClosureNotify notify);
		static d_g_closure_add_finalize_notifier g_closure_add_finalize_notifier = FuncLoader.LoadFunction<d_g_closure_add_finalize_notifier>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_closure_add_finalize_notifier"));

		delegate uint d_g_signal_connect_closure(IntPtr obj, IntPtr name, IntPtr closure, bool is_after);
		static d_g_signal_connect_closure g_signal_connect_closure = FuncLoader.LoadFunction<d_g_signal_connect_closure>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_signal_connect_closure"));

		delegate void d_g_signal_handler_disconnect(IntPtr instance, uint handler);
		static d_g_signal_handler_disconnect g_signal_handler_disconnect = FuncLoader.LoadFunction<d_g_signal_handler_disconnect>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_signal_handler_disconnect"));

		delegate bool d_g_signal_handler_is_connected(IntPtr instance, uint handler);
		static d_g_signal_handler_is_connected g_signal_handler_is_connected = FuncLoader.LoadFunction<d_g_signal_handler_is_connected>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_signal_handler_is_connected"));
	}
}

