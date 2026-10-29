namespace BorrowTomorrow.Narrative
{
    // No scene/UI dependencies: incorrect photos may physically fit the same paper edge.
    // Correctness is checked only when the player elects to inspect the assembled photo.
    public sealed class PhotoReconstructionState
    {
        public const int PieceCount = 5;
        public const int FixedSlot = 3;
        public static readonly int[] Shapes = { 0, 1, 2, 0, 1 };
        private readonly int[] slots = { -1, -1, -1, -1, -1 };
        public int SlotOf(int piece) => Valid(piece) ? slots[piece] : -1;
        public int PieceAt(int slot)
        {
            for (int i = 0; i < PieceCount; i++) if (slots[i] == slot) return i;
            return -1;
        }
        public int FilledSlots
        {
            get { int n = 1; for (int i = 0; i < PieceCount; i++) if (slots[i] >= 0) n++; return n; }
        }
        public bool IsCorrect => slots[0] == 0 && slots[1] == 1 && slots[2] == 2;
        public bool Place(int piece, int slot)
        {
            if (!Valid(piece) || slot < 0 || slot >= FixedSlot) return false;
            int prior = PieceAt(slot);
            if (prior >= 0) slots[prior] = -1;
            slots[piece] = slot;
            return true;
        }
        public void Remove(int piece) { if (Valid(piece)) slots[piece] = -1; }
        private static bool Valid(int piece) => piece >= 0 && piece < PieceCount;
    }
}
