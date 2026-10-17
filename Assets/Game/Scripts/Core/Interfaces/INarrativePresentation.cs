using BorrowTomorrow.Core.Data;

namespace BorrowTomorrow.Core.Interfaces
{
    public interface INarrativePresentation
    {
        void Present(DialogueEntry entry, string text, bool blocking, int page, int pages);
        void Close();
    }
}
