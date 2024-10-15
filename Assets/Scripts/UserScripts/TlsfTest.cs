using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using StargateNet;

public unsafe class TlsfTest : MonoBehaviour
{
    private const int CONTROLT_SIZE = 6536;
    private const int BLOCK_SIZE = 1024 * 2 + CONTROLT_SIZE; // 1024 for tlsf data
    private const int ADDRESS_ALIGNMENT_TO_TLSF = 8;
    private const int SNAPSHOT_SIZE = 128;
    private void* tlsfBlock;
    private void* snapShot;

    private void Awake()
    {
        // Debug.Log(sizeof(TLSF64.control_t));
        tlsfBlock = UnsafeUtility.Malloc(BLOCK_SIZE, ADDRESS_ALIGNMENT_TO_TLSF, Allocator.Persistent);
        tlsfBlock = TLSF64.tlsf_create_with_pool(tlsfBlock, BLOCK_SIZE);
        TLSF64.control_t* ct = (TLSF64.control_t*)tlsfBlock;
        // TLSF64.block_header_t* temp = (TLSF64.block_header_t*)((ulong)ct + (ulong)sizeof(TLSF64.control_t));
        // Debug.Log(temp->size);
        // 实际上给了24字节
        void* test = TLSF64.tlsf_malloc(tlsfBlock, 1);
        void* test2 = TLSF64.tlsf_malloc(tlsfBlock, 1024);
        TLSF64.block_header_t* header = TLSF64.block_from_ptr(test);
        // 内存被切割，剩下的大小在2^8量级(tlsf最小区块是16字节)
        Debug.Log(header->size);
        Debug.Log(ct->fl_bitmap);
        // 第一次还，内存不连续，所以bitmap结果是17，即还回来的在最小集合
        TLSF64.tlsf_free(tlsfBlock, test);
        Debug.Log(ct->fl_bitmap);
        // 第二次还，触发合并机制，前后连续，bitmap又变成32
        TLSF64.tlsf_free(tlsfBlock, test2);
        Debug.Log(ct->fl_bitmap);
        UnsafeUtility.Free(tlsfBlock, Allocator.Persistent);
    }
}