using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using UnityEditor;
GameSession.Instance.LoadLevel(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Data/Levels/L01_L01_Courtyard.asset"));
return "Loading L01";
