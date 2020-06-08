using System;
using System.Collections.Generic;
using TerrainGeneration.TerrainUtils;
using Unity.Jobs;

namespace TerrainGeneration.Jobs {

public class TerrainJobPool<TChunk, TJob> where TJob : struct, IJobParallelFor, IConstructable, IDisposable {

    // Use this delegate to set data in the job before it's scheduled
    public delegate void InitializeJob (TChunk chunk, ref TJob job);

    // Use this delegate to process the data in the job after it's been completed
    public delegate void OnJobCompleted (TChunk chunk, ref TJob job);
    
    // These fields are used when scheduling the job
    private readonly int arrayLength;
    private readonly int innerLoopBatchCount;

    private bool isDisposed;
    private readonly Queue<TJob> passiveJobs = new Queue<TJob>();
    private readonly List<ActiveJobData> activeJobs = new List<ActiveJobData>();

    public TerrainJobPool (int arrayLength, int innerLoopBatchCount) {
        this.arrayLength = arrayLength;
        this.innerLoopBatchCount = innerLoopBatchCount;
    }

    public void Dispose () {
        isDisposed = true;

        foreach (var job in passiveJobs) {
            job.Dispose();
        }
    }

    private TJob GetJob () {
        // Get an existing job first if it exists
        if (passiveJobs.Count > 0) {
            return passiveJobs.Dequeue();
        }

        // TJob is a struct so we can simply create one and rely on the InitializeJob delegate to initialize it
        var job = new TJob();
        job.Construct();

        return job;
    }

    public void ScheduleJob (TChunk chunk, InitializeJob initializeJob, OnJobCompleted onJobCompleted, out JobHandle jobHandle, JobHandle dependsOn = default) {
        jobHandle = ScheduleJob(chunk, initializeJob, onJobCompleted, dependsOn);
    }

    public JobHandle ScheduleJob (TChunk chunk, InitializeJob initializeJob, OnJobCompleted onJobCompleted, JobHandle dependsOn = default) {
        if (isDisposed) {
            return default;
        }
        
        var job = GetJob();
        
        // Make sure the job data is up to date
        initializeJob.Invoke(chunk, ref job);
        
        // Schedule the job
        var handle = job.Schedule(arrayLength, innerLoopBatchCount, dependsOn);
        
        // Keep track of the job and it's handle
        activeJobs.Add(new ActiveJobData(job, chunk, handle, onJobCompleted));

        return handle;
    }

    public void Update () {
        if (isDisposed) {
            return;
        }
        
        for (var i = 0; i < activeJobs.Count; i++) {
            var (job, chunk, handle, onJobCompleted) = activeJobs[i];

            // Check if the job is no longer active
            if (handle.IsCompleted) {
                activeJobs.RemoveAt(i);
                
                // Tell the job system we are done with this job
                handle.Complete();

                // Send notice of the job's completion
                onJobCompleted.Invoke(chunk, ref job);
                
                // Move the job to the waiting queue
                passiveJobs.Enqueue(job);
                i--;
            }
        }
    }

    public class ActiveJobData {

        public TJob job;
        public TChunk chunk;
        public JobHandle handle;
        public OnJobCompleted onJobCompleted;

        public ActiveJobData (TJob job, TChunk chunk, JobHandle handle, OnJobCompleted onJobCompleted) {
            this.job = job;
            this.chunk = chunk;
            this.handle = handle;
            this.onJobCompleted = onJobCompleted;
        }

        public void Deconstruct(out TJob job, out TChunk chunk, out JobHandle handle, out OnJobCompleted onJobCompleted) {
            job = this.job;
            chunk = this.chunk;
            handle = this.handle;
            onJobCompleted = this.onJobCompleted;
        }
        
    }

}

}