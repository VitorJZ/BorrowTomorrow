#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using UnityEngine;
namespace BorrowTomorrow.Diagnostics
{
    /// Explicit level-select launch. Does not solve or automate any gameplay.
    public sealed class CorridorLevelPreview : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            bool requested=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--l03-preview");
#if UNITY_EDITOR
            requested |= UnityEditor.SessionState.GetBool("BorrowTomorrow.L03Preview",false);
#endif
            if(requested && FindObjectOfType<CorridorLevelPreview>()==null)
                new GameObject("L03PreviewEntry").AddComponent<CorridorLevelPreview>();
        }
        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            float deadline=Time.realtimeSinceStartup+30;
            while(GameSession.Instance==null || GameSession.Instance.State.Phase!=GamePhase.MainMenu)
            {if(Time.realtimeSinceStartup>deadline){Destroy(gameObject);yield break;}yield return null;}
            var s=GameSession.Instance;
            var definition=Resources.FindObjectsOfTypeAll<LevelDefinition>().FirstOrDefault(d=>d.levelId=="L03");
            if(definition==null){Debug.LogError("[L03Preview] Missing L03 definition");Destroy(gameObject);yield break;}
            s.StoryFlags.Clear();s.StoryFlags.SetFlag("IntroPlayed");s.StoryFlags.SetFlag("UniformRead");
            if(ServiceRegistry.TryGet<ICheckpointService>(out var cp))cp.RestoreFrom(null);
            s.LoadLevel(definition);
            while(s.State.Phase!=GamePhase.Playing){if(Time.realtimeSinceStartup>deadline){Destroy(gameObject);yield break;}yield return null;}
            
            Destroy(gameObject);
        }
    }
}
#endif
