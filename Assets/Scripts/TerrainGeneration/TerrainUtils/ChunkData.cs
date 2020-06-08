using System;
using TerrainGeneration.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Rendering;

namespace WorldGeneration {

public class ChunkData : MonoBehaviour {

    //Native collections for mesh and density data used in jobs.
    private NativeArray<Vector3> buffer;
    private NativeArray<int> indexes;
    private NativeArray<float> noiseMap;

    //Chunk's position
    public int3 pos;

    //Marching cubes job handle
    private JobHandle myHandle;

    //Is marching cubes job currently running
    private bool isBeingProcessed;

    //Collider baking handle
    private JobHandle colliderHandle;

    //Is a collider being currently baked
    private bool meshBaking = false;

    //This chunk's mesh
    private Mesh myMesh;

    //This chunk's mesh filter
    private MeshFilter filter;

    //Thread safe counter to keep track of vertex index from https://github.com/Eldemarkki/Marching-Cubes-Terrain
    private Counter _counter;

    //Chunk's size
    public int size;

    //This is needed after mesh.SetIndexBuffer data to make the mesh visible, for some reason
    private SubMeshDescriptor desc;

    private bool isNewChunk = true;

    //Has chunk been modified since last frame
    private bool needsUpdate = false;

    private bool isModifiedByUser;

    //Variables passed chunk modify job
    private int3 newExplosionSpot;
    private float explosionRange;
    private float explosionValue;

    //Simulate an explosion at a point for this chunk
    public void Explode (int3 worldPos, float explosionRange, float explosionSign) {
        newExplosionSpot = worldPos - pos;
        this.explosionRange = explosionRange;
        explosionValue = explosionSign;
        needsUpdate = true;

        isModifiedByUser = true;
    }

    private void OnEnable () {
        if (isNewChunk) {
            NewChunk();
            isNewChunk = false;
        }
        else {
            myHandle.Complete();
            myMesh.Clear();
            ChunkUpdate();
        }

        WorldBase.currentChunks.Add((int3) (float3) transform.position, this);
    }

    private void OnDisable () {
        //myHandle.Complete();
        isBeingProcessed = false;

        if (myMesh.vertexCount > 0 && isModifiedByUser) {
            var saveData = new ChunkSaveData {
                key = "test",
                position = pos,
                noiseMap = new float[noiseMap.Length],
            };

            noiseMap.CopyTo(saveData.noiseMap);

            ChunkSaveData.Save(saveData);
        }

        WorldBase.currentChunks.Remove(pos);
        WorldBase.freeChunks.Enqueue(this);
    }

    private void OnApplicationQuit () {
        buffer.Dispose();
        indexes.Dispose();
        noiseMap.Dispose();
    }

    private void LateUpdate () {
        if (isBeingProcessed) {
            if (myHandle.IsCompleted) {
                isBeingProcessed = false;
                myHandle.Complete();
                UpdateChunk();
            }
        }
        else if (needsUpdate) {
            NoiseMapExplosion();
            needsUpdate = false;
        }
        else if (colliderHandle.IsCompleted && meshBaking) {
            colliderHandle.Complete();
            ApplyCollider();
            meshBaking = false;
        }
    }

    private void NewChunk () {
        // transform.position = (float3)pos;
        var arraySize = size * size * size;
        buffer = new NativeArray<Vector3>(arraySize * 3 * 5, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        indexes = new NativeArray<int>(arraySize * 3 * 5, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        noiseMap = new NativeArray<float>((size + 1) * (size + 1) * (size + 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        myMesh = new Mesh();
        filter = GetComponent<MeshFilter>();
        filter.sharedMesh = myMesh;

        desc.topology = MeshTopology.Triangles;

        ChunkUpdate();
    }

    private void ChunkUpdate () {
        var loadedData = ChunkSaveData.Load("test", pos);

        JobHandle? generateHandle = null;
        if (loadedData == null) {
            generateHandle = GenerateChunk();
        }
        else {
            noiseMap.CopyFrom(loadedData.noiseMap);
        }

        MarchChunk(generateHandle);
    }

    private void UpdateChunk () {
        var layout = new[] {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        };
        var vertexCount = _counter.Count * 3;
        if (vertexCount > 0) {
            var center = size / 2;
            myMesh.bounds = new Bounds(new Vector3(center, center, center), new Vector3(size, size, size));
            //Set vertices and indices
            myMesh.SetVertexBufferParams(vertexCount, layout);
            myMesh.SetVertexBufferData(buffer, 0, 0, vertexCount, 0, MeshUpdateFlags.DontValidateIndices);
            myMesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
            myMesh.SetIndexBufferData(indexes, 0, 0, vertexCount, MeshUpdateFlags.DontValidateIndices);

            desc.indexCount = vertexCount;
            myMesh.SetSubMesh(0, desc, MeshUpdateFlags.DontValidateIndices);

            myMesh.RecalculateNormals();
            filter.sharedMesh = myMesh;
            
            //Start collider baking
            var colliderJob = new ColliderBakeJob {batchSize = 1, meshIDs = {[0] = myMesh.GetInstanceID()}};
            colliderHandle = colliderJob.Schedule();
            meshBaking = true;
        }
        else {
            myMesh.Clear();
        }

        // transform.position = (float3)pos;

        _counter.Dispose();
    }

    //Collider baked, set it to the chunk
    private void ApplyCollider () {
        GetComponent<MeshCollider>().sharedMesh = myMesh;
    }

    private JobHandle GenerateChunk () {
        var noiseJob = new NoiseJob() {
            amplitude = WorldBase.noiseData.ampl,
            frequency = WorldBase.noiseData.freq,
            octaves = WorldBase.noiseData.oct,
            offset = pos,
            seed = WorldBase.noiseData.offset,
            surfaceLevel = WorldBase.noiseData.surfaceLevel,
            noiseMap = noiseMap,
            size = size + 1
            //pos = WorldSetup.positions
        };

        noiseJob.Construct();

        return noiseJob.Schedule((size + 1) * (size + 1) * (size + 1), 64);
    }

    private void MarchChunk (JobHandle? noiseHandle) {
        _counter = new Counter(Allocator.Persistent);
        var arraySize = size * size * size;

        var marchingJob = new MarchingCubesJob() {
            densities = noiseMap,
            isolevel = 0f,
            chunkSize = size,
            triangleIndexBuffer = indexes,
            vertexBuffer = buffer,
            counter = _counter
        };

        marchingJob.Construct();

        myHandle = noiseHandle != null ? marchingJob.Schedule(arraySize, 32, noiseHandle.Value) : marchingJob.Schedule(arraySize, 32);
        isBeingProcessed = true;
    }

    private void NoiseMapExplosion () {
        _counter = new Counter(Allocator.Persistent);
        var noiseUpdateJob = new ChunkExplodeJob() {
            size = size + 1,
            isolevel = 0,
            explosionOrigin = newExplosionSpot,
            explosionRange = explosionRange,
            newDensity = explosionValue,
            noiseMap = noiseMap
        };
        var handl = noiseUpdateJob.Schedule((size + 1) * (size + 1) * (size + 1), 64);

        var marchingJob = new MarchingCubesJob() {
            densities = noiseMap,
            isolevel = 0f,
            chunkSize = size,
            triangleIndexBuffer = indexes,
            vertexBuffer = buffer,
            counter = _counter
        };
        var marchinJob = marchingJob.Schedule(size * size * size, 32, handl);
        myHandle = marchinJob;
        isBeingProcessed = true;
    }

}

}