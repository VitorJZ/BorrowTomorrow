using BorrowTomorrow.Core.Data;

namespace BorrowTomorrow.Core.Interfaces
{
    public interface INarrativeSceneTransitionView
    {
        void PresentSceneTransition(DialogueEntry previous, DialogueEntry next, float elapsed, bool blocking);
    }

    public interface INarrativePresentation
    {
        void Present(DialogueEntry entry, string text, bool blocking, int page, int pages);
        void Close();
    }
}
