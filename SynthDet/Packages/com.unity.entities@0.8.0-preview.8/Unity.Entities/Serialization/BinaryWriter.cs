#if !NET_DOTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Properties;

[assembly: InternalsVisibleTo("Unity.Scenes.Hybrid")]
namespace Unity.Entities.Serialization
{
    static class BoxedProperties
    {
        internal class ReadBoxedStructForwarder : IContainerTypeCallback
        {
            public object Container;
            public PropertiesBinaryReader reader;

            public void Invoke<T>()
            {
                var value = default(T);
                PropertyContainer.Visit(ref value, reader);
                Container = value;
            }
        }

       internal class ReadBoxedClassForwarder : IContainerTypeCallback
        {
            public object Container;
            public PropertiesBinaryReader reader;

            public void Invoke<T>()
            {
                var value = (T) Activator.CreateInstance(typeof(T));
                PropertyContainer.Visit(ref value, reader);
                Container = value;
            }
        }

        public static object ReadBoxedStruct(Type type, PropertiesBinaryReader reader)
        {
            var forwarder = new ReadBoxedStructForwarder {reader = reader, Container = null};
            var propertyBag = PropertyBagResolver.Resolve(type);

            propertyBag.Cast(ref forwarder);

            return forwarder.Container;
        }

        public static object ReadBoxedClass(Type type, PropertiesBinaryReader reader)
        {
            var forwarder = new ReadBoxedClassForwarder {reader = reader, Container = null};
            var propertyBag = PropertyBagResolver.Resolve(type);

            propertyBag.Cast(ref forwarder);

            return forwarder.Container;
        }

        public static void WriteBoxedType<TVisitor>(object container, TVisitor visitor)
            where TVisitor : IPropertyVisitor
        {
            var changeTracker = new ChangeTracker();
            var resolved = PropertyBagResolver.Resolve(container.GetType());
            if (resolved != null)
            {
                resolved.Accept(ref container, ref visitor, ref changeTracker);
            }
            else
                throw new ArgumentException("Not supported");
        }
    }

    internal struct DictionaryContainer<TKey, TValue>
    {
        public List<TKey> Keys;
        public List<TValue> Values;

        public void PopulateDictionary(Dictionary<TKey, TValue> dict)
        {
            Assertions.Assert.AreEqual(Keys.Count, Values.Count);

            for (int i = 0; i < Keys.Count; ++i)
            {
                dict.Add(Keys[i], Values[i]);
            }
        }
    }

    internal struct PolymorphicTypeContainer<TValue>
    {
        public TValue Value;
    }

    internal struct PolymorphicTypeName
    {
        public string Name;
    }
    
    class BasePropertyVisitor : PropertyVisitor
    {
        internal static readonly uint kMagicNull = 0xDEADC0DE;
        internal static readonly uint kMagicPolymorphic = 0xABCACBAC;

        protected Stack<bool> _ProcessingCollectionStack;
        protected Stack<bool> _ProcessingElementStack;

        protected BasePropertyVisitor()
        {
            _ProcessingCollectionStack = new Stack<bool>();
            _ProcessingElementStack = new Stack<bool>();
        }
    }

    unsafe class PropertiesBinaryWriter : BasePropertyVisitor
    {
        protected BinaryPrimitiveWriterAdapter PrimitiveWriter { get; }
        public ref UnsafeAppendBuffer Buffer
        {
            get { return ref *PrimitiveWriter.m_Buffer; }
        }


        // This whole file is marked as !NET_DOTS but once Unity.Properties is supported in NET_DOTS
        // that top-level ifdef will be removed but this inner ifdef should not be as this code is for hybrid only
#if !NET_DOTS
        private List<UnityEngine.Object> _ObjectTable = new List<UnityEngine.Object>();
        private Dictionary<UnityEngine.Object, int> _ObjectTableMap = new Dictionary<UnityEngine.Object, int>();

        public UnityEngine.Object[] GetObjectTable()
        {
            return _ObjectTable.ToArray();
        }

        protected void AppendObject(UnityEngine.Object obj)
        {
            int index = -1;
            if (obj != null)
            {
                if (!_ObjectTableMap.TryGetValue(obj, out index))
                {
                    index = _ObjectTable.Count;
                    _ObjectTableMap.Add(obj, index);
                    _ObjectTable.Add(obj);
                }
            }

            Buffer.Add(index);
        }
#endif

        public PropertiesBinaryWriter(UnsafeAppendBuffer* stream)
        {
            PrimitiveWriter = new BinaryPrimitiveWriterAdapter(stream);
            AddAdapter(PrimitiveWriter);
        }

        private void VisitDictionary<TKey, TValue>(ICollection<TKey> keys, ICollection<TValue> values)
        {
            var dictContainer = new DictionaryContainer<TKey, TValue>() { Keys = new List<TKey>(keys), Values = new List<TValue>(values) };
            PropertyContainer.Visit(ref dictContainer, this);
        }

        // In essence, simply forces a cast of the value type so we can visit it correctly
        private void VisitPolymorphicType<TValue>(TValue value)
        {
            var container = new PolymorphicTypeContainer<TValue>() { Value = value };
            PropertyContainer.Visit(ref container, this);
        }

        protected override VisitStatus BeginContainer<TProperty, TContainer, TValue>
            (TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            if (_ProcessingCollectionStack.Count > 0)
            {
                _ProcessingElementStack.Push(true);
            }
            
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(typeof(TValue)))
            {
                var dict = value as System.Collections.IDictionary;
                var keys = dict.Keys;
                var values = dict.Values;
                var tKey = typeof(TValue).GetGenericArguments()[0];
                var tValue = typeof(TValue).GetGenericArguments()[1];

                // Workaround to support Dictionaries since Unity.Properties doesn't contain a ReflectedDictionaryProperty nor is it currently setup 
                // to easily be extended to support Key-Value container types. As such we treat dictionaries as two list (keys and values) by 
                // making our own container type to visit which we populate with the two lists we care about
                var openMethod = typeof(PropertiesBinaryWriter).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(m => m.Name == "VisitDictionary");
                var closedMethod = openMethod.MakeGenericMethod(tKey, tValue);
                closedMethod.Invoke(this, new[] { keys, values });

                EndContainer(property, ref container, ref value, ref changeTracker);
                return VisitStatus.Override;
            }
#if !NET_DOTS
            else if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(TValue)))
            {
                AppendObject(value as UnityEngine.Object);
                EndContainer(property, ref container, ref value, ref changeTracker);
                return VisitStatus.Override;
            }
