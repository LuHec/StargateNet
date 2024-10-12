using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using StargateNet;

public unsafe class TlsfTest : MonoBehaviour
{
    private const int BLOCK_SIZE = 1024 * 16; // 1024 for tlsf data
    private const int ADDRESS_ALIGNMENT_TO_TLSF = 16;
    private const int SNAPSHOT_SIZE = 128;
    private void* tlsfBlock;
    private void* snapShot;

    private void Awake()
    {
        tlsfBlock = UnsafeUtility.Malloc(BLOCK_SIZE, ADDRESS_ALIGNMENT_TO_TLSF, Allocator.Persistent);
        tlsfBlock = TLSF64.tlsf_create_with_pool(tlsfBlock, BLOCK_SIZE);
        TLSF64.control_t* ct = (TLSF64.control_t*)tlsfBlock;
        void* test = TLSF64.tlsf_malloc(tlsfBlock, 1024 * 4);
        TLSF64.block_header_t* header = TLSF64.block_from_ptr(test);
        Debug.Log(header->size);
        Debug.Log(ct->fl_bitmap);
        TLSF64.tlsf_free(tlsfBlock, test);
        UnsafeUtility.Free(tlsfBlock, Allocator.Persistent);
    }
}