using System;
using System.Collections;

namespace BorrowTomorrow.Core.Interfaces
{
    // A fall-only presentation. Manual checkpoint restores deliberately bypass this service.
    public interface IFallMemoryService
    {
        bool HasNext { get; }
        IEnumerator Play(Action restoreAtBlack);
    }
}
