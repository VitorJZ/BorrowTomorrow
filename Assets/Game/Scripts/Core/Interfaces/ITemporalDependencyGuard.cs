using BorrowTomorrow.Core.Data;

namespace BorrowTomorrow.Core.Interfaces
{
    /// Checks indirect supports before the boundary commits a threshold group.
    public interface ITemporalDependencyGuard
    {
        bool AllowsChange(string[] changingIds, Era targetEra, IPlayerContext player);
    }
}
