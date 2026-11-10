// Object.cs - GObject class wrapper implementation
//
// Authors: Mike Kestner <mkestner@speakeasy.net>
//          Andres G. Aragoneses <knocte@gmail.com>
//
// Copyright (c) 2001-2003 Mike Kestner
// Copyright (c) 2004-2005 Novell, Inc.
// Copyright (c) 2013 Andres G. Aragoneses
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
	using System.Collections.Generic;
	using System.Reflection;
	using System.Runtime.InteropServices;
	using System.Linq;

	public class Object : IWrapper, IDisposable {

		IntPtr handle;
		ToggleRef tref;
		bool disposed;
		static uint idx = 1;
		static Dictionary<IntPtr, ToggleRef> Objects = new Dictionary<IntPtr, ToggleRef>();
		static Dictionary<IntPtr, Dictionary<IntPtr, GLib.Value>> PropertiesToSet = new Dictionary<IntPtr, Dictionary<IntPtr, GLib.Value>>();

		public static List<(IntPtr, string)> ObjectConstructionTraces = new List<(IntPtr, string)>();

		public static bool IsRecordingObjectTrace { get; set; }
		public static List<(IntPtr, string)> ObjectTraceRecordingNew = new List<(IntPtr, string)>();
		public static List<(IntPtr, string)> ObjectTraceRecordingRemoved = new List<(IntPtr, string)>();
		// One entry per live wrapper. Removed entries are matched by their trace string, not by handle:
		// when a wrapper is disposed and the same native object is wrapped again later, the new wrapper
		// has its own entry and must still be reported. Construction is traced from both the constructor
		// and the Raw setter, so a wrapper has two entries; keep one per handle.
		public static List<(IntPtr, string)> ObjectTraceRecordingDiff {
			get {
				var removed = new HashSet<string> (ObjectTraceRecordingRemoved.Select (x => x.Item2), ReferenceComparer<string>.Instance);
				var seen = new HashSet<IntPtr> ();
				return ObjectTraceRecordingNew.Where (x => !removed.Contains (x.Item2) && seen.Add (x.Item1)).ToList ();
			}
		}

		public static List<string> QueuedFreeTraces = new List<string>();

		// Objects created during recording that were freed by the GC finalizer instead of Dispose ().
		public static List<(IntPtr, string)> ObjectTraceRecordingFinalized = new List<(IntPtr, string)>();

		~Object ()
		{
			if (WarnOnFinalize)
				Console.Error.WriteLine ("Unexpected finalization of " + GetType() + " instance.  Consider calling Dispose. (" + handle.ToInt64 () + ")");

			Dispose (false);
		}

		public void Dispose ()
		{
			if (disposed)
				return;

			GC.SuppressFinalize (this);

			Dispose (true);
			disposed = true;
		}

		protected virtual void Dispose (bool disposing)
		{
			ToggleRef tref;
			lock (Objects) {
				if (Objects.TryGetValue (Handle, out tref)) {
					Objects.Remove (Handle);
				}
			}

			handle = IntPtr.Zero;
			if (tref == null)
				return;

			if (disposing)
			{
				tref.Dispose ();

				if (signals != null)
				{
					foreach (var sig in signals.Keys)
						signals[sig].Free ();
				}
			}
			else
			{
				if (signals != null)
					QueueSignalFree ();

				tref.QueueUnref ();
			}

			signals = null;
			disposed = true;
		}

		static string[] filter = {
			"System.Reflection",
			"GLib.Object",
			"GLib.Signal.Closure",
			"System.Runtime.CompilerServices",
			"System.Delegate.DynamicInvokeImpl",
			"System.MulticastDelegate.DynamicInvokeImpl",
			"System.Delegate.DynamicInvoke",
			"GLib.SignalClosure.Invoke",
			"GLib.SignalClosure.MarshalCallback",
			"System.Threading.Tasks",
			"GLib.GLibSynchronizationContext",
			"System.Object.wrapper_native",
			"System.Threading.ExecutionContext",
			"Gtk.Builder.BindFields",
			"System.RuntimeType.CreateInstanceMono",
			"System.Activator.CreateInstance"
		};

		// Describes what native code is doing when a wrapper is created without any
		// application frames on the stack, e.g. the signal whose arguments are being marshaled.
		[ThreadStatic]
		static string traceContext;

		internal static string TraceContext {
			get => traceContext;
			set => traceContext = value;
		}

		/// <summary>True while object construction is traced or recorded. Used by generated callbacks.</summary>
		[System.ComponentModel.EditorBrowsable (System.ComponentModel.EditorBrowsableState.Never)]
		public static bool IsTracingObjects => IsRecordingObjectTrace || TraceObjectConstruction;

		/// <summary>Object tracing only: sets the context printed with wrappers created on this thread; returns the previous one.</summary>
		[System.ComponentModel.EditorBrowsable (System.ComponentModel.EditorBrowsableState.Never)]
		public static string EnterTraceContext (string context)
		{
			string previous = traceContext;
			traceContext = context;
			return previous;
		}

		/// <summary>Object tracing only: restores the context returned by <see cref="EnterTraceContext"/>.</summary>
		[System.ComponentModel.EditorBrowsable (System.ComponentModel.EditorBrowsableState.Never)]
		public static void LeaveTraceContext (string previous) => traceContext = previous;

		/// <summary>Object tracing only: "Type.Method" for each target of a delegate.</summary>
		[System.ComponentModel.EditorBrowsable (System.ComponentModel.EditorBrowsableState.Never)]
		public static string DescribeDelegate (Delegate handler)
		{
			if (handler == null)
				return "none";

			return string.Join (", ", handler.GetInvocationList ().Select (d => $"{d.Method.DeclaringType?.FullName}.{d.Method.Name}"));
		}

		// Object tracing only: set by GetObject while it creates a wrapper for an existing native object.
		[ThreadStatic]
		static string wrapKind;

		// How a wrapper came to exist, which decides how it should be released:
		//   constructed   - "new X ()" in managed code: Dispose it, or use 'using'.
		//   returned      - a native function returned a new object owned by the caller: Dispose it.
		//   getter        - wrapper for an object owned elsewhere (StyleContext, Selection, ContentArea, ...):
		//                   dispose it once, in the owner's Dispose.
		//   builder       - a Gtk.Builder object or [Builder.Object] field: dispose it in Dispose.
		//   signal-arg, callback-arg, vfunc-arg
		//                 - created while marshaling arguments, with no application code on the stack:
		//                   keep the object in a field or dispose it through its owner.
		static string TraceKind (string rawStack, IEnumerable<string> frames)
		{
			if (wrapKind == null)
				return "constructed";

			if (rawStack.Contains ("Gtk.Builder."))
				return "builder";

			if (traceContext != null && !HasApplicationFrame (frames)) {
				int space = traceContext.IndexOf (' ');
				return (space > 0 ? traceContext.Substring (0, space) : traceContext) + "-arg";
			}

			return wrapKind;
		}

		void TraceConstruction ()
		{
			if (!IsTracingObjects) return;
			if (handle == IntPtr.Zero) return;

			string rawStack = Environment.StackTrace;
			var frames = rawStack.Split ('\n').Skip (2).Where (x => filter.All (y => !x.Contains (y))).ToList ();
			string context = traceContext == null ? string.Empty : $"   during {traceContext}\n";
			string trace = $"{GetType ()} {handle.ToInt64()} [{TraceKind (rawStack, frames)}] ->\n" + context + string.Join ("\n", frames) + "\n";

			if (IsRecordingObjectTrace)
				ObjectTraceRecordingNew.Add ((handle, trace));

			if (TraceObjectConstruction)
				ObjectConstructionTraces.Add ((handle, trace));
		}

		public static bool ToggleObjectTraceRecording ()
		{
			if (!IsRecordingObjectTrace)
			{
				ObjectTraceRecordingNew.Clear();
				ObjectTraceRecordingRemoved.Clear();
				ObjectTraceRecordingFinalized.Clear();
				lock (UseAfterDisposeTraces)
					UseAfterDisposeTraces.Clear ();
			}

			return IsRecordingObjectTrace = !IsRecordingObjectTrace;
		}

		public static string ObjectTraceRecordingStatistics()
		{
			return $"Is currently recording: {IsRecordingObjectTrace}\n" +
			       $"Old objects removed (existing prior to start): {ObjectTraceRecordingRemoved.Count (x => ObjectTraceRecordingNew.All (y => y.Item1 != x.Item1))}\n" +
			       $"New objects: {ObjectTraceRecordingNew.Count}\n" +
			       $"Transient objects (create and freed during): {ObjectTraceRecordingRemoved.Count (x => ObjectTraceRecordingNew.Any (y => y.Item1 == x.Item1))}\n" +
			       $"Potentially leaked objects (create without freeing): {ObjectTraceRecordingDiff.Count}\n" +
			       $"Freed by the GC finalizer instead of Dispose: {ObjectTraceRecordingFinalized.Select (x => x.Item1).Distinct ().Count ()}";
		}

		public static string ObjectTraceRecordingReport()
		{
			var diff = ObjectTraceRecordingDiff;

			// Objects kept by a static field are alive by design; list them apart from the leaks.
			var staticHolders = FindStaticHolders (diff);
			var leaked = diff.Where (x => !staticHolders.ContainsKey (x.Item1)).ToList ();
			var held = diff.Where (x => staticHolders.ContainsKey (x.Item1)).ToList ();

			var groups = leaked.GroupBy (x => TraceSummaryKey (x.Item2))
				.OrderByDescending (g => g.Count ())
				.ToList ();
			var heldGroups = held.GroupBy (x => $"{TraceSummaryKey (x.Item2)}  held by static {staticHolders[x.Item1]}")
				.OrderByDescending (g => g.Count ())
				.ToList ();

			var report = new System.Text.StringBuilder ();
			report.Append ($"Is currently recording: {IsRecordingObjectTrace}\n");
			report.Append ($"Potientially leaked objects: {leaked.Count}\n");
			report.Append ($"Held by static fields: {held.Count}\n\n");

			report.Append ("Kinds: [constructed] new in managed code, [returned] owned object returned by a native call,\n" +
			               "       [getter] wrapper for an object owned elsewhere (dispose once in the owner's Dispose),\n" +
			               "       [builder] Gtk.Builder object, [signal-arg]/[callback-arg]/[vfunc-arg] created while marshaling arguments.\n\n");
			report.Append ($"Summary ({groups.Count} sources):\n");
			foreach (var g in groups)
				report.Append ($"{g.Count (),5} x {g.Key}\n");

			report.Append ($"\nHeld by static fields ({heldGroups.Count} sources): never released, as no instance owns them.\n" +
			               "Replace a static icon Pixbuf with an icon name; give other objects an owner that disposes them.\n");
			foreach (var g in heldGroups)
				report.Append ($"{g.Count (),5} x {g.Key}\n");

			report.Append ("\n").Append (UseAfterDisposeReport ());
			report.Append ("\n").Append (FinalizedReport ());
			report.Append ("\n").Append (HandlersOnDisposedObjectsReport ());

			report.Append ("\nOne example per source:\n\n");
			foreach (var g in groups)
				report.Append ($"[{g.Count ()} x] ").Append (g.First ().Item2).Append ("\n");
			foreach (var g in heldGroups)
				report.Append ($"[{g.Count ()} x, held by static {staticHolders[g.First ().Item1]}] ").Append (g.First ().Item2).Append ("\n");

			return report.ToString ();
		}

		// Frames from the bindings and the runtime; the first frame outside these is application code.
		static readonly string[] libraryFramePrefixes = {
			"GLib.", "Gtk.", "Gdk.", "Pango.", "Cairo.", "Atk.", "GtkSource.", "WebKit.",
			"System.", "Microsoft.", "Mono.", "InvokeStub_"
		};

		static IEnumerable<string> ApplicationFrames (IEnumerable<string> lines)
		{
			return lines
				.Select (l => l.Trim ())
				.Where (l => l.StartsWith ("at "))
				.Select (l => l.Substring (3))
				.Where (l => !libraryFramePrefixes.Any (p => l.StartsWith (p)))
				// The program entry point is below every main loop dispatch and says nothing about the source.
				.Where (l => !l.Contains (".Main(String[] args)"));
		}

		static bool HasApplicationFrame (IEnumerable<string> lines) => ApplicationFrames (lines).Any ();

		// Created on first use, so the type initializer does not compile a regex when tracing is off.
		static System.Text.RegularExpressions.Regex frameLocationCache;
		static System.Text.RegularExpressions.Regex frameLocation => frameLocationCache ??=
			new System.Text.RegularExpressions.Regex (@"\(.*?\)( in .*[\\/]([^\\/]+):line (\d+))?\s*$");

		// "Type  App.Frame (File.cs:12) <- Caller (File.cs:34) <- ..." or, when no application frame
		// is on the stack, the signal/callback the wrapper was created in.
		static string TraceSummaryKey (string trace)
		{
			var lines = trace.Split ('\n');
			var header = lines[0].Split (' ');
			string kind = header.FirstOrDefault (p => p.StartsWith ("["));
			string type = kind == null ? header[0] : $"{header[0]} {kind}";

			var appFrames = ApplicationFrames (lines)
				.Take (3)
				.Select (l => frameLocation.Replace (l, m => m.Groups[2].Success ? $" ({m.Groups[2].Value}:{m.Groups[3].Value})" : string.Empty))
				.ToList ();

			if (appFrames.Count > 0)
				return $"{type}  {string.Join (" <- ", appFrames)}";

			string during = lines.Select (l => l.Trim ()).FirstOrDefault (l => l.StartsWith ("during "));
			return during != null ? $"{type}  ({during})" : $"{type}  (no application frames)";
		}

		// Use after Dispose: while object tracing is active, a stack trace is recorded when Handle is
		// read on a wrapper that has been disposed, e.g. calling a method or removing an event handler.

		// Unique stack trace -> number of occurrences.
		public static Dictionary<string, int> UseAfterDisposeTraces = new Dictionary<string, int> ();

		// Callers that read Handle only to check whether the wrapper is still alive.
		static readonly HashSet<string> handleCheckCallers = new HashSet<string> {
			"Dispose", "Destroy", "Finalize", "LookupGType", "get_NativeType", "ToString", "Equals", "GetHashCode"
		};

		[System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		void OnZeroHandleAccess ()
		{
			if (disposed && IsTracingObjects)
				TraceUseAfterDisposeAccess ();
		}

		void TraceUseAfterDisposeAccess ()
		{
			var stack = new System.Diagnostics.StackTrace (1, true);

			// The first frame outside this method and the Handle getter (which the JIT may inline).
			var caller = stack.GetFrames ()
				.Select (f => f.GetMethod ())
				.FirstOrDefault (m => m != null && !(m.DeclaringType == typeof (Object) && (m.Name == "get_Handle" || m.Name == nameof (OnZeroHandleAccess) || m.Name == nameof (TraceUseAfterDisposeAccess))));
			if (caller == null || handleCheckCallers.Contains (caller.Name))
				return;

			string context = traceContext == null ? string.Empty : $"   during {traceContext}\n";
			string trace = $"{GetType ()} used after Dispose ->\n" + context +
				stack.ToString ().TrimEnd ().Split ('\n').Where (x => filter.All (y => !x.Contains (y))).Aggregate (string.Empty, (x, y) => x + y.TrimEnd () + "\n");

			bool first;
			lock (UseAfterDisposeTraces) {
				UseAfterDisposeTraces.TryGetValue (trace, out int count);
				first = count == 0;
				UseAfterDisposeTraces[trace] = count + 1;
			}

			// Print right away: a use after Dispose is often followed by a crash before a report is taken.
			if (first)
				Console.Error.WriteLine (trace);
		}

		// Objects created during recording that only the GC finalizer released: they are missing a
		// Dispose () and kept their native object alive until a collection happened to run.
		public static string FinalizedReport ()
		{
			var seen = new HashSet<IntPtr> ();
			var groups = ObjectTraceRecordingFinalized
				.Where (x => seen.Add (x.Item1))
				.GroupBy (x => TraceSummaryKey (x.Item2))
				.OrderByDescending (g => g.Count ())
				.ToList ();

			var report = new System.Text.StringBuilder ();
			report.Append ($"Freed by the GC finalizer instead of Dispose: {seen.Count} objects from {groups.Count} sources\n");
			foreach (var g in groups)
				report.Append ($"{g.Count (),5} x {g.Key}\n");
			foreach (var g in groups)
				report.Append ("\n").Append ($"[{g.Count ()} x] ").Append (g.First ().Item2);

			return report.ToString ();
		}

		// Signal handlers on live objects whose target (or the 'this' captured by a lambda) is a
		// disposed wrapper. The delegate keeps the disposed object, and everything it references,
		// reachable for as long as the emitting object lives.
		public static string HandlersOnDisposedObjectsReport ()
		{
			var found = new List<(string Key, string Detail)> ();

			List<ToggleRef> refs;
			lock (Objects)
				refs = Objects.Values.ToList ();

			foreach (var tref in refs) {
				Object emitter = tref.Target;
				if (emitter?.signals == null)
					continue;

				foreach (var signal in emitter.signals.Values.ToList ()) {
					foreach (var d in signal.ConnectedHandlers) {
						Object subscriber = d.Target as Object ?? CapturedThis (d.Target) as Object;
						if (subscriber == null || !subscriber.disposed)
							continue;

						string handler = $"{d.Method.DeclaringType?.FullName}.{d.Method.Name}";
						string key = $"signal '{signal.Name}' on {emitter.GetType ()} -> {handler} (disposed {subscriber.GetType ()})";
						string created = FindConstructionTrace (emitter.Handle) ?? $"{emitter.GetType ()} {emitter.Handle.ToInt64 ()} -> created before recording started\n";
						found.Add ((key, $"{key}\n   emitter: " + created));
					}
				}
			}

			var groups = found.GroupBy (x => x.Key).OrderByDescending (g => g.Count ()).ToList ();

			var report = new System.Text.StringBuilder ();
			report.Append ($"Handlers on disposed objects: {found.Count} handlers in {groups.Count} places\n");
			foreach (var g in groups)
				report.Append ($"{g.Count (),5} x {g.Key}\n");
			foreach (var g in groups)
				report.Append ("\n").Append ($"[{g.Count ()} x] ").Append (g.First ().Detail);

			return report.ToString ();
		}

		// Report only: which of the traced wrappers a static field holds, as "Type.field".
		// Only types whose code is on the wrappers' creation stacks are read: a cached static
		// (static X => x ??= ..., static readonly X = ..., a static constructor) created the object
		// from inside that type, and reading the fields of a type whose code already ran does not
		// start a type initializer that would otherwise not have run.
		static Dictionary<IntPtr, string> FindStaticHolders (List<(IntPtr, string)> traces)
		{
			var result = new Dictionary<IntPtr, string> ();
			var wanted = new Dictionary<object, IntPtr> (ReferenceComparer<object>.Instance);
			foreach (var (h, _) in traces) {
				var o = TryGetObject (h);
				if (o != null)
					wanted[o] = h;
			}
			if (wanted.Count == 0)
				return result;

			foreach (var type in TypesOnStacks (traces.Select (x => x.Item2))) {
				FieldInfo[] fields;
				try {
					fields = type.GetFields (BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				} catch {
					continue;
				}

				foreach (var field in fields) {
					var ft = field.FieldType;
					if (field.IsLiteral || ft.IsPrimitive || ft.IsEnum || ft == typeof (string) || ft.IsPointer)
						continue;

					object value;
					try {
						value = field.GetValue (null);
					} catch {
						continue;
					}

					foreach (var item in StaticFieldItems (value)) {
						if (item != null && wanted.TryGetValue (item, out IntPtr h) && !result.ContainsKey (h))
							result[h] = $"{type.FullName?.Replace ('+', '.')}.{field.Name}";
					}
				}
			}

			return result;
		}

		// The value itself and, one level down, the items of a collection or a created Lazy<T>.
		static IEnumerable<object> StaticFieldItems (object value)
		{
			if (value == null)
				yield break;

			yield return value;

			// A GLib object (e.g. a ListStore) can be enumerable; its rows are not what we look for.
			if (value is Object || value is string)
				yield break;

			var type = value.GetType ();
			if (type.IsGenericType && type.GetGenericTypeDefinition () == typeof (Lazy<>)) {
				object created = null;
				try {
					if ((bool) type.GetProperty ("IsValueCreated").GetValue (value))
						created = type.GetProperty ("Value").GetValue (value);
				} catch {
				}
				if (created != null)
					yield return created;
				yield break;
			}

			var items = new List<object> ();
			try {
				if (value is System.Collections.IDictionary dictionary) {
					foreach (System.Collections.DictionaryEntry entry in dictionary) {
						items.Add (entry.Key);
						items.Add (entry.Value);
					}
				} else if (value is System.Collections.IEnumerable enumerable) {
					foreach (var item in enumerable) {
						items.Add (item);
						if (items.Count >= 100000)
							break;
					}
				}
			} catch {
				// The collection changed or could not be enumerated; report what we have.
			}

			foreach (var item in items)
				yield return item;
		}

		// Application types (and their enclosing types) named in "at Type.Method(...)" frames.
		static HashSet<Type> TypesOnStacks (IEnumerable<string> traces)
		{
			var byName = new Dictionary<string, Type> ();
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies ()) {
				if (assembly.IsDynamic)
					continue;
				string name = assembly.GetName ().Name ?? string.Empty;
				if (name.StartsWith ("System") || name.StartsWith ("Microsoft") || name == "mscorlib" || name == "netstandard")
					continue;

				Type[] types;
				try {
					types = assembly.GetTypes ();
				} catch (ReflectionTypeLoadException e) {
					types = e.Types.Where (t => t != null).ToArray ();
				} catch {
					continue;
				}

				foreach (var t in types) {
					if (t.FullName != null && !t.ContainsGenericParameters)
						byName[t.FullName.Replace ('+', '.')] = t;
				}
			}

			var result = new HashSet<Type> ();
			foreach (var frame in ApplicationFrames (traces.SelectMany (t => t.Split ('\n')))) {
				int paren = frame.IndexOf ('(');
				string method = paren > 0 ? frame.Substring (0, paren) : frame;
				string typeName;
				if (method.EndsWith ("..ctor") || method.EndsWith ("..cctor"))
					typeName = method.Substring (0, method.LastIndexOf ("..", StringComparison.Ordinal));
				else {
					int dot = method.LastIndexOf ('.');
					if (dot <= 0)
						continue;
					typeName = method.Substring (0, dot);
				}

				if (!byName.TryGetValue (typeName, out Type type))
					continue;

				for (var t = type; t != null; t = t.DeclaringType)
					result.Add (t);
			}

			return result;
		}

		sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
		{
			public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T> ();
			public bool Equals (T x, T y) => ReferenceEquals (x, y);
			public int GetHashCode (T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode (obj);
		}

		// The 'this' of the method that created a lambda or local function, if it captured one.
		static object CapturedThis (object closure)
		{
			if (closure == null || closure is Object)
				return null;

			var field = closure.GetType ().GetFields (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.FirstOrDefault (f => f.Name.EndsWith ("__this"));
			return field?.GetValue (closure);
		}

		static string FindConstructionTrace (IntPtr h)
		{
			// The latest entry belongs to the current wrapper of the handle.
			string trace = ObjectTraceRecordingNew.LastOrDefault (x => x.Item1 == h).Item2;
			return trace ?? ObjectConstructionTraces.LastOrDefault (x => x.Item1 == h).Item2;
		}

		public static string UseAfterDisposeReport ()
		{
			List<KeyValuePair<string, int>> traces;
			lock (UseAfterDisposeTraces)
				traces = UseAfterDisposeTraces.OrderByDescending (x => x.Value).ToList ();

			var report = new System.Text.StringBuilder ();
			report.Append ($"Used after Dispose: {traces.Count} places\n");
			foreach (var t in traces)
				report.Append ($"{t.Value,5} x {TraceSummaryKey (t.Key)}\n");
			foreach (var t in traces)
				report.Append ("\n").Append ($"[{t.Value} x] ").Append (t.Key);

			return report.ToString ();
		}

		public static bool WarnOnFinalize { get; set; }
		public static bool TraceObjectConstruction { get; set; }

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr d_g_object_ref(IntPtr raw);
		static d_g_object_ref g_object_ref = FuncLoader.LoadFunction<d_g_object_ref>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_ref"));
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void d_g_object_unref(IntPtr raw);
		static d_g_object_unref g_object_unref = FuncLoader.LoadFunction<d_g_object_unref>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_unref"));
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate bool d_g_object_is_floating(IntPtr raw);
		static d_g_object_is_floating g_object_is_floating = FuncLoader.LoadFunction<d_g_object_is_floating>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_is_floating"));

		public static Object TryGetObject (IntPtr o)
		{
			if (o == IntPtr.Zero)
				return null;

			ToggleRef toggle_ref;
			lock (Objects) {
				if (!Objects.TryGetValue (o, out toggle_ref)) {
					return null;
				}
			}

			if (toggle_ref != null) {
				return toggle_ref.Target;
			}

			return null;
		}

		public static Object GetObject(IntPtr o, bool owned_ref)
		{
			if (o == IntPtr.Zero)
				return null;

			Object obj = null;

			ToggleRef toggle_ref;
			lock (Objects) {
				if (Objects.TryGetValue (o, out toggle_ref)) {
					if (toggle_ref != null)
						obj = toggle_ref.Target;
				}
			}

			if (obj != null && obj.Handle == o) {
				if (owned_ref)
					g_object_unref (obj.Handle);
				return obj;
			}

			bool unexpected_owned_floating = false;
			// If we don't get an owned reference here then we need to increase the
			// reference count as CreateObject() always takes an owned reference.
			// If we get a floating reference passed, however, then we assume that
			// we actually own it and have to sink the floating reference, which
			// will happen in the setter for Raw later.
			if (!owned_ref && !g_object_is_floating(o))
				g_object_ref (o);
			else if (owned_ref && g_object_is_floating(o))
				unexpected_owned_floating = true;

			if (IsTracingObjects) {
				string previousKind = wrapKind;
				wrapKind = owned_ref ? "returned" : "getter";
				try {
					obj = GLib.ObjectManager.CreateObject(o);
				} finally {
					wrapKind = previousKind;
				}
			} else {
				obj = GLib.ObjectManager.CreateObject(o);
			}
			if (obj == null) {
				g_object_unref (o);
				return null;
			}

			if (unexpected_owned_floating)
				Console.Error.WriteLine ("Unexpected owned floating reference of " + obj.GetType() + " instance. This will be leaked");

			return obj;
		}

		public static Object GetObject(IntPtr o)
		{
			return GetObject (o, false);
		}

		//  Key: The Type for the set of properties
		//  Value->SubKey: The pointer to the ParamSpec of the property
		//  Value->SubValue: The corresponding PropertyInfo object
		static Dictionary<Type, Dictionary<IntPtr, PropertyInfo>> properties;
		static Dictionary<Type, Dictionary<IntPtr, PropertyInfo>> Properties {
			get {
				if (properties == null)
					properties = new Dictionary<Type, Dictionary<IntPtr, PropertyInfo>> ();
				return properties;
			}
		}

		static Dictionary<IntPtr, Dictionary<Type, PropertyInfo>> interface_properties;
		static Dictionary<IntPtr, Dictionary<Type, PropertyInfo>> IProperties {
			get {
				if (interface_properties == null)
					interface_properties = new Dictionary<IntPtr, Dictionary<Type, PropertyInfo>> ();
				return interface_properties;
			}
		}

		struct GTypeClass {
			public IntPtr gtype;
		}

		struct GObjectClass {
			public GTypeClass type_class;
			public IntPtr construct_props;
			public ConstructorDelegate constructor_cb;
			public SetPropertyDelegate set_prop_cb;
			public GetPropertyDelegate get_prop_cb;
			public IntPtr dispose;
			public IntPtr finalize;
			public IntPtr dispatch_properties_changed;
			public IntPtr notify;
			public IntPtr constructed;
			public IntPtr dummy1;
			public IntPtr dummy2;
			public IntPtr dummy3;
			public IntPtr dummy4;
			public IntPtr dummy5;
			public IntPtr dummy6;
			public IntPtr dummy7;
		}

		internal class ClassInitializer {

			internal Type Type { get; private set; }
			internal bool HandlersOverriden { get; private set; }
			internal GType.ClassInitDelegate ClassInitManagedDelegate { get; private set; }

			uint idx = 1;
			bool is_first_subclass;
			private GType gtype;
			private List<GInterfaceAdapter> adapters = new List<GInterfaceAdapter> ();

			internal ClassInitializer (Type type)
			{
				ClassInitManagedDelegate = this.ClassInit;
				Type = type;
				gtype = GType.RegisterGObjectType (this);
				is_first_subclass = gtype.GetBaseType () == gtype.GetThresholdType ();
			}

			internal GType Init ()
			{
				AddGInterfaces ();
				gtype.EnsureClass (); //calls class_init

				ConnectDefaultHandlers ();
				InvokeTypeInitializers ();
				AddInterfaceProperties ();
				return gtype;
			}

			private void AddGInterfaces ()
			{
				foreach (Type iface in Type.GetInterfaces ()) {
					if (!iface.IsDefined (typeof (GInterfaceAttribute), true))
						continue;

					GInterfaceAttribute attr = iface.GetCustomAttributes (typeof (GInterfaceAttribute), false) [0] as GInterfaceAttribute;
					GInterfaceAdapter adapter = Activator.CreateInstance (attr.AdapterType, null) as GInterfaceAdapter;

					if (!iface.IsAssignableFrom (Type.BaseType)) {
						GInterfaceInfo info = adapter.Info;
						info.Data = gtype.Val;
						g_type_add_interface_static (gtype.Val, adapter.GInterfaceGType.Val, ref info);
						adapters.Add (adapter);
					}
				}
			}

			private void ClassInit (IntPtr gobject_class_handle)
			{
				bool override_ctor = is_first_subclass;

				bool override_props = is_first_subclass || adapters.Count > 0;

				OverrideHandlers (gobject_class_handle, override_ctor, override_props);

				foreach (GInterfaceAdapter adapter in adapters) {
					InitializeProperties (adapter, gobject_class_handle);
				}

				AddProperties (gobject_class_handle);
			}

			private void InitializeProperties (GInterfaceAdapter adapter, IntPtr gobject_class_handle)
			{
				foreach (PropertyInfo pinfo in adapter.GetType ().GetProperties (BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
					foreach (object attr in pinfo.GetCustomAttributes (typeof (PropertyAttribute), false)) {
						if (pinfo.GetIndexParameters ().Length > 0)
							throw new InvalidOperationException (String.Format ("Property {0} of type {1} cannot be overriden because its GLib.PropertyAttribute is expected to have one or more indexed parameters",
							                                                    pinfo.Name, adapter.GetType ().FullName));

						PropertyAttribute property_attr = attr as PropertyAttribute;
						if (property_attr != null) {
							OverrideProperty (gobject_class_handle, property_attr.Name);
						}
					}
				}
			}

			void OverrideProperty (IntPtr declaring_class, string name)
			{
				Object.OverrideProperty (declaring_class, idx++, name);
				idx++;
			}

			private void OverrideHandlers (bool ctor, bool properties)
			{
				OverrideHandlers (gtype.GetClassPtr (), ctor, properties);
			}

			private void OverrideHandlers (IntPtr gobject_class_handle, bool ctor, bool properties)
			{
				if (HandlersOverriden || (ctor == false && properties == false))
					return;

				GObjectClass gobject_class = (GObjectClass)Marshal.PtrToStructure (gobject_class_handle, typeof(GObjectClass));
				if (ctor) {
					gobject_class.constructor_cb = GLib.Object.ConstructorHandler;
				}
				if (properties) {
					gobject_class.get_prop_cb = GLib.Object.GetPropertyHandler;
					gobject_class.set_prop_cb = GLib.Object.SetPropertyHandler;
				}
				Marshal.StructureToPtr (gobject_class, gobject_class_handle, false);
				HandlersOverriden = true;
			}

			void AddProperties (IntPtr gobject_class_handle)
			{
				if (is_first_subclass) {
					ParamSpec pspec = new ParamSpec ("gtk-sharp-managed-instance", "", "", GType.Pointer, ParamFlags.Writable | ParamFlags.ConstructOnly);
					g_object_class_install_property (gobject_class_handle, idx, pspec.Handle);
					idx++;
				}

				foreach (PropertyInfo pinfo in Type.GetProperties (BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
					foreach (object attr in pinfo.GetCustomAttributes (typeof (PropertyAttribute), false)) {
						if (pinfo.GetIndexParameters ().Length > 0)
							throw new InvalidOperationException(String.Format("GLib.RegisterPropertyAttribute cannot be applied to property {0} of type {1} because the property expects one or more indexed parameters",
							                                                  pinfo.Name, Type.FullName));

						OverrideHandlers (false, true);

						PropertyAttribute property_attr = attr as PropertyAttribute;
						try {
							IntPtr param_spec = RegisterProperty (gtype, property_attr.Name, property_attr.Nickname, property_attr.Blurb, idx, (GType) pinfo.PropertyType, pinfo.CanRead, pinfo.CanWrite);
							Type type = (Type)gtype;
							Dictionary<IntPtr, PropertyInfo> gtype_properties;
							if (!Properties.TryGetValue (type, out gtype_properties)) {
								gtype_properties = new Dictionary<IntPtr, PropertyInfo> ();
								Properties [type] = gtype_properties;
							}
							gtype_properties.Add (param_spec, pinfo);
							idx++;
						} catch (ArgumentException) {
							throw new InvalidOperationException (String.Format ("GLib.PropertyAttribute cannot be applied to property {0} of type {1} because the return type of the property is not supported",
							                                                    pinfo.Name, Type.FullName));
						}
					}
				}
			}

			void AddInterfaceProperties ()
			{
				foreach (Type iface in Type.GetInterfaces ()) {
					if (!iface.IsDefined (typeof (GInterfaceAttribute), true))
						continue;

					GInterfaceAttribute attr = iface.GetCustomAttributes (typeof (GInterfaceAttribute), false) [0] as GInterfaceAttribute;
					GInterfaceAdapter adapter = Activator.CreateInstance (attr.AdapterType, null) as GInterfaceAdapter;

					foreach (PropertyInfo p in iface.GetProperties ()) {
						PropertyAttribute[] attrs = p.GetCustomAttributes (typeof (PropertyAttribute), true) as PropertyAttribute [];
						if (attrs.Length == 0)
							continue;
						PropertyAttribute property_attr = attrs [0];
						PropertyInfo declared_prop = Type.GetProperty (p.Name, BindingFlags.Public | BindingFlags.Instance);
						if (declared_prop == null)
							continue;
						IntPtr param_spec = FindInterfaceProperty (adapter.GInterfaceGType, property_attr.Name);

						Dictionary<IntPtr, PropertyInfo> props;
						if (!Properties.TryGetValue (Type, out props)) {
							props = new Dictionary<IntPtr, PropertyInfo> ();
							Properties [Type] = props;
						}
						props [param_spec] = declared_prop;
					}
				}
			}

			void ConnectDefaultHandlers ()
			{
				foreach (MethodInfo minfo in Type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
					MethodInfo baseinfo = minfo.GetBaseDefinition ();
					if (baseinfo == minfo)
						continue;

					foreach (object attr in baseinfo.GetCustomAttributes (typeof (DefaultSignalHandlerAttribute), false)) {
						DefaultSignalHandlerAttribute sigattr = attr as DefaultSignalHandlerAttribute;
						MethodInfo connector = sigattr.Type.GetMethod (sigattr.ConnectionMethod, BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof (GType) }, new ParameterModifier [0]);
						object[] parms = new object [1];
						parms [0] = gtype;
						connector.Invoke (null, parms);
						break;
					}
				}
			}

			void InvokeTypeInitializers ()
			{
				object[] parms = {gtype, Type};

				BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;

				foreach (TypeInitializerAttribute tia in Type.GetCustomAttributes (typeof (TypeInitializerAttribute), true)) {
					MethodInfo m = tia.Type.GetMethod (tia.MethodName, flags);
					if (m != null)
						m.Invoke (null, parms);
				}
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr ConstructorDelegate (IntPtr gtype, uint n_construct_properties, IntPtr construct_properties);

		static ConstructorDelegate constructor_handler;

		static ConstructorDelegate ConstructorHandler {
			get {
				if (constructor_handler == null)
					constructor_handler = new ConstructorDelegate (ConstructorCallback);
				return constructor_handler;
			}
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr d_g_param_spec_get_name(IntPtr pspec);
		static d_g_param_spec_get_name g_param_spec_get_name = FuncLoader.LoadFunction<d_g_param_spec_get_name>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_param_spec_get_name"));

		static IntPtr ConstructorCallback (IntPtr gtypeval, uint n_construct_properties, IntPtr construct_properties)
		{
			GType gtype = new GLib.GType (gtypeval);
			GObjectClass threshold_class = (GObjectClass) Marshal.PtrToStructure (gtype.GetThresholdType ().GetClassPtr (), typeof (GObjectClass));
			IntPtr raw = threshold_class.constructor_cb (gtypeval, n_construct_properties, construct_properties);
			Dictionary<IntPtr, GLib.Value> deferred;

			GLib.Object obj = null;
			for (int i = 0; i < n_construct_properties; i++) {
				IntPtr p = new IntPtr (construct_properties.ToInt64 () + i * 2 * IntPtr.Size);

				string prop_name = Marshaller.Utf8PtrToString (g_param_spec_get_name (Marshal.ReadIntPtr (p)));
				if (prop_name != "gtk-sharp-managed-instance")
					continue;

				Value val = (Value) Marshal.PtrToStructure (Marshal.ReadIntPtr (p, IntPtr.Size), typeof (Value));
				if ((IntPtr) val.Val != IntPtr.Zero) {
					GCHandle gch = (GCHandle) (IntPtr) val.Val;
					obj = (GLib.Object) gch.Target;
					obj.Raw = raw;
					break;
				}
			}

			if (obj == null)
				obj = GetObject (raw, false);

			if(PropertiesToSet.TryGetValue(raw, out deferred)) {
				foreach(var item in deferred) {
					SetDeferredProperty(obj, item.Value, item.Key);
				}
				PropertiesToSet.Remove(raw);
			}
			return raw;
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void d_g_object_class_override_property(IntPtr klass, uint prop_id, IntPtr name);
		static d_g_object_class_override_property g_object_class_override_property = FuncLoader.LoadFunction<d_g_object_class_override_property>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_class_override_property"));

		public static void OverrideProperty (IntPtr oclass, uint property_id, string name)
		{
			IntPtr native_name = GLib.Marshaller.StringToPtrGStrdup (name);
			g_object_class_override_property (oclass, property_id, native_name);
			GLib.Marshaller.Free (native_name);
		}

		[Obsolete ("Use OverrideProperty(oclass,property_id,name)")]
		public static void OverrideProperty (IntPtr declaring_class, string name)
		{
			OverrideProperty (declaring_class, idx, name);
			idx++;
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr d_g_object_class_find_property(IntPtr klass, IntPtr name);
		static d_g_object_class_find_property g_object_class_find_property = FuncLoader.LoadFunction<d_g_object_class_find_property>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_class_find_property"));

		static IntPtr FindClassProperty (GLib.Object o, string name)
		{
			IntPtr gobjectclass = Marshal.ReadIntPtr (o.Handle);
			IntPtr native_name = GLib.Marshaller.StringToPtrGStrdup (name);
			try
			{
				return g_object_class_find_property (gobjectclass, native_name);
			}
			finally
			{
				GLib.Marshaller.Free (native_name);
			}
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr d_g_object_interface_find_property(IntPtr klass, IntPtr name);
		static d_g_object_interface_find_property g_object_interface_find_property = FuncLoader.LoadFunction<d_g_object_interface_find_property>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_interface_find_property"));

		static IntPtr FindInterfaceProperty (GType type, string name)
		{
			IntPtr g_iface = type.GetDefaultInterfacePtr ();
			IntPtr native_name = GLib.Marshaller.StringToPtrGStrdup (name);
			try
			{
				return g_object_interface_find_property (g_iface, native_name);
			}
			finally
			{
				GLib.Marshaller.Free (native_name);
			}
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void d_g_object_class_install_property(IntPtr klass, uint prop_id, IntPtr param_spec);
		static d_g_object_class_install_property g_object_class_install_property = FuncLoader.LoadFunction<d_g_object_class_install_property>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_class_install_property"));

		static IntPtr RegisterProperty (GType type, string name, string nick, string blurb, uint property_id, GType property_type, bool can_read, bool can_write)
		{
			IntPtr declaring_class = type.GetClassPtr ();
			ParamSpec pspec = new ParamSpec (name, nick, blurb, property_type, can_read, can_write);

			g_object_class_install_property (declaring_class, property_id, pspec.Handle);
			return pspec.Handle;
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void GetPropertyDelegate (IntPtr GObject, uint property_id, ref GLib.Value value, IntPtr pspec);

		static void GetPropertyCallback (IntPtr handle, uint property_id, ref GLib.Value value, IntPtr param_spec)
		{
			GLib.Object obj = GLib.Object.GetObject (handle, false);
			var type = (Type)obj.LookupGType ();

			Dictionary<IntPtr, PropertyInfo> props;
			if (!Properties.TryGetValue (type, out props))
				return;

			PropertyInfo prop;
			if (!props.TryGetValue (param_spec, out prop))
				return;

			value.Val = prop.GetValue (obj, new object [0]);
		}

		static GetPropertyDelegate get_property_handler;
		static GetPropertyDelegate GetPropertyHandler {
			get {
				if (get_property_handler == null)
					get_property_handler = new GetPropertyDelegate (GetPropertyCallback);
				return get_property_handler;
			}
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void SetPropertyDelegate (IntPtr GObject, uint property_id, ref GLib.Value value, IntPtr pspec);

		static void SetPropertyCallback(IntPtr handle, uint property_id, ref GLib.Value value, IntPtr param_spec)
		{
			// There are multiple issues in this place.
			// We cannot construct an object here as it can be in construction
			// from ConstructorCallback thus managed object already created.
			//
			// We cannot use the "gtk-sharp-managed-instance" property as when
			// constructed by Gtk.Builder it is set to null.
			//
			// We defer setting the properties to later time when
			// we have unmanaged and managed objects paired.
			GLib.Object obj = TryGetObject(handle);
			if(obj != null) {
				SetDeferredProperty(obj, value, param_spec);
				return;
			}
			Dictionary<IntPtr, GLib.Value> deferred;
			if(!PropertiesToSet.TryGetValue(handle, out deferred)) {
				deferred = new Dictionary<IntPtr, GLib.Value>();
				PropertiesToSet.Add(handle, deferred);
			}
			deferred[param_spec] = value;
		}

		static void SetDeferredProperty(GLib.Object obj, GLib.Value value, IntPtr param_spec)
		{
			var type = (Type)obj.LookupGType ();

			Dictionary<IntPtr, PropertyInfo> props;
			if (!Properties.TryGetValue (type, out props))
				return;

			PropertyInfo prop;
			if (!props.TryGetValue (param_spec, out prop))
				return;

			prop.SetValue (obj, value.Val, new object [0]);
		}

		static SetPropertyDelegate set_property_handler;
		static SetPropertyDelegate SetPropertyHandler {
			get {
				if (set_property_handler == null)
					set_property_handler = new SetPropertyDelegate (SetPropertyCallback);
				return set_property_handler;
			}
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void d_g_type_add_interface_static(IntPtr gtype, IntPtr iface_type, ref GInterfaceInfo info);
		static d_g_type_add_interface_static g_type_add_interface_static = FuncLoader.LoadFunction<d_g_type_add_interface_static>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_type_add_interface_static"));

		protected internal static GType RegisterGType (System.Type t)
		{
			//this is a deprecated way of tracking a property counter,
			//but we may still need it for backwards compatibility
			idx = 1;

			return new ClassInitializer (t).Init ();
		}

		protected GType LookupGType ()
		{
			if (Handle != IntPtr.Zero) {
				GTypeInstance obj = (GTypeInstance) Marshal.PtrToStructure (Handle, typeof (GTypeInstance));
				GTypeClass klass = (GTypeClass) Marshal.PtrToStructure (obj.g_class, typeof (GTypeClass));
				return new GLib.GType (klass.gtype);
			} else {
				return LookupGType (GetType ());
			}
		}

		protected internal static GType LookupGType (System.Type t)
		{
			return GType.LookupGObjectType (t);
		}

		protected Object (IntPtr raw)
		{
			Raw = raw;

			TraceConstruction ();
		}

		protected Object ()
		{
			CreateNativeObject (new string [0], new GLib.Value [0]);
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr d_g_object_new(IntPtr gtype, IntPtr dummy);
		static d_g_object_new g_object_new = FuncLoader.LoadFunction<d_g_object_new>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_new"));

		struct GParameter {
			public IntPtr name;
			public GLib.Value val;
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr d_g_object_newv(IntPtr gtype, int n_params, GParameter[] parms);
		static d_g_object_newv g_object_newv = FuncLoader.LoadFunction<d_g_object_newv>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_newv"));

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void d_g_object_ref_sink(IntPtr raw);
		static d_g_object_ref_sink g_object_ref_sink = FuncLoader.LoadFunction<d_g_object_ref_sink>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_ref_sink"));

		protected virtual void CreateNativeObject (string[] names, GLib.Value[] vals)
		{
			GType gtype = LookupGType ();
			bool is_managed_subclass = GType.IsManaged (gtype);
			GParameter[] parms = new GParameter [is_managed_subclass ? names.Length + 1 : names.Length];
			for (int i = 0; i < names.Length; i++) {
				parms [i].name = GLib.Marshaller.StringToPtrGStrdup (names [i]);
				parms [i].val = vals [i];
			}

			if (is_managed_subclass) {
				GCHandle gch = GCHandle.Alloc (this);
				parms[names.Length].name = GLib.Marshaller.StringToPtrGStrdup ("gtk-sharp-managed-instance");
				parms[names.Length].val = new GLib.Value ((IntPtr) gch);
				Raw = g_object_newv (gtype.Val, parms.Length, parms);
				gch.Free ();
			} else {
				Raw = g_object_newv (gtype.Val, parms.Length, parms);
			}

			foreach (GParameter p in parms)
				GLib.Marshaller.Free (p.name);

			TraceConstruction ();
		}

		protected virtual IntPtr Raw {
			get {
				return handle;
			}
			set {
				if (handle == value)
					return;

				lock (Objects) {
					if (handle != IntPtr.Zero) {
						Objects.Remove (handle);
						if (tref != null) {
							tref.Dispose ();
							tref = null;
						}
					}

					// All references that we get here are assumed to be owned by us. If we
					// get a floating reference then we should take ownership of it by
					// sinking it.
					if (value != IntPtr.Zero && g_object_is_floating(value)) {
						g_object_ref_sink(value);
					}

					handle = value;
					if (value != IntPtr.Zero) {
						tref = new ToggleRef (this);
						Objects [value] = tref;
					}

					TraceConstruction ();
				}
			}
		}

		public static GLib.GType GType {
			get { return GType.Object; }
		}

		protected string TypeName {
			get { return NativeType.ToString (); }
		}

		public GLib.GType NativeType {
			get { return LookupGType (); }
		}

		internal ToggleRef ToggleRef {
			get { return tref; }
		}

		public IntPtr Handle {
			get {
				// Keep the hot path tiny so the JIT still inlines it; a zero handle is the rare case.
				IntPtr h = handle;
				if (h == IntPtr.Zero)
					OnZeroHandleAccess ();
				return h;
			}
		}

		public IntPtr OwnedHandle {
			get { return g_object_ref (handle); }
		}

		public void AddNotification (string property, NotifyHandler handler)
		{
			AddSignalHandler ("notify::" + property, handler, typeof(NotifyArgs));
		}

		public void AddNotification (NotifyHandler handler)
		{
			AddSignalHandler ("notify", handler, typeof(NotifyArgs));
		}

		public void RemoveNotification (string property, NotifyHandler handler)
		{
			RemoveSignalHandler ("notify::" + property, handler);
		}

		public void RemoveNotification (NotifyHandler handler)
		{
			RemoveSignalHandler ("notify", handler);
		}

		public override int GetHashCode ()
		{
			return Handle.GetHashCode ();
		}

		System.Collections.Hashtable data;
		public System.Collections.Hashtable Data {
			get {
				if (data == null)
					data = new System.Collections.Hashtable ();

				return data;
			}
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void d_g_object_get_property(IntPtr obj, IntPtr name, ref GLib.Value val);
		static d_g_object_get_property g_object_get_property = FuncLoader.LoadFunction<d_g_object_get_property>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_get_property"));

		public GLib.Value GetProperty (string name)
		{
			Value val = new Value (this, name);
			IntPtr native_name = GLib.Marshaller.StringToPtrGStrdup (name);
			g_object_get_property (Raw, native_name, ref val);
			GLib.Marshaller.Free (native_name);
			return val;
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void d_g_object_set_property(IntPtr obj, IntPtr name, ref GLib.Value val);
		static d_g_object_set_property g_object_set_property = FuncLoader.LoadFunction<d_g_object_set_property>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_set_property"));

		public void SetProperty (string name, GLib.Value val)
		{
			IntPtr native_name = GLib.Marshaller.StringToPtrGStrdup (name);
			g_object_set_property (Raw, native_name, ref val);
			GLib.Marshaller.Free (native_name);
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void d_g_object_notify(IntPtr obj, IntPtr property_name);
		static d_g_object_notify g_object_notify = FuncLoader.LoadFunction<d_g_object_notify>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_notify"));

		protected void Notify (string property_name)
		{
			IntPtr native_name = GLib.Marshaller.StringToPtrGStrdup (property_name);
			g_object_notify (Handle, native_name);
			GLib.Marshaller.Free (native_name);
		}

		public static List<Signal> PendingSignalFrees = new List<Signal> ();
		static bool idle_queued;

		Dictionary<string, Signal> signals;
		Dictionary<string, Signal> Signals {
			get {
				if (signals == null)
					signals = new Dictionary<string, Signal> ();
				return signals;
			}
		}

		public void AddSignalHandler (string name, Delegate handler)
		{
			AddSignalHandler (name, handler, typeof (EventArgs));
		}

		public void AddSignalHandler (string name, Delegate handler, Delegate marshaler)
		{
			Signal sig;
			if (!Signals.TryGetValue (name, out sig)) {
				sig = new Signal (this, name, marshaler);
				Signals [name] = sig;
			}

			sig.AddDelegate (handler);
		}

		public void AddSignalHandler (string name, Delegate handler, Type args_type)
		{
			if (args_type == null)
				args_type = handler.Method.GetParameters ()[1].ParameterType;

			Signal sig;
			if (!Signals.TryGetValue (name, out sig)) {
				sig = new Signal (this, name, args_type);
				Signals [name] = sig;
			}

			sig.AddDelegate (handler);
		}

		public void RemoveSignalHandler (string name, Delegate handler)
		{
			Signal sig;
			if (Signals.TryGetValue (name, out sig))
				sig.RemoveDelegate (handler);
		}

		public void QueueSignalFree ()
		{
			lock (PendingSignalFrees) {
				PendingSignalFrees.AddRange (signals.Values);
				if (!idle_queued){
					Timeout.Add (50, new TimeoutHandler (PerformQueuedSignalFrees));
					idle_queued = true;
				}
			}
		}

		static bool PerformQueuedSignalFrees ()
		{
			Signal[] qsignals;

			lock (PendingSignalFrees){
				qsignals = new Signal[PendingSignalFrees.Count];
				PendingSignalFrees.CopyTo (qsignals, 0);
				PendingSignalFrees.Clear ();
				idle_queued = false;
			}

			foreach (Signal s in qsignals)
				s.Free ();

			return false;
		}

		protected static void OverrideVirtualMethod (GType gtype, string name, Delegate cb)
		{
			Signal.OverrideDefaultHandler (gtype, name, cb);
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		protected delegate void d_g_signal_chain_from_overridden(IntPtr args, ref GLib.Value retval);
		protected static d_g_signal_chain_from_overridden g_signal_chain_from_overridden = FuncLoader.LoadFunction<d_g_signal_chain_from_overridden>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_signal_chain_from_overridden"));
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate bool d_g_type_check_instance_is_a(IntPtr obj, IntPtr gtype);
		static d_g_type_check_instance_is_a g_type_check_instance_is_a = FuncLoader.LoadFunction<d_g_type_check_instance_is_a>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_type_check_instance_is_a"));

		internal static bool IsObject (IntPtr obj)
		{
			return g_type_check_instance_is_a (obj, GType.Object.Val);
		}

		public struct GTypeInstance {
			public IntPtr g_class;
		}

		public struct GObject {
			public GTypeInstance type_instance;
			public uint ref_count;
			public IntPtr qdata;
		}

		protected int RefCount {
			get {
				GObject native = (GObject) Marshal.PtrToStructure (Handle, typeof (GObject));
				return (int) native.ref_count;
			}
		}

		internal void Harden ()
		{
			tref.Harden ();
		}

		static Object ()
		{
			if (Environment.GetEnvironmentVariable ("GTK_SHARP_DEBUG") != null)
				GLib.Log.SetLogHandler ("GLib-GObject", GLib.LogLevelFlags.All, new GLib.LogFunc (GLib.Log.PrintTraceLogFunction));
		}

		// Internal representation of the wrapped ABI structure.
		static public unsafe AbiStruct abi_info = new AbiStruct(new List<AbiField> {
				new GLib.AbiField("g_type_instance"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, null
					, "ref_count"
					, (long) Marshal.OffsetOf<GObject_g_type_instanceAlign>("g_type_instance")
					, 0
					),
				new GLib.AbiField("ref_count"
					, -1
					, (uint) sizeof (uint) // ref_count
					, "g_type_instance"
					, "qdata"
					, (long) Marshal.OffsetOf<GObject_ref_countAlign>("ref_count")
					, 0
					),
				new GLib.AbiField("qdata"
					, -1
					, (uint) sizeof (IntPtr) // qdata
					, "ref_count"
					, null
					, (long) Marshal.OffsetOf<GObject_qdataAlign>("qdata")
					, 0
					),
			}
		);
		//
		// Internal representation of the wrapped ABI structure.
		static public unsafe AbiStruct class_abi = new AbiStruct(new List<AbiField> {
				new GLib.AbiField("type_class"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, null
					, "construct_props"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("construct_props"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "type_class"
					, "constructor_cb"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("constructor_cb"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "construct_props"
					, "set_prop_cb"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("set_prop_cb"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "constructor_cb"
					, "get_prop_cb"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("get_prop_cb"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "set_prop_cb"
					, "dispose"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dispose"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "get_prop_cb"
					, "finalize"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("finalize"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "dispose"
					, "dispatch_properties_changed"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dispatch_properties_changed"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "finalize"
					, "notify"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("notify"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "dispatch_properties_changed"
					, "constructed"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("constructed"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "notify"
					, "dummy1"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dummy1"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "constructed"
					, "dummy2"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dummy2"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "dummy1"
					, "dummy3"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dummy3"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "dummy2"
					, "dummy4"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dummy3"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "dummy2"
					, "dummy4"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dummy4"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "dummy3"
					, "dummy5"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dummy5"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "dummy4"
					, "dummy6"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dummy6"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "dummy5"
					, "dummy7"
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
				new GLib.AbiField("dummy7"
					, 0
					, (uint) sizeof (IntPtr) // g_type_instance
					, "dummy6"
					, null
					, (uint) sizeof (IntPtr) // g_type_instance
					, 0
					),
			}
		);

		[StructLayout(LayoutKind.Sequential)]
		public struct GObject_g_type_instanceAlign
		{
			sbyte f1;
			private IntPtr g_type_instance;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct GObject_ref_countAlign
		{
			sbyte f1;
			private uint ref_count;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct GObject_qdataAlign
		{
			sbyte f1;
			private IntPtr qdata;
		}
		// End of the ABI representation.
	}
}