#endif
            else if (value == null)
            {
                Buffer.Add(kMagicNull);
                return VisitStatus.Override;
            }

            // Is the runtime type different than the field type, then we are dealing with a polymorphic type
            // and we need to persist enough information to reconstruct the correct type when deserializing
            var valueType = value.GetType();
            if (typeof(TValue) != valueType)
            {
                // If we are iterating over a list, don't write out the element type since we did that already
                // when constructing the list
                if (_ProcessingCollectionStack.Count == 0 || (_ProcessingCollectionStack.Count > 0 && _ProcessingElementStack.Count > 1))
                {
                    Buffer.Add(kMagicPolymorphic);
                    var typeName = new PolymorphicTypeName() { Name = valueType.AssemblyQualifiedName };
                    PropertyContainer.Visit(ref typeName, this);
                }
                
                var openMethod = typeof(PropertiesBinaryWriter).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(m => m.Name == "VisitPolymorphicType");
                var closedMethod = openMethod.MakeGenericMethod(valueType);
                closedMethod.Invoke(this, new object[] { value });

                // Override will skip EndContainer, and Handled will start visiting fields we already visited. Sigh...
                EndContainer(property, ref container, ref value, ref changeTracker);
                
                return VisitStatus.Override;
            }

            return base.BeginContainer(property, ref container, ref value, ref changeTracker);
        }
        
        protected override void EndContainer<TProperty, TContainer, TValue>(TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            if (_ProcessingCollectionStack.Count > 0)
            {
                _ProcessingElementStack.Pop();
            }
        }

        protected override VisitStatus BeginCollection<TProperty, TContainer, TValue>
            (TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            _ProcessingCollectionStack.Push(true);

            if (null == value)
            {
                Buffer.Add(kMagicNull);
                return VisitStatus.Override;
            }
            
            // Write out size of list required for deserializing later
            var count = property.GetCount(ref container);
            Buffer.Add(count);

            // If the collection contains polymorphic types, write out all the concrete names to the buffer
            // as we will reconstruct the list elements upon deserialization _before_ reading in the element's 
            // actual values (current restriction in Properties), so we need to know the element types up front
            // Since a collection could be a List<SomeBaseClass> and each element a concrete type, we need to check
            // all the elements to see if recording the element names is required. 
            var elementType = typeof(TValue);
            if (elementType.IsArray)
                elementType = elementType.GetElementType();
            else if(elementType.IsGenericType)
                elementType = elementType.GetGenericArguments()[0];
            else
                throw new ArgumentException($"Collection type {typeof(TValue)} is not supported.");
            
            bool shouldGetTypeNames = false;
            var list = (IList)value;
            for (int i = 0; i < count; ++i)
            {
                var element = list[i];
                if (element == null)
                    continue;
                
                var concreteType = element.GetType();
                if (concreteType != elementType)
                {
                    shouldGetTypeNames = true;
                    break;
                }
            }
            
            if (shouldGetTypeNames)
            {
                // Push a sentinel to know when deserializing if we should read the list element names or not
                Buffer.Add(kMagicPolymorphic);
                for (int i = 0; i < count; ++i)
                {
                    var element = list[i];
                    if(element == null)
                        Buffer.Add(kMagicNull);
                    else
                    {
                        var concreteType = element.GetType();
                        var typeName = new PolymorphicTypeName() { Name = concreteType.AssemblyQualifiedName };
                        PropertyContainer.Visit(ref typeName, this);
                    }
                }    
            }

            return VisitStatus.Handled;
        }
        
        protected override void EndCollection<TProperty, TContainer, TValue>(TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            _ProcessingCollectionStack.Pop();
        }
    }

    class PropertiesBinaryReader : BasePropertyVisitor
    {
        protected BinaryPrimitiveReaderAdapter _PrimitiveReader;

#if !NET_DOTS
        private UnityEngine.Object[] _ObjectTable;

        protected unsafe UnityEngine.Object ReadObject()
        {
            _PrimitiveReader.Buffer->ReadNext(out int index);
            if (index != -1)
                return _ObjectTable[index];
            else
                return null;
        }

        unsafe public PropertiesBinaryReader(UnsafeAppendBuffer.Reader* stream, UnityEngine.Object[] objectTable)
        {
            _PrimitiveReader = new BinaryPrimitiveReaderAdapter(stream);
            _ObjectTable = objectTable;
            AddAdapter(_PrimitiveReader);
        }
#else
        unsafe public PropertiesBinaryReader(UnsafeAppendBuffer.Reader* stream)
        {
            _PrimitiveReader = new BinaryPrimitiveReaderAdapter(stream);
            AddAdapter(_PrimitiveReader);
        }
#endif


        internal DictionaryContainer<TKey, TValue> VisitDictionary<TKey, TValue>(ICollection<TKey> keys, ICollection<TValue> values)
        {
            var dictContainer = new DictionaryContainer<TKey, TValue>() { Keys = new List<TKey>(keys), Values = new List<TValue>(values) };
            PropertyContainer.Visit(ref dictContainer, this);

            return dictContainer;
        }

        // In essence, simply forces a cast of the value type so we can visit it correctly
        internal TValue VisitPolymorphicType<TValue>(TValue value)
        {
            var container = new PolymorphicTypeContainer<TValue>() { Value = value };
            PropertyContainer.Visit(ref container, this);
            return container.Value;
        }
        
        unsafe protected override VisitStatus BeginContainer<TProperty, TContainer, TValue>
            (TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            if (_ProcessingCollectionStack.Count > 0)
            {
                _ProcessingElementStack.Push(true);
            }
            
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(typeof(TValue)))
            {
                if (InitializeNullIfFieldNotNull(ref value))
                {
                    return VisitStatus.Override;
                }

                var dict = value as System.Collections.IDictionary;
                var keys = dict.Keys;
                var values = dict.Values;
                var tKey = typeof(TValue).GetGenericArguments()[0];
                var tValue = typeof(TValue).GetGenericArguments()[1];

                // Workaround to support Dictionaries since Unity.Properties doesn't contain a ReflectedDictionaryProperty nor is it currently setup 
                // to easily be extended to support Key-Value container types. As such we treat dictionaries as two list (keys and values) by 
                // making our own container type to visit which we populate with the two lists we care about
                var openVisitMethod = typeof(PropertiesBinaryReader).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(m => m.Name == "VisitDictionary");
                var closedVisitMethod = openVisitMethod.MakeGenericMethod(tKey, tValue);
                var dictContainer = closedVisitMethod.Invoke(this, new[] { keys, values });

                // now fill our actual dict with the read in kv lists
                var closedDictContainerType = typeof(DictionaryContainer<,>).MakeGenericType(tKey, tValue);
                var populateMethod = closedDictContainerType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Single(m => m.Name == "PopulateDictionary");
                populateMethod.Invoke(dictContainer, new object[] { value });

                EndContainer(property, ref container, ref value, ref changeTracker);
                return VisitStatus.Override;
            }
#if !NET_DOTS
            else if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(TValue)))
            {
                if (_ObjectTable == null)
                    throw new ArgumentException("We are reading a UnityEngine.Object however no ObjectTable was provided to the PropertiesBinaryReader.");

                var unityObject = ReadObject();
                Unsafe.As<TValue, UnityEngine.Object>(ref value) = unityObject;
                
                EndContainer(property, ref container, ref value, ref changeTracker);
                return VisitStatus.Override;
            }
            else if (value == null || typeof(TValue) == typeof(object))
            {
                if (InitializeNullIfFieldNotNull(ref value))
                {
                    // The value really was null so just continue
                    return VisitStatus.Override;
                }
                // the value isn't null so fallthrough so we can try visiting it
            }

            // Is the runtime type different than the field type, then we are dealing with a polymorphic type
            // and we need to persist enough information to reconstruct the correct type when deserializing
            var valueType = value.GetType();
            if (typeof(TValue) != valueType)
            {
                var openMethod = typeof(PropertiesBinaryReader).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(m => m.Name == "VisitPolymorphicType");
                var closedMethod = openMethod.MakeGenericMethod(valueType);
                value = (TValue) closedMethod.Invoke(this, new object[] { value });

                // Override will skip EndContainer, and Handled will start visiting fields we already visited. Sigh...
                EndContainer(property, ref container, ref value, ref changeTracker);

                return VisitStatus.Override;
            }
#endif

            return base.BeginContainer(property, ref container, ref value, ref changeTracker);
        }

        protected override void EndContainer<TProperty, TContainer, TValue>(TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            if (_ProcessingCollectionStack.Count > 0)
            {
                _ProcessingElementStack.Pop();
            }
        }
        
        unsafe protected override VisitStatus BeginCollection<TProperty, TContainer, TValue>
            (TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            _ProcessingCollectionStack.Push(true);

            if (CheckSentinel(_PrimitiveReader, kMagicNull))
            {
                return VisitStatus.Override;
            }
            
            // Unity.Properties doesn't really support class types so we need to workaround
            // this issue for now by prefilling our list with instances which will be later replaced.
            // Properties assumes a value type (non-null) value will already be there to write 
            // and if not will try to create a default value (which will be null for class types)
            _PrimitiveReader.Buffer->ReadNext(out int count);
            
            var polyList = count != 0 && CheckSentinel(_PrimitiveReader, kMagicPolymorphic);
            
            var type = typeof(TValue);
            if (type.IsArray)
            {
                var tValue = type.GetElementType();
                Array array;
                array = Array.CreateInstance(tValue, count);

                if (polyList)
                {
                    for (int i = 0; i < count; ++i)
                    {
                        // annoyingly we need to check if an element in our polymorphic list was null (in which case we
                        // couldn't resolve the actual concrete type
                        if (CheckSentinel(_PrimitiveReader, kMagicNull))
                        {
                            array.SetValue(null, i);
                        }
                        else
                        {
                            var typeName = new PolymorphicTypeName();
                            PropertyContainer.Visit(ref typeName, this);
                            var concreteType = Type.GetType(typeName.Name);

                            if (!concreteType.IsValueType &&
                                !concreteType.GetConstructors().Any(c => c.GetParameters().Count() == 0))
                            {
                                throw new ArgumentException(
                                    $"All class component types must be default constructable. '{concreteType.FullName}' is missing a default constructor.");
                            }

                            array.SetValue(Activator.CreateInstance(concreteType), i);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < count; ++i)
                    {
                        // Strings are immutable so we need to give a value when creating them
                        if (typeof(string) == tValue)
                        {
                            array.SetValue(Activator.CreateInstance(tValue, "".ToCharArray()), i);
                        }
                        else if (typeof(UnityEngine.Object).IsAssignableFrom(tValue))
                        {
                            // do nothing
                        }
                        else
                        {
                            if (!tValue.IsValueType &&
                                !tValue.GetConstructors().Any(c => c.GetParameters().Count() == 0))
                            {
                                throw new ArgumentException(
                                    $"All class component types must be default constructable. '{tValue.FullName}' is missing a default constructor.");
                            }

                            array.SetValue(Activator.CreateInstance(tValue), i);
                        }
                    }   
                }

                value = (TValue) (object) array;
            }
            else if(type.IsGenericType)
            {
                var tValue = type.GetGenericArguments()[0];
                System.Collections.IList list;
                if (null == value)
                {
                    list = (System.Collections.IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(tValue));
                }
                else
                {
                    list = value as System.Collections.IList;
                }

                if (polyList)
                {
                    for (int i = 0; i < count; ++i)
                    {
                        // annoyingly we need to check if an element in our polymorphic list was null (in which case we
                        // couldn't resolve the actual concrete type
                        if (CheckSentinel(_PrimitiveReader, kMagicNull))
                        {
                            list.Add(null);
                        }
                        else
                        {
                            var typeName = new PolymorphicTypeName();
                            PropertyContainer.Visit(ref typeName, this);
                            var concreteType = Type.GetType(typeName.Name);

                            if (!concreteType.IsValueType &&
                                !concreteType.GetConstructors().Any(c => c.GetParameters().Count() == 0))
                            {
                                throw new ArgumentException(
                                    $"All class component types must be default constructable. '{concreteType.FullName}' is missing a default constructor.");
                            }

                            list.Add(Activator.CreateInstance(concreteType));
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < count; ++i)
                    {
                        // Strings are immutable so we need to give a value when creating them
                        if (typeof(string) == tValue)
                            list.Add(Activator.CreateInstance(tValue, "".ToCharArray()));
                        else if (typeof(UnityEngine.Object).IsAssignableFrom(tValue))
                        {
                            list.Add(null);
                        }
                        else
                        {
                            if (!tValue.IsValueType &&
                                !tValue.GetConstructors().Any(c => c.GetParameters().Count() == 0))
                            {
                                throw new ArgumentException(
                                    $"All class component types must be default constructable. '{tValue.FullName}' is missing a default constructor.");
                            }

                            list.Add(Activator.CreateInstance(tValue));
                        }
                    }
                }

                value = (TValue) list;
            }

            property.SetValue(ref container, value);
            property.SetCount(ref container, count);

            return VisitStatus.Handled;
        }
        
        protected override void EndCollection<TProperty, TContainer, TValue>(TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            _ProcessingCollectionStack.Pop();
        }

        unsafe bool CheckSentinel(BinaryPrimitiveReaderAdapter reader, uint expected)
        {
            var offset = _PrimitiveReader.Buffer->Offset;
            reader.Buffer->ReadNext(out uint sentinel);

            if (sentinel == expected)
                return true;
            
            _PrimitiveReader.Buffer->Offset = offset;
            return false;
        }

        // As properties iterates over containers, if the container doesn't default construct it's fields but we 
        // have serialized out field data for those containers check for that data (absence of our null sentinel) 
        // and then return a default constructed container to be filled by said data
        unsafe bool InitializeNullIfFieldNotNull<TValue>(ref TValue value)
        {
            bool isActuallyNull = true;
            var oldOffset = _PrimitiveReader.Buffer->Offset;
            var sentinel = _PrimitiveReader.Buffer->ReadNext<uint>();
            if (sentinel != kMagicNull)
            {
                isActuallyNull = false;
                if (sentinel == kMagicPolymorphic)
                {
                    var typeName = new PolymorphicTypeName();
                    PropertyContainer.Visit(ref typeName, this);
                    var concreteType = Type.GetType(typeName.Name);
                    value = (TValue)Activator.CreateInstance(concreteType);
                }
                else
                {
                    // We didn't read a sentinel value so reset the offset to what it was before
                    _PrimitiveReader.Buffer->Offset = oldOffset;
                    value = Activator.CreateInstance<TValue>();
                }
            }

            return isActuallyNull;
        }
    }

    /// <summary>
    /// No need to ever use this class or call its functions. These only exist to be invoked in generated code paths
    /// (which themselves will never be called at runtime) only to hint to the Ahead Of Time compiler which types
    /// to generate specialized function bodies for.
    /// </summary>
    internal static class AOTFunctionGenerator
    {
        public static unsafe void GenerateAOTFunctions<TProperty, TContainer, TValue>()
            where TProperty : IProperty<TContainer, TValue>
        {
            TProperty property = default(TProperty);
            TContainer container = default(TContainer);
            ChangeTracker changeTracker = default(ChangeTracker);
            
            UnsafeAppendBuffer.Reader* reader = null;
            var propertyReader = new PropertiesBinaryReader(reader, null);

            propertyReader.VisitProperty<TProperty, TContainer, TValue>(property, ref container, ref changeTracker);
            propertyReader.IsExcluded<TProperty, TContainer, TValue>(property, ref container);
            Properties.AOTFunctionGenerator.GenerateAOTContainerFunctions<DictionaryContainer<object, object>>();
            Properties.AOTFunctionGenerator.GenerateAOTContainerFunctions<PolymorphicTypeName>();
            Properties.AOTFunctionGenerator.GenerateAOTContainerFunctions<PolymorphicTypeContainer<TContainer>>();
            Properties.AOTFunctionGenerator.GenerateAOTContainerFunctions<PolymorphicTypeContainer<TValue>>();
        }
        
        public static unsafe void GenerateAOTCollectionFunctions<TProperty, TContainer, TValue>()
            where TProperty : ICollectionProperty<TContainer, TValue>
        {
            TProperty property = default(TProperty);
            TContainer container = default(TContainer);
            ChangeTracker changeTracker = default(ChangeTracker);
            
            UnsafeAppendBuffer.Reader* reader = null;
            var propertyReader = new PropertiesBinaryReader(reader, null);

            propertyReader.VisitProperty<TProperty, TContainer, TValue>(property, ref container, ref changeTracker);
            propertyReader.VisitCollectionProperty<TProperty, TContainer, TValue>(property, ref container, ref changeTracker);
            propertyReader.IsExcluded<TProperty, TContainer, TValue>(property, ref container);
            Properties.AOTFunctionGenerator.GenerateAOTContainerFunctions<DictionaryContainer<object, object>>();
            Properties.AOTFunctionGenerator.GenerateAOTContainerFunctions<PolymorphicTypeName>();
            Properties.AOTFunctionGenerator.GenerateAOTContainerFunctions<PolymorphicTypeContainer<TContainer>>();
            Properties.AOTFunctionGenerator.GenerateAOTContainerFunctions<PolymorphicTypeContainer<TValue>>();
        }
    }
}
#endif