using System.Runtime.CompilerServices;
using System.Threading;

namespace NCoreUtils.IO
{
    // static class CasLock
    // {
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public static bool TryLock(ref int _sync)
    //         => 0 == Interlocked.CompareExchange(ref _sync, 1, 0);
    //
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public static void Lock(ref int _sync)
    //     {
    //         while (!TryLock(ref _sync)) { }
    //     }
    //
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public static void Release(ref int _sync)
    //         => Interlocked.CompareExchange(ref _sync, 0, 1);
    // }
}