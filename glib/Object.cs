// Object.cs - GObject class wrapper implementation
//
// Author: Bob Smith <bob@thestuff.net>
//
// (c) 2001 Bob Smith

namespace Glib {

	using System;
	using System.Runtime.InteropServices;

	public class Object  {
		public Object(IntPtr o)
		{
			Object=o;
		}
		protected IntPtr _obj;

		private IntPtr Object
		{
			get
			{
				return _obj;
			}
			set
			{
				if (value == null) throw new ArgumentNullException ();
				_objectManager = null;
				_obj = value;
			}
		}       

		protected ObjectManager _objectManager;

		private ObjectManager ObjectManager
		{
			get
			{
				if (_objectManager == null)
				{
					IntPtr o = Data["gobject#-object-manager"];
					if (o == null) _objectManager = new ObjectManager(_obj);
					else _objectManager = o;
				}
				return _objectManager;
			}
		}       
		protected EventHandlerList Events
		{
			get
			{
				return ObjectManager.Events;
			}
		}
		[DllImport("gtk-1.3")]
		static extern IntPtr g_object_get_data (
					IntPtr object,
					String key );
		[DllImport("gtk-1.3")]
		static extern void g_object_set_data (
					IntPtr object,
					String key,
					IntPtr data );

		public IntPtr Data [String key]
		{
			get
			{
				return g_object_get_data (Object, key);
			}
			set
			{
				g_object_set_data (Object, key, value);
			}
		}

/*

void        (*GObjectGetPropertyFunc)       (GObject *object,
                                             guint property_id,
                                             GValue *value,
                                             GParamSpec *pspec);
void        (*GObjectSetPropertyFunc)       (GObject *object,
                                             guint property_id,
                                             const GValue *value,
                                             GParamSpec *pspec);
void        (*GObjectFinalizeFunc)          (GObject *object);
#define     G_TYPE_IS_OBJECT (type)
#define     G_OBJECT (object)
#define     G_IS_OBJECT (object)
#define     G_OBJECT_CLASS (class)
#define     G_IS_OBJECT_CLASS (class)
#define     G_OBJECT_GET_CLASS (object)
#define     G_OBJECT_TYPE (object)
#define     G_OBJECT_TYPE_NAME (object)
#define     G_OBJECT_CLASS_TYPE (class)
#define     G_OBJECT_CLASS_NAME (class)
#define     G_VALUE_HOLDS_OBJECT (value)
void        g_object_class_install_property (GObjectClass *oclass,
                                             guint property_id,
                                             GParamSpec *pspec);
GParamSpec* g_object_class_find_property (GObjectClass *oclass,
                                             const gchar *property_name);
GParamSpec** g_object_class_list_properties (GObjectClass *oclass,
                                             guint *n_properties);
gpointer g_object_new (GType object_type,
                                             const gchar *first_property_name,
                                             ...);
gpointer g_object_newv (GType object_type,
                                             guint n_parameters,
                                             GParameter *parameters);
gpointer g_object_ref (gpointer object);
void        g_object_unref (gpointer object);
void        g_object_weak_ref (GObject *object,
                                             GWeakNotify notify,
                                             gpointer data);
void        g_object_weak_unref (GObject *object,
                                             GWeakNotify notify,
                                             gpointer data);
void        g_object_add_weak_pointer (GObject *object,
                                             gpointer *weak_pointer_location);
void        g_object_remove_weak_pointer (GObject *object,
                                             gpointer *weak_pointer_location);
gpointer g_object_connect (gpointer object,
                                             const gchar *signal_spec,
                                             ...);
void        g_object_disconnect (gpointer object,
                                             const gchar *signal_spec,
                                             ...);
void        g_object_set (gpointer object,
                                             const gchar *first_property_name,
                                             ...);
void        g_object_get (gpointer object,
                                             const gchar *first_property_name,
                                             ...);
void        g_object_notify (GObject *object,
                                             const gchar *property_name);
void        g_object_freeze_notify (GObject *object);
void        g_object_thaw_notify (GObject *object);
void        g_object_set_data_full (GObject *object,
                                             const gchar *key,
                                             gpointer data,
                                             GDestroyNotify destroy);
gpointer g_object_steal_data (GObject *object,
                                             const gchar *key);
gpointer g_object_get_qdata (GObject *object,
                                             GQuark quark);
void        g_object_set_qdata (GObject *object,
                                             GQuark quark,
                                             gpointer data);
void        g_object_set_qdata_full (GObject *object,
                                             GQuark quark,
                                             gpointer data,
                                             GDestroyNotify destroy);
gpointer g_object_steal_qdata (GObject *object,
                                             GQuark quark);
void        g_object_set_property (GObject *object,
                                             const gchar *property_name,
                                             const GValue *value);
void        g_object_get_property (GObject *object,
                                             const gchar *property_name,
                                             GValue *value);
GObject*    g_object_new_valist (GType object_type,
                                             const gchar *first_property_name,
                                             va_list var_args);
void        g_object_set_valist (GObject *object,
                                             const gchar *first_property_name,
                                             va_list var_args);
void        g_object_get_valist (GObject *object,
                                             const gchar *first_property_name,
                                             va_list var_args);
void        g_object_watch_closure (GObject *object,
                                             GClosure *closure);
void        g_object_run_dispose (GObject *object);
void        g_value_set_object (GValue *value,
                                             gpointer v_object);
gpointer g_value_get_object (const GValue *value);
GObject*    g_value_dup_object (const GValue *value);
#define     G_OBJECT_WARN_INVALID_PROPERTY_ID(object, property_id, pspec)
*/

	}
}
