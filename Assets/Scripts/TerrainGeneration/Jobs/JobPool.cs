using System.Collections.Generic;
using Unity.Jobs;

namespace WorldGeneration {

public interface IConstructableJob {

    public void Construct ();

}

public interface IDisposableJob {

    public void Dispose ();

}

public class JobPool<TJob> where TJob : struct, IJobParallelFor, IConstructableJob, IDisposableJob {

    // Use this delegate to set data in the job before it's scheduled
    public delegate void InitializeJob (ref TJob job);

    // Use this delegate to process the data in the job after it's been completed
    public delegate void OnJobCompleted (ref TJob job);
    
    // These fields are used when scheduling the job
    private readonly int arrayLength;
    private readonly int innerLoopBatchCount;

    private bool isDisposed;
    private readonly Queue<TJob> passiveJobs = new Queue<TJob>();
    private readonly List<(TJob job, JobHandle handle, OnJobCompleted onJobCompleted)> activeJobs = new List<(TJob job, JobHandle handle, OnJobCompleted onJobCompleted)>();

    public JobPool (int arrayLength, int innerLoopBatchCount) {
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

    public JobHandle ScheduleJob (InitializeJob initializeJob, OnJobCompleted onJobCompleted, JobHandle dependsOn = default) {
        if (isDisposed) {
            return default;
        }
        
        var job = GetJob();
        
        // Make sure the job data is up to date
        initializeJob.Invoke(ref job);
        
        // Schedule the job
        var handle = job.Schedule(arrayLength, innerLoopBatchCount, dependsOn);
        
        // Keep track of the job and it's handle
        activeJobs.Add((job, handle, onJobCompleted));

        return handle;
    }

    public void Update () {
        if (isDisposed) {
            return;
        }
        
        for (var i = 0; i < activeJobs.Count; i++) {
            var (job, handle, onJobCompleted) = activeJobs[i];

            // Check if the job is no longer active
            if (handle.IsCompleted) {
                activeJobs.RemoveAt(i);
                
                // Tell the job system we are done with this job
                handle.Complete();

                // Send notice of the job's completion
                onJobCompleted.Invoke(ref job);
                
                // Move the job to the waiting queue
                passiveJobs.Enqueue(job);
                i--;
            }
        }
    }

}

}