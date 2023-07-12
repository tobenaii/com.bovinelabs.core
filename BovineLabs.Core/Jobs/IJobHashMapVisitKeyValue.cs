﻿// <copyright file="IJobHashMapVisitKeyValue.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Core.Jobs
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using JetBrains.Annotations;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary>
    /// A burst friendly low level hash map enumerating job.
    /// You can use <see cref="JobHashMapVisitKeyValue.Read{TJob, TKey, TValue}" /> to safely get the current key/value.
    /// </summary>
    [JobProducerType(typeof(JobHashMapVisitKeyValue.JobHashMapVisitKeyValueProducer<>))]
    public unsafe interface IJobHashMapVisitKeyValue
    {
        void ExecuteNext(byte* keys, byte* values, int entryIndex, int jobIndex);
    }

    public static class JobHashMapVisitKeyValue
    {
        public static unsafe JobHandle ScheduleParallel<TJob, TKey, TValue>(
            this TJob jobData,
            NativeHashMap<TKey, TValue> hashMap,
            int minIndicesPerJobCount,
            JobHandle dependsOn = default)
            where TJob : unmanaged, IJobHashMapVisitKeyValue
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var jobProducer = new JobHashMapVisitKeyValueProducer<TJob>
            {
                HashMap = (HashMapWrapper*)hashMap.m_Data,
                JobData = jobData,
            };

            JobHashMapVisitKeyValueProducer<TJob>.Initialize();
            var reflectionData = JobHashMapVisitKeyValueProducer<TJob>.ReflectionData.Data;
            CollectionHelper.CheckReflectionDataCorrect<TJob>(reflectionData);

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobProducer),
                reflectionData,
                dependsOn,
                ScheduleMode.Parallel);

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, hashMap.m_Data->BucketCapacity, minIndicesPerJobCount);
        }

        /// <summary>
        /// Gathers and caches reflection data for the internal job system's managed bindings.
        /// Unity is responsible for calling this method - don't call it yourself.
        /// </summary>
        [UsedImplicitly]
        public static void EarlyJobInit<T>()
            where T : struct, IJobHashMapVisitKeyValue
        {
            JobHashMapVisitKeyValueProducer<T>.Initialize();
        }

        public static unsafe void Read<TJob, TKey, TValue>(this ref TJob job, int entryIndex, byte* keys, byte* values, out TKey key, out TValue value)
            where TJob : unmanaged, IJobHashMapVisitKeyValue
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            key = UnsafeUtility.ReadArrayElement<TKey>(keys, entryIndex);
            value = UnsafeUtility.ReadArrayElement<TValue>(values, entryIndex);
        }

        /// <summary> The job execution struct. </summary>
        /// <typeparam name="T"> The type of the job. </typeparam>
        internal unsafe struct JobHashMapVisitKeyValueProducer<T>
            where T : struct, IJobHashMapVisitKeyValue
        {
            /// <summary> The <see cref="NativeParallelMultiHashMap{TKey,TValue}" />. </summary>
            [ReadOnly]
            [NativeDisableUnsafePtrRestriction]
            public HashMapWrapper* HashMap;

            // ReSharper disable once StaticMemberInGenericType
            internal static readonly SharedStatic<IntPtr> ReflectionData = SharedStatic<IntPtr>.GetOrCreate<JobHashMapVisitKeyValueProducer<T>>();

            /// <summary> The job. </summary>
            internal T JobData;

            private delegate void ExecuteJobFunction(
                ref JobHashMapVisitKeyValueProducer<T> producer,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex);

            [BurstDiscard]
            internal static void Initialize()
            {
                if (ReflectionData.Data == IntPtr.Zero)
                {
                    ReflectionData.Data = JobsUtility.CreateJobReflectionData(
                        typeof(JobHashMapVisitKeyValueProducer<T>),
                        typeof(T),
                        (ExecuteJobFunction)Execute);
                }
            }

            /// <summary> Executes the job. </summary>
            /// <param name="fullData"> The job data. </param>
            /// <param name="additionalPtr"> AdditionalPtr. </param>
            /// <param name="bufferRangePatchData"> BufferRangePatchData. </param>
            /// <param name="ranges"> The job range. </param>
            /// <param name="jobIndex"> The job index. </param>
            [SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Required by burst.")]
            internal static void Execute(
                ref JobHashMapVisitKeyValueProducer<T> fullData,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex)
            {
                while (true)
                {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                    {
                        return;
                    }

                    var buckets = fullData.HashMap->Buckets;
                    var nextPtrs = fullData.HashMap->Next;
                    var keys = fullData.HashMap->Keys;
                    var values = fullData.HashMap->Ptr;

                    for (var i = begin; i < end; i++)
                    {
                        var entryIndex = buckets[i];

                        while (entryIndex != -1)
                        {
                            fullData.JobData.ExecuteNext(keys, values, entryIndex, jobIndex);
                            entryIndex = nextPtrs[entryIndex];
                        }
                    }
                }
            }
        }

        internal unsafe struct HashMapWrapper
        {
            [NativeDisableUnsafePtrRestriction]
            internal byte* Ptr;

            [NativeDisableUnsafePtrRestriction]
            internal byte* Keys;

            [NativeDisableUnsafePtrRestriction]
            internal int* Next;

            [NativeDisableUnsafePtrRestriction]
            internal int* Buckets;

            private int Count;
            private int Capacity;
            private int Log2MinGrowth;
            private int BucketCapacity;
            private int AllocatedIndex;
            private int FirstFreeIdx;
            private int SizeOfTValue;
            private AllocatorManager.AllocatorHandle Allocator;
        }
    }
}
