using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using tlsf;

public static unsafe class TLSF64
{
    public const int SL_INDEX_COUNT_LOG2 = 5; 
    public const int ALIGN_SIZE_LOG2 = 3;
    public const int ALIGN_SIZE = 1 << ALIGN_SIZE_LOG2;
    public const int FL_INDEX_MAX = 32;
    public const int SL_INDEX_COUNT = 1 << SL_INDEX_COUNT_LOG2;
    public const int FL_INDEX_SHIFT = SL_INDEX_COUNT_LOG2 + ALIGN_SIZE_LOG2;
    public const int FL_INDEX_COUNT = FL_INDEX_MAX - FL_INDEX_SHIFT + 1;
    public const int SMALL_BLOCK_SIZE = 1 << FL_INDEX_SHIFT;
    public const ulong block_header_free_bit = 1 << 0;
    public const ulong block_header_prev_free_bit = 1 << 1;
    public const ulong block_header_overhead = sizeof(ulong);
    public const ulong block_start_offset = sizeof(ulong) + sizeof(ulong);
    public static readonly ulong block_size_min = (ulong)(sizeof(block_header_t) - sizeof(ulong));
    public const ulong block_size_max = (ulong)1 << FL_INDEX_MAX;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void memcpy(void* dst, void* src, ulong size) => Unsafe.CopyBlock(dst, src, (uint)size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int tlsf_fls(uint word)
    {
        var bit = 32 - BitOperationsHelpers.LeadingZeroCount(word);
        return bit - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int tlsf_ffs(uint word)
    {
        var reverse = word & (~word + 1);
        var bit = 32 - BitOperationsHelpers.LeadingZeroCount(reverse);
        return bit - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int tlsf_fls_sizet(ulong size)
    {
        var bit = BitOperationsHelpers.LeadingZeroCount(size);
        return bit == 64 ? -1 : 63 - bit;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong tlsf_min(ulong a, ulong b) => a < b ? a : b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong tlsf_max(ulong a, ulong b) => a > b ? a : b;

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void tlsf_assert(bool condition) => Debug.Assert(condition);

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void tlsf_assert(bool condition, string message) => Debug.Assert(condition, message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong block_size(block_header_t* block) =>
        block->size & ~(block_header_free_bit | block_header_prev_free_bit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_set_size(block_header_t* block, ulong size)
    {
        var oldsize = block->size;
        block->size = size | (oldsize & (block_header_free_bit | block_header_prev_free_bit));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int block_is_last(block_header_t* block) => block_size(block) == 0 ? 1 : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int block_is_free(block_header_t* block) => (int)(block->size & block_header_free_bit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_set_free(block_header_t* block) => block->size |= block_header_free_bit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_set_used(block_header_t* block) => block->size &= ~block_header_free_bit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int block_is_prev_free(block_header_t* block) => (int)(block->size & block_header_prev_free_bit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_set_prev_free(block_header_t* block) => block->size |= block_header_prev_free_bit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_set_prev_used(block_header_t* block) => block->size &= ~block_header_prev_free_bit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_from_ptr(void* ptr) => (block_header_t*)((byte*)ptr - block_start_offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* block_to_ptr(block_header_t* block) => (byte*)block + block_start_offset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* offset_to_block(void* ptr, ulong size) => (block_header_t*)((nint)ptr + (nint)size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_prev(block_header_t* block)
    {
        tlsf_assert(block_is_prev_free(block) != 0, "previous block must be free");
        return block->prev_phys_block;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_next(block_header_t* block)
    {
        var next = offset_to_block(block_to_ptr(block), block_size(block) - block_header_overhead);
        tlsf_assert(block_is_last(block) == 0);
        return next;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_link_next(block_header_t* block)
    {
        var next = block_next(block);
        next->prev_phys_block = block;
        return next;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_mark_as_free(block_header_t* block)
    {
        // 在申请的时候，block的前后链表被连接，但是block还是持有前后链表的地址，所以可以完成插入
        var next = block_link_next(block);
        block_set_prev_free(next);
        block_set_free(block);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_mark_as_used(block_header_t* block)
    {
        var next = block_next(block);
        block_set_prev_used(next);
        block_set_used(block);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong align_up(ulong x, ulong align)
    {
        // 5和4,5比4多个1，最低位为1，所以相与结果为1；4和3，4有效数据比3高一位，所以相与结果为0
        tlsf_assert(0 == (align & (align - 1)), "must align to a power of two");
        return (x + (align - 1)) & ~(align - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong align_down(ulong x, ulong align)
    {
        tlsf_assert(0 == (align & (align - 1)), "must align to a power of two");
        return x - (x & (align - 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* align_ptr(void* ptr, ulong align)
    {
        var aligned = ((nint)ptr + (nint)(align - 1)) & ~ (nint)(align - 1);
        tlsf_assert(0 == (align & (align - 1)), "must align to a power of two");
        return (void*)aligned;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong adjust_request_size(ulong size, ulong align)
    {
        ulong adjust = 0;
        if (size != 0)
        {
            var aligned = align_up(size, align);
            if (aligned < block_size_max)
                adjust = tlsf_max(aligned, block_size_min);
        }

        return adjust;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void mapping_insert(ulong size, int* fli, int* sli)
    {
        int fl, sl;
        if (size < SMALL_BLOCK_SIZE)
        {
            fl = 0;
            sl = (int)size / (SMALL_BLOCK_SIZE / SL_INDEX_COUNT);
        }
        else
        {
            fl = tlsf_fls_sizet(size);
            sl = (int)(size >> (fl - SL_INDEX_COUNT_LOG2)) ^ (1 << SL_INDEX_COUNT_LOG2);
            fl -= FL_INDEX_SHIFT - 1;
        }

        *fli = fl;
        *sli = sl;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void mapping_search(ulong size, int* fli, int* sli)
    {
        if (size >= SMALL_BLOCK_SIZE)
        {
            var round = (ulong)((1 << (tlsf_fls_sizet(size) - SL_INDEX_COUNT_LOG2)) - 1);
            size += round;
        }

        mapping_insert(size, fli, sli);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* search_suitable_block(control_t* control, int* fli, int* sli)
    {
        var fl = *fli;
        var sl = *sli;
        var sl_map = control->sl_bitmap[fl] & (~0U << sl);
        if (!(sl_map != 0))
        {
            var fl_map = control->fl_bitmap & (~0U << (fl + 1));
            if (!(fl_map != 0))
                return null;
            fl = tlsf_ffs(fl_map);
            *fli = fl;
            // 用fl找到slmap（包含了四个槽位）
            sl_map = control->sl_bitmap[fl];
        }

        tlsf_assert(sl_map != 0, "internal error - second level bitmap is null");
        // 从第二层中拿到有内存的槽位
        sl = tlsf_ffs(sl_map);
        *sli = sl;
        // 获取第二级的位置，然后拿到具体的block
        // tlsf设计中，第一个block是空闲块
        return get_blocks(control, fl)[sl];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void remove_free_block(control_t* control, block_header_t* block, int fl, int sl)
    {
        var prev = block->prev_free;
        var next = block->next_free;
        tlsf_assert(prev != null, "prev_free field can not be null");
        tlsf_assert(next != null, "next_free field can not be null");
        next->prev_free = prev;
        prev->next_free = next;
        if (get_blocks(control, fl)[sl] == block)
        {
            get_blocks(control, fl)[sl] = next;
            if (next == &control->block_null)
            {
                control->sl_bitmap[fl] &= ~(1U << sl);
                if (!(control->sl_bitmap[fl] != 0))
                    control->fl_bitmap &= ~(1U << fl);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void insert_free_block(control_t* control, block_header_t* block, int fl, int sl)
    {
        var current = get_blocks(control, fl)[sl];
        tlsf_assert(current != null, "free list cannot have a null entry");
        tlsf_assert(block != null, "cannot insert a null entry into the free list");
        block->next_free = current;
        block->prev_free = &control->block_null;
        current->prev_free = block;
        tlsf_assert(block_to_ptr(block) == align_ptr(block_to_ptr(block), ALIGN_SIZE), "block not aligned properly");
        get_blocks(control, fl)[sl] = block;
        control->fl_bitmap |= 1U << fl;
        control->sl_bitmap[fl] |= 1U << sl;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_remove(control_t* control, block_header_t* block)
    {
        int fl, sl;
        mapping_insert(block_size(block), &fl, &sl);
        remove_free_block(control, block, fl, sl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_insert(control_t* control, block_header_t* block)
    {
        int fl, sl;
        mapping_insert(block_size(block), &fl, &sl);
        insert_free_block(control, block, fl, sl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int block_can_split(block_header_t* block, ulong size) =>
        block_size(block) >= (ulong)sizeof(block_header_t) + size ? 1 : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_split(block_header_t* block, ulong size)
    {
        var remaining = offset_to_block(block_to_ptr(block), size - block_header_overhead);
        var remain_size = block_size(block) - (size + block_header_overhead);
        tlsf_assert(block_to_ptr(remaining) == align_ptr(block_to_ptr(remaining), ALIGN_SIZE),
            "remaining block not aligned properly");
        tlsf_assert(block_size(block) == remain_size + size + block_header_overhead);
        block_set_size(remaining, remain_size);
        tlsf_assert(block_size(remaining) >= block_size_min, "block split with invalid size");
        block_set_size(block, size);
        block_mark_as_free(remaining);
        return remaining;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_absorb(block_header_t* prev, block_header_t* block)
    {
        tlsf_assert(block_is_last(prev) == 0, "previous block can't be last");
        prev->size += block_size(block) + block_header_overhead;
        block_link_next(prev);
        return prev;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_merge_prev(control_t* control, block_header_t* block)
    {
        if (block_is_prev_free(block) != 0)
        {
            var prev = block_prev(block);
            tlsf_assert(prev != null, "prev physical block can't be null");
            tlsf_assert(block_is_free(prev) != 0, "prev block is not free though marked as such");
            block_remove(control, prev);
            block = block_absorb(prev, block);
        }

        return block;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_merge_next(control_t* control, block_header_t* block)
    {
        var next = block_next(block);
        tlsf_assert(next != null, "next physical block can't be null");
        if (block_is_free(next) != 0)
        {
            tlsf_assert(block_is_last(block) == 0, "previous block can't be last");
            block_remove(control, next);
            block = block_absorb(block, next);
        }

        return block;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_trim_free(control_t* control, block_header_t* block, ulong size)
    {
        tlsf_assert(block_is_free(block) != 0, "block must be free");
        if (block_can_split(block, size) != 0)
        {
            var remaining_block = block_split(block, size);
            block_link_next(block);
            block_set_prev_free(remaining_block);
            block_insert(control, remaining_block);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void block_trim_used(control_t* control, block_header_t* block, ulong size)
    {
        tlsf_assert(block_is_free(block) == 0, "block must be used");
        if (block_can_split(block, size) != 0)
        {
            var remaining_block = block_split(block, size);
            block_set_prev_used(remaining_block);
            remaining_block = block_merge_next(control, remaining_block);
            block_insert(control, remaining_block);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_trim_free_leading(control_t* control, block_header_t* block, ulong size)
    {
        var remaining_block = block;
        if (block_can_split(block, size) != 0)
        {
            remaining_block = block_split(block, size - block_header_overhead);
            block_set_prev_free(remaining_block);
            block_link_next(block);
            block_insert(control, block);
        }

        return remaining_block;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t* block_locate_free(control_t* control, ulong size)
    {
        int fl = 0, sl = 0;
        block_header_t* block = null;
        if (size != 0)
        {
            mapping_search(size, &fl, &sl);
            if (fl < FL_INDEX_COUNT)
                block = search_suitable_block(control, &fl, &sl);
        }

        if (block != null)
        {
            tlsf_assert(block_size(block) >= size);
            remove_free_block(control, block, fl, sl);
        }
        
        return block;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* block_prepare_used(control_t* control, block_header_t* block, ulong size)
    {
        void* p = null;
        if (block != null)
        {
            tlsf_assert(size != 0, "size must be non-zero");
            block_trim_free(control, block, size);
            block_mark_as_used(block);
            p = block_to_ptr(block);
        }

        return p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void control_construct(control_t* control)
    {
        int i, j;
        control->block_null.next_free = &control->block_null;
        control->block_null.prev_free = &control->block_null;
        control->fl_bitmap = 0;
        for (i = 0; i < FL_INDEX_COUNT; ++i)
        {
            control->sl_bitmap[i] = 0;
            for (j = 0; j < SL_INDEX_COUNT; ++j)
                // 初始化，将所有sl的首节点都设置为空
                get_blocks(control, i)[j] = &control->block_null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void tlsf_insist(bool x, string message, ref int status)
    {
        tlsf_assert(x, message);
        if (!x)
            status--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void integrity_walker(void* ptr, ulong size, int used, void* user)
    {
        var block = block_from_ptr(ptr);
        var integ = (integrity_t*)user;
        var this_prev_status = block_is_prev_free(block) != 0 ? 1 : 0;
        var this_status = block_is_free(block) != 0 ? 1 : 0;
        var this_block_size = block_size(block);
        var status = 0;
        _ = used;
        tlsf_insist(integ->prev_status == this_prev_status, "prev status incorrect", ref status);
        tlsf_insist(size == this_block_size, "block size incorrect", ref status);
        integ->prev_status = this_status;
        integ->status += status;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int tlsf_check(void* tlsf)
    {
        int i, j;
        var control = (control_t*)tlsf;
        var status = 0;
        for (i = 0; i < FL_INDEX_COUNT; ++i)
        {
            for (j = 0; j < SL_INDEX_COUNT; ++j)
            {
                var fl_map = (int)(control->fl_bitmap & (1U << i));
                var sl_list = (int)control->sl_bitmap[i];
                var sl_map = (int)(sl_list & (1U << j));
                var block = get_blocks(control, i)[j];
                if (!(fl_map != 0))
                    tlsf_insist(sl_map == 0, "second-level map must be null", ref status);
                if (!(sl_map != 0))
                {
                    tlsf_insist(block == &control->block_null, "block list must be null", ref status);
                    continue;
                }

                tlsf_insist(sl_list != 0, "no free blocks in second-level map", ref status);
                tlsf_insist(block != &control->block_null, "block should not be null", ref status);
                while (block != &control->block_null)
                {
                    int fli, sli;
                    tlsf_insist(block_is_free(block) != 0, "block should be free", ref status);
                    tlsf_insist(block_is_prev_free(block) == 0, "blocks should have coalesced", ref status);
                    tlsf_insist(block_is_free(block_next(block)) == 0, "blocks should have coalesced", ref status);
                    tlsf_insist(block_is_prev_free(block_next(block)) != 0, "block should be free", ref status);
                    tlsf_insist(block_size(block) >= block_size_min, "block not minimum size", ref status);
                    mapping_insert(block_size(block), &fli, &sli);
                    tlsf_insist(fli == i && sli == j, "block size indexed in wrong list", ref status);
                    block = block->next_free;
                }
            }
        }

        return status;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void default_walker(void* ptr, ulong size, int used, void* user)
    {
        _ = user;
        Console.WriteLine("\t{0} {1} size: {2:X} ({3})", (nuint)ptr, used != 0 ? "used" : "free", (uint)size,
            (nuint)block_from_ptr(ptr));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void tlsf_walk_pool(void* pool, delegate* managed<void*, ulong, int, void*, void> walker, void* user)
    {
        var pool_walker = walker != null ? walker : &default_walker;
        var block = offset_to_block(pool, unchecked((ulong)-(int)block_header_overhead));
        while (block != null && !(block_is_last(block) != 0))
        {
            pool_walker(block_to_ptr(block), block_size(block), !(block_is_free(block) != 0) ? 1 : 0, user);
            block = block_next(block);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong tlsf_block_size(void* ptr)
    {
        ulong size = 0;
        if (ptr != null)
        {
            var block = block_from_ptr(ptr);
            size = block_size(block);
        }

        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong tlsf_size() => (ulong)sizeof(control_t);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong tlsf_align_size() => ALIGN_SIZE;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong tlsf_block_size_min() => block_size_min;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong tlsf_block_size_max() => block_size_max;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong tlsf_pool_overhead() => 2 * block_header_overhead;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong tlsf_alloc_overhead() => block_header_overhead;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* tlsf_add_pool(void* tlsf, void* mem, ulong bytes)
    {
        block_header_t* block;
        block_header_t* next;
        var pool_overhead = tlsf_pool_overhead();
        var pool_bytes = align_down(bytes - pool_overhead, ALIGN_SIZE);
        if ((long)mem % ALIGN_SIZE != 0)
            return null;
        if (pool_bytes < block_size_min || pool_bytes > block_size_max)
        {
            Console.WriteLine("tlsf_add_pool: Memory size must be between 0x{0:X} and 0x{1:X}00 bytes.",
                (uint)(pool_overhead + block_size_min), (uint)((pool_overhead + block_size_max) / 256));
            return null;
        }

        // 把地址偏移到block头第一项prev_phys_block之后的位置
        block = offset_to_block(mem, unchecked((ulong)-(nint)block_header_overhead));
        block_set_size(block, pool_bytes);
        block_set_free(block);
        block_set_prev_used(block);
        block_insert((control_t*)tlsf, block);
        next = block_link_next(block);
        block_set_size(next, 0);
        block_set_used(next);
        block_set_prev_free(next);
        return mem;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void tlsf_remove_pool(void* tlsf, void* pool)
    {
        var control = (control_t*)tlsf;
        var block = offset_to_block(pool, unchecked((ulong)-(int)block_header_overhead));
        int fl = 0, sl = 0;
        tlsf_assert(block_is_free(block) != 0, "block should be free");
        tlsf_assert(block_is_free(block_next(block)) == 0, "next block should not be free");
        tlsf_assert(block_size(block_next(block)) == 0, "next block size should be zero");
        mapping_insert(block_size(block), &fl, &sl);
        remove_free_block(control, block, fl, sl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* tlsf_create(void* mem)
    {
        if ((nint)mem % ALIGN_SIZE != 0)
            return null;
        control_construct((control_t*)mem);
        return mem;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* tlsf_create_with_pool(void* mem, ulong bytes)
    {
        // 此处得到control_t，但是还没有切分并放入内存
        var tlsf = tlsf_create(mem);
        // 这里把头部剪掉，传入剩下的内存来切分
        tlsf_add_pool(tlsf, (byte*)mem + tlsf_size(), bytes - tlsf_size());
        return tlsf;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* tlsf_get_pool(void* tlsf) => (byte*)tlsf + tlsf_size();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* tlsf_malloc(void* tlsf, ulong size)
    {
        var control = (control_t*)tlsf;
        // 只有小于32字节时才会进行内存对齐，一个block header的大小是32字节，要保证内存还回来时能够转换成block header
        var adjust = adjust_request_size(size, ALIGN_SIZE);
        var block = block_locate_free(control, adjust);
        return block_prepare_used(control, block, adjust);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* tlsf_memalign(void* tlsf, ulong align, ulong size)
    {
        var control = (control_t*)tlsf;
        var adjust = adjust_request_size(size, ALIGN_SIZE);
        var gap_minimum = (ulong)sizeof(block_header_t);
        var size_with_gap = adjust_request_size(adjust + align + gap_minimum, align);
        var aligned_size = adjust != 0 && align > ALIGN_SIZE ? size_with_gap : adjust;
        var block = block_locate_free(control, aligned_size);
        tlsf_assert(sizeof(block_header_t) == (int)(block_size_min + block_header_overhead));
        if (block != null)
        {
            var ptr = block_to_ptr(block);
            var aligned = align_ptr(ptr, align);
            var gap = (ulong)((nint)aligned - (nint)ptr);
            if (gap != 0 && gap < gap_minimum)
            {
                var gap_remain = gap_minimum - gap;
                var offset = tlsf_max(gap_remain, align);
                var next_aligned = (void*)((nint)aligned + (nint)offset);
                aligned = align_ptr(next_aligned, align);
                gap = (ulong)((nint)aligned - (nint)ptr);
            }

            if (gap != 0)
            {
                tlsf_assert(gap >= gap_minimum, "gap size too small");
                block = block_trim_free_leading(control, block, gap);
            }
        }

        return block_prepare_used(control, block, adjust);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void tlsf_free(void* tlsf, void* ptr)
    {
        if (ptr != null)
        {
            var control = (control_t*)tlsf;
            var block = block_from_ptr(ptr);
            tlsf_assert(!(block_is_free(block) != 0), "block already marked as free");
            block_mark_as_free(block);
            block = block_merge_prev(control, block);
            block = block_merge_next(control, block);
            block_insert(control, block);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* tlsf_realloc(void* tlsf, void* ptr, ulong size)
    {
        var control = (control_t*)tlsf;
        void* p = null;
        if (ptr != null && size == 0)
            tlsf_free(tlsf, ptr);
        else if (!(ptr != null))
            p = tlsf_malloc(tlsf, size);
        else
        {
            var block = block_from_ptr(ptr);
            var next = block_next(block);
            var cursize = block_size(block);
            var combined = cursize + block_size(next) + block_header_overhead;
            var adjust = adjust_request_size(size, ALIGN_SIZE);
            tlsf_assert(!(block_is_free(block) != 0), "block already marked as free");
            if (adjust > cursize && (!(block_is_free(next) != 0) || adjust > combined))
            {
                p = tlsf_malloc(tlsf, size);
                if (p != null)
                {
                    var minsize = tlsf_min(cursize, size);
                    memcpy(p, ptr, minsize);
                    tlsf_free(tlsf, ptr);
                }
            }
            else
            {
                if (adjust > cursize)
                {
                    block_merge_next(control, block);
                    block_mark_as_used(block);
                }

                block_trim_used(control, block, adjust);
                p = ptr;
            }
        }

        return p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static block_header_t** get_blocks(control_t* control, int i) =>
        (block_header_t**)control->blocks + i * SL_INDEX_COUNT;

    [StructLayout(LayoutKind.Sequential)] public struct integrity_t
    {
        public int prev_status;
        public int status;
    }

    [StructLayout(LayoutKind.Sequential)] public struct block_header_t
    {
        public block_header_t* prev_phys_block;
        public ulong size;
        public block_header_t* next_free;
        public block_header_t* prev_free;
    }

    [StructLayout(LayoutKind.Sequential)] public struct control_t
    {
        public block_header_t block_null;
        public uint fl_bitmap;
        public fixed uint sl_bitmap[FL_INDEX_COUNT];
        // 每一级的内存链表首地址
        public fixed ulong blocks[FL_INDEX_COUNT * SL_INDEX_COUNT];
    }
}