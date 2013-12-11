﻿using Neon.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Neon.Serialization {
    /// <summary>
    /// Provides a view of an arbitrary type that unifies a number of discrete concepts in the CLR.
    /// Arrays and Collection types have special support, but their APIs are unified by the
    /// TypeModel so that they can be treated as if they were a regular type.
    /// </summary>
    public class TypeModel {
        /// <summary>
        /// Creates a new instance of the type that this model points back to.
        /// </summary>
        /// <remarks>
        /// Activator.CreateInstance cannot be used because TypeModel can point to an Array.
        /// </remarks>
        public object CreateInstance() {
            if (IsArray) {
                // we have to start with a size zero array otherwise it will have invalid data
                // inside of it
                return Array.CreateInstance(ElementType, 0);
            }

            try {
                return Activator.CreateInstance(ReflectedType);
            }
            catch (MissingMethodException e) {
                throw new InvalidOperationException("Unable to create instance of " + ReflectedType.FullName + "; there is no default constructor", e);
            }
            catch (TargetInvocationException e) {
                throw new InvalidOperationException("Constructor of " + ReflectedType.FullName + " threw an exception when creating an instance", e);
            }
            catch (MemberAccessException e) {
                throw new InvalidOperationException("Unable to access constructor of " + ReflectedType.FullName, e);
            }
        }

        /// <summary>
        /// Appends a value to the end of the array or collection. If we are modeling an array, then
        /// the value is inserted at indexHint, which *should* be equal to ((Array)context).Length.
        /// </summary>
        public void AppendValue(ref object context, object value, int indexHint) {
            if (IsArray) {
                Array array = (Array)context;

                // If we don't have storage in the allocated array, then allocate storage.
                if (indexHint >= array.Length) {
                    Array newArray = Array.CreateInstance(ElementType, indexHint + 1);
                    Array.Copy(array, newArray, array.Length);
                    array = newArray;
                }

                // Assign the array index
                array.SetValue(value, indexHint);
                context = array;
            }
            else if (IsCollection) {
                _collectionAddMethod.Invoke(context, new object[] { value });
            }
            else {
                throw new InvalidOperationException("Cannot assign a list slot to a non-list type");
            }
        }

        /// <summary>
        /// Initializes a new instance of the TypeModel class from a type. Use TypeCache to get
        /// instances of TypeModel; do not use this constructor directly.
        /// </summary>
        internal TypeModel(Type type) {
            ReflectedType = type;

            // Iterate over all attributes in the type to check for the requirement of a custom
            // converter or if it needs to support cyclic references
            foreach (var attribute in ReflectedType.GetCustomAttributes(inherit: true)) {
                if (attribute is SerializationSupportCyclicReferencesAttribute) {
                    SupportsCyclicReferences = true;
                }
            }

            // Determine if the type needs to support inheritance
            SupportsInheritance = type.IsInterface || type.IsAbstract;
            if (!SupportsInheritance) {
                foreach (var attribute in ReflectedType.GetCustomAttributes(inherit: false)) {
                    if (attribute is SerializationSupportInheritance) {
                        SupportsInheritance = true;
                    }
                }
            }

            // But do not support it if inheritance is explicitly denied
            if (SupportsInheritance) {
                foreach (var attribute in ReflectedType.GetCustomAttributes(inherit: true)) {
                    if (attribute is SerializationNoAutoInheritance) {
                        SupportsInheritance = false;
                    }
                }
            }

            // determine if we are a collection or array; recall that arrays implement the
            // ICollection interface, however

            IsArray = type.IsArray;
            IsCollection = IsArray == false && type.IsImplementationOf(typeof(ICollection<>));

            // If we're a collection or array, get the generic type definition so that client code
            // can determine how to deserialize child elements
            if (IsCollection) {
                Type collectionType = type.GetInterface(typeof(ICollection<>));

                _elementType = collectionType.GetGenericArguments()[0];
                _collectionAddMethod = collectionType.GetMethod("Add");
                if (_collectionAddMethod == null) {
                    throw new InvalidOperationException("Unable to get Add method for type " + collectionType);
                }
            }
            else if (IsArray) {
                _elementType = type.GetElementType();
            }

            // If we're not one of those three types, then we will be using Properties to assign
            // data to ourselves, so we want to lookup said information
            else {
                HashSet<PropertyModel> properties = new HashSet<PropertyModel>();
                CollectProperties(type, properties);
                _properties = new List<PropertyModel>(properties);
            }
        }

        /// <summary>
        /// Recursive method that adds all of the properties and fields from the given type into the
        /// given list.
        /// </summary>
        /// <param name="type">The type to process. This method will recurse up the type's
        /// inheritance hierarchy</param>
        /// <param name="properties">The list of properties that should be appended to</param>
        private static void CollectProperties(Type type, HashSet<PropertyModel> properties) {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

            foreach (PropertyInfo property in type.GetProperties(flags)) {
                // We don't serialize delegates
                if (typeof(Delegate).IsAssignableFrom(property.PropertyType)) {
                    Log<TypeModel>.Info("Ignoring property {0}.{1} because it is a delegate",
                        type.FullName, property.Name);
                    continue;
                }

                // We don't serialize properties marked with [NonSerialized] or [NotSerializable]
                foreach (var attribute in property.GetCustomAttributes(true)) {
                    if (attribute is NonSerializedAttribute || attribute is NotSerializableAttribute) {
                        Log<TypeModel>.Info("Ignoring property {0}.{1} because it has a " +
                            "NonSerialized or a NotSerializable attribute", type.FullName,
                            property.Name);
                        goto loop_end;
                    }
                }

                // If the property cannot be both read and written to, we don't serialize it
                if (property.CanRead == false || property.CanWrite == false) {
                    Log<TypeModel>.Info("Ignoring property {0}.{1} because it cannot both be " +
                        "read from and written to", type.FullName, property.Name);
                    continue;
                }

                // If the property is named "Item", it might be the this[int] indexer, which in that
                // case we don't serialize it We cannot just compare with "Item" because of explicit
                // interfaces, where the name of the property will be the full method name.
                if (property.Name.EndsWith("Item")) {
                    ParameterInfo[] parameters = property.GetIndexParameters();
                    if (parameters.Length == 1) {
                        goto loop_end;
                    }
                }

                properties.Add(new PropertyModel(property));

            loop_end: { }
            }

            foreach (FieldInfo field in type.GetFields(flags)) {
                // We don't serialize delegates
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) {
                    Log<TypeModel>.Info("Ignoring field {0}.{1} because it is a delegate",
                        type.FullName, field.Name);
                    continue;
                }

                // We don't serialize non-serializable properties
                if (field.IsNotSerialized) {
                    Log<TypeModel>.Info("Ignoring field {0}.{1} because it is marked " +
                        "NoNSerialized", type.FullName, field.Name);
                    continue;
                }

                // We don't serialize fields marked with [NonSerialized] or [NotSerializable]
                foreach (var attribute in field.GetCustomAttributes(true)) {
                    if (attribute is NonSerializedAttribute || attribute is NotSerializableAttribute) {
                        Log<TypeModel>.Info("Ignoring field {0}.{1} because it has a " +
                            "NonSerialized or a NotSerializable attribute", type.FullName,
                            field.Name);
                        goto loop_end;
                    }
                }

                // We don't serialize compiler generated fields (an example would be a backing field
                // for an automatically generated property).
                if (field.IsDefined(typeof(CompilerGeneratedAttribute), false)) {
                    continue;
                }

                properties.Add(new PropertyModel(field));

            loop_end: { }
            }

            if (type.BaseType != null) {
                CollectProperties(type.BaseType, properties);
            }
        }

        /// <summary>
        /// Does this type need to support cyclic references?
        /// </summary>
        public bool SupportsCyclicReferences {
            get;
            private set;
        }

        /// <summary>
        /// If this type needs to support inheritance in serialization/deserialization, then this
        /// will be true.
        /// </summary>
        public bool SupportsInheritance {
            get;
            private set;
        }

        /// <summary>
        /// The type that this model is modeling, ie, the type that the model was constructed off
        /// of.
        /// </summary>
        public Type ReflectedType {
            get;
            private set;
        }

        /// <summary>
        /// Iff this model maps back to a Collection or an Array type, then this is the type of
        /// element stored inside the array or collection. Otherwise, this method throws an
        /// exception.
        /// </summary>
        public Type ElementType {
            get {
                if (IsCollection == false && IsArray == false) {
                    throw new InvalidOperationException("Unable to get the ElementType of a " +
                        "type model object that is not a collection or an array");
                }

                return _elementType;
            }
        }
        private Type _elementType;

        /// <summary>
        /// True if the base type is a collection. If true, accessing Properties will throw an
        /// exception.
        /// </summary>
        public bool IsCollection {
            get;
            private set;
        }

        /// <summary>
        /// The cached Add method in ICollection[T]. This only contains a value if IsCollection is
        /// true.
        /// </summary>
        private MethodInfo _collectionAddMethod;

        /// <summary>
        /// True if the base type is an array. If true, accessing Properties will throw an
        /// exception.
        /// </summary>
        public bool IsArray {
            get;
            private set;
        }

        /// <summary>
        /// The properties on the type. This is used when importing/exporting a type that does not
        /// have a user-defined importer/exporter.
        /// </summary>
        public List<PropertyModel> Properties {
            get {
                if (IsCollection || IsArray) {
                    throw new InvalidOperationException("A type that is a collection or an array " +
                        "does not have properties (for the model on type " + ReflectedType + ")");
                }

                return _properties;
            }
        }
        private List<PropertyModel> _properties;
    }
}