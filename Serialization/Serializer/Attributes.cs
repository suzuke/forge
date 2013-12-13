﻿using System;

namespace Neon.Serialization {
    /// <summary>
    /// Changes the serialization format of an object so that it will support cyclic references.
    /// This annotation has a large change on the serialization format (making it less clear) and
    /// has potential performance implications, and additionally requires the converter to
    /// export/import metadata, so try to minimize the number of types which need to support cyclic
    /// references.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true,
        AllowMultiple = false)]
    public sealed class SerializationSupportCyclicReferencesAttribute : Attribute { }

    /// <summary>
    /// A type that has a [SerializationSupportInheritance] attribute will cause an automatic
    /// importer and exporter to be registered for the type which will allow for correct
    /// serialization and deserialization of derived types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface,
        Inherited = false, AllowMultiple = false)]
    public sealed class SerializationSupportInheritance : Attribute { }

    /// <summary>
    /// Annotate a type with this class to *not* automatically inject inheritance support if the
    /// type is abstract or an interface.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface,
        Inherited = false, AllowMultiple = false)]
    public sealed class SerializationNoAutoInheritance : Attribute { }

    /// <summary>
    /// Marks that a specified field/type/etc should not be serialized. This is equivalent to
    /// [NonSerialized] except it can be applied to a significantly larger number of types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct |
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event,
        Inherited = true, AllowMultiple = false)]
    public sealed class NotSerializableAttribute : Attribute { }
}