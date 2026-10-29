using System;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    // A local interaction may precede a story without changing its dialogue chain.
    public abstract class NarrativeInteractionGate : MonoBehaviour
    {
        public abstract bool ShouldOpen { get; }
        public abstract bool Open(Action onContinue);
        public virtual bool HasCompletedView => false;
        public virtual string CompletedPrompt => string.Empty;
        public virtual bool OpenCompleted() => false;
    }
}
