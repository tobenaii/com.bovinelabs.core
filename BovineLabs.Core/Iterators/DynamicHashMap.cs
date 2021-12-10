﻿// <copyright file="DynamicHashMap.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Core.Iterators
{
    using System;
    using System.Diagnostics;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using CollectionHelper = Unity.Collections.CollectionHelper;

    public interface IDynamicHashMap<TKey, TValue> : IBufferElementData
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
        byte Value { get; }
    }

    public static class IDynamicHashMapExtension
    {
        public static DynamicHashMap<TBuffer, TKey, TValue> AsHashMap<TBuffer, TKey, TValue>(this DynamicBuffer<TBuffer> buffer)
            where TBuffer : struct, IDynamicHashMap<TKey, TValue>
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            return new DynamicHashMap<TBuffer, TKey, TValue>(buffer);
        }

        internal static unsafe DynamicHashMapData* AsData<TBuffer, TKey, TValue>(this DynamicBuffer<TBuffer> buffer)
            where TBuffer : struct, IDynamicHashMap<TKey, TValue>
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            return (DynamicHashMapData*)buffer.GetUnsafePtr();
        }
    }

    public unsafe struct DynamicHashMap<TBuffer, TKey, TValue>
        where TBuffer : struct, IDynamicHashMap<TKey, TValue>
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
        private DynamicBuffer<TBuffer> data;

        internal DynamicHashMap(DynamicBuffer<TBuffer> buffer)
        {
            CheckSize(buffer);

            this.data = buffer;

            // First time, need to setup
            if (buffer.Length == 0)
            {
                this.Allocate();
            }
        }

        /// <summary> Reports whether container is empty. </summary>
        /// <value>True if this container empty.</value>
        public bool IsEmpty => !this.IsCreated || DynamicHashMapData.IsEmpty(this.Buffer);

        public bool IsCreated => this.data.IsCreated;

        /// <summary> The number of items that can fit in the container. </summary>
        /// <value>The number of items that the container can hold before it resizes its internal storage.</value>
        /// <remarks>Capacity specifies the number of items the container can currently hold. You can change Capacity
        /// to fit more or fewer items. Changing Capacity creates a new array of the specified size, copies the
        /// old array to the new one, and then deallocates the original array memory.</remarks>
        public int Capacity
        {
            get => this.data.AsData<TBuffer, TKey, TValue>()->KeyCapacity;
            set => DynamicHashMapData.ReallocateHashMap<TBuffer, TKey, TValue>(this.data, value, UnsafeHashMapData.GetBucketSize(value), out _);
        }

        private DynamicHashMapData* Buffer => this.data.AsData<TBuffer, TKey, TValue>();

        /// <summary> The current number of items in the container. </summary>
        /// <returns>The item count.</returns>
        public int Count() => DynamicHashMapData.GetCount(this.Buffer);

        /// <summary>
        /// Clears the container.
        /// </summary>
        /// <remarks>Containers capacity remains unchanged.</remarks>
        public void Clear()
        {
            DynamicHashMapBase<TBuffer, TKey, TValue>.Clear(this.data);
        }

        /// <summary>
        /// Try adding an element with the specified key and value into the container. If the key already exist, the value won't be updated.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="item">The value of the element to add.</param>
        /// <returns>Returns true if value is added into the container, otherwise returns false.</returns>
        public bool TryAdd(TKey key, TValue item)
        {
            return DynamicHashMapBase<TBuffer, TKey, TValue>.TryAdd(this.data, key, item, false);
        }

        /// <summary>
        /// Add an element with the specified key and value into the container.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="item">The value of the element to add.</param>
        public void Add(TKey key, TValue item)
        {
            this.TryAdd(key, item);
        }

        /// <summary>
        /// Removes the element with the specified key from the container.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>Returns true if the key was removed from the container, otherwise returns false indicating key wasn't in the container.</returns>
        public bool Remove(TKey key)
        {
            return DynamicHashMapBase<TBuffer, TKey, TValue>.Remove(this.data, key, false) != 0;
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="item">If key is found item parameter will contain value</param>
        /// <returns>Returns true if key is found, otherwise returns false.</returns>
        public bool TryGetValue(TKey key, out TValue item)
        {
            return DynamicHashMapBase<TBuffer, TKey, TValue>.TryGetFirstValueAtomic(this.Buffer, key, out item, out _);
        }

        /// <summary>
        /// Determines whether an key is in the container.
        /// </summary>
        /// <param name="key">The key to locate in the container.</param>
        /// <returns>Returns true if the container contains the key.</returns>
        public bool ContainsKey(TKey key)
        {
            return DynamicHashMapBase<TBuffer, TKey, TValue>.TryGetFirstValueAtomic(this.Buffer, key, out var tempValue, out var tempIt);
        }

        /// <summary>
        /// Returns array populated with keys.
        /// </summary>
        /// <remarks>Number of returned keys will match number of values in the container. If key contains multiple values it will appear number of times
        /// how many values are associated to the same key. If only unique key values desired use GetUniqueKeyArray instead.</remarks>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>Array of keys.</returns>
        public NativeArray<TKey> GetKeyArray(Allocator allocator)
        {
            var result = new NativeArray<TKey>(this.Count(), allocator, NativeArrayOptions.UninitializedMemory);
            DynamicHashMapData.GetKeyArray(this.Buffer, result);
            return result;
        }

        /// <summary>
        /// Returns array populated with values.
        /// </summary>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>Array of values.</returns>
        public NativeArray<TValue> GetValueArray(Allocator allocator)
        {
            var result = new NativeArray<TValue>(this.Count(), allocator, NativeArrayOptions.UninitializedMemory);
            DynamicHashMapData.GetValueArray(this.Buffer, result);
            return result;
        }

        /// <summary>
        /// Returns arrays populated with keys and values.
        /// </summary>
        /// <remarks>If key contains multiple values, returned key array will contain multiple identical keys.</remarks>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>Array of keys-values.</returns>
        public NativeKeyValueArrays<TKey, TValue> GetKeyValueArrays(Allocator allocator)
        {
            var result = new NativeKeyValueArrays<TKey, TValue>(this.Count(), allocator, NativeArrayOptions.UninitializedMemory);
            DynamicHashMapData.GetKeyValueArrays(this.Buffer, result);
            return result;
        }

        private void Allocate()
        {
            CollectionHelper.CheckIsUnmanaged<TKey>();
            CollectionHelper.CheckIsUnmanaged<TValue>();

            DynamicHashMapData.AllocateHashMap<TBuffer, TKey, TValue>(this.data, 0, 0, out _);
            this.Clear();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckSize(DynamicBuffer<TBuffer> buffer)
        {
            if (UnsafeUtility.SizeOf<TBuffer>() != 1)
            {
                throw new InvalidOperationException($"Type {typeof(TBuffer)} is not size 1.");
            }

            if (buffer.Length != 0 && buffer.Length < UnsafeUtility.SizeOf<DynamicHashMapData>())
            {
                throw new InvalidOperationException($"Buffer has data but is too small to be a header.");
            }
        }
    }
}